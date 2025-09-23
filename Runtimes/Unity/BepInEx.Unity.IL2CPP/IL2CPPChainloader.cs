using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Preloader.Core.Logging;
using BepInEx.Unity.IL2CPP.Hook;
using BepInEx.Unity.IL2CPP.Logging;
using BepInEx.Unity.IL2CPP.Utils;
using Il2CppInterop.Runtime.InteropTypes;
using Logger = BepInEx.Logging.Logger;

namespace BepInEx.Unity.IL2CPP;

public class IL2CPPChainloader : BaseChainloader<BasePlugin>
{
    private static RuntimeInvokeDetourDelegate originalInvoke;

    private static readonly ConfigEntry<bool> ConfigUnityLogging = ConfigFile.CoreConfig.Bind(
     "Logging", "UnityLogListening",
     true,
     "Enables showing unity log messages in the BepInEx logging system.");

    private static readonly ConfigEntry<bool> ConfigDiskWriteUnityLog = ConfigFile.CoreConfig.Bind(
     "Logging.Disk", "WriteUnityLog",
     false,
     "Include unity log messages in log file output.");


    private static NativeDetour RuntimeInvokeDetour { get; set; }

    public static IL2CPPChainloader Instance { get; set; }

    /// <summary>
    ///     Register and add a Unity Component (for example MonoBehaviour) into BepInEx global manager.
    ///     Automatically registers the type with Il2Cpp type system if it isn't initialised already.
    /// </summary>
    /// <typeparam name="T">Type of the component to add.</typeparam>
    public static T AddUnityComponent<T>() where T : Il2CppObjectBase => AddUnityComponent(typeof(T)).Cast<T>();

    /// <summary>
    ///     Register and add a Unity Component (for example MonoBehaviour) into BepInEx global manager.
    ///     Automatically registers the type with Il2Cpp type system if it isn't initialised already.
    /// </summary>
    /// <param name="t">Type of the component to add</param>
    public static Il2CppObjectBase AddUnityComponent(Type t) => Il2CppUtils.AddComponent(t);

    /// <summary>
    ///     Occurs after a plugin is instantiated and just before <see cref="BasePlugin.Load"/> is called.
    /// </summary>
    public event Action<PluginInfo, Assembly, BasePlugin> PluginLoad;

    public override void Initialize(string gameExePath = null)
    {
        base.Initialize(gameExePath);
        Instance = this;
        // OnInvokeHook is handled by Starlight native patches.
    }

    protected override void InitializeLoggers()
    {
        base.InitializeLoggers();

        if (!ConfigDiskWriteUnityLog.Value) DiskLogListener.BlacklistedSources.Add("Unity");

        ChainloaderLogHelper.RewritePreloaderLogs();

        Logger.Sources.Add(new IL2CPPLogSource());
    }

    public override void Execute()
    {
        try
        {
            StarlightInterop.set_loading(true);

            var paths = new List<string> { Paths.PluginPath };

            if (StarlightEntrypoint.ModProfileDirectory != null &&
                Directory.Exists(StarlightEntrypoint.ModProfileDirectory))
            {
                paths.Add(StarlightEntrypoint.ModProfileDirectory);
            }

            var data = StarlightEntrypoint.ProfileData;

            if (data != null)
            {
                Logger.Log(LogLevel.Info, "Loading Profile plugins...");
                var modsPath = Path.Combine(Utility.ParentDirectory(Paths.BepInExRootPath), "starlight_mods");
                foreach (var (mod, version) in data.Value.mods)
                {
                    if (data.Value.disabledMods.Contains(mod))
                    {
                        continue;
                    }

                    var versionPath = Path.Combine(modsPath, mod, version);
                    if (Directory.Exists(versionPath))
                    {
                        paths.Add(versionPath);
                    }
                    else
                    {
                        Logger.Log(LogLevel.Error, "Directory does not exist: " + versionPath);
                    }
                }
            }
            else
            {
                Logger.Log(LogLevel.Warning, "No profile data found, skipping profile plugins.");
            }

            var plugins = new List<PluginInfo>();
            foreach (var pluginsPath in paths)
            {
                plugins.AddRange(DiscoverPluginsFrom(pluginsPath)
                                     .Where(plugin => data == null ||
                                                      !data.Value.disabledMods.Contains(plugin.Metadata.GUID) &&
                                                      !data.Value.disabledMods.Contains(plugin.Location)));
            }

            StarlightInterop.set_loading_count(plugins.Count);
            LoadPlugins(plugins);

            Finish();
        }
        catch (Exception ex)
        {
            try
            {
                ConsoleManager.CreateConsole();
            }
            catch { }

            Logger.Log(LogLevel.Error, $"Error occurred loading plugins: {ex}");
        }
        finally
        {
            StarlightInterop.set_loading(false);
        }

        Logger.Log(LogLevel.Message, "Chainloader startup complete");
    }

    public override BasePlugin LoadPlugin(PluginInfo pluginInfo, Assembly pluginAssembly)
    {
        StarlightInterop.set_loading_text(pluginInfo.Metadata.Name);

        var type = pluginAssembly.GetType(pluginInfo.TypeName);

        var pluginInstance = (BasePlugin) Activator.CreateInstance(type);

        PluginLoad?.Invoke(pluginInfo, pluginAssembly, pluginInstance);
        pluginInstance.Load();

        StarlightInterop.increment_loading();

        return pluginInstance;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr RuntimeInvokeDetourDelegate(IntPtr method, IntPtr obj, IntPtr parameters, IntPtr exc);
}
