using System.IO;
using System.Runtime.InteropServices;
using System;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Preloader.Core;
using BepInEx.Unity.IL2CPP.Utils;
using MonoMod.Utils;

namespace BepInEx.Unity.IL2CPP;

internal static unsafe class StarlightEntrypoint
{
    public static string? ModProfileDirectory { get; private set; }
    public static ModProfileJson? ProfileData { get; private set; }

    [StructLayout(LayoutKind.Sequential)]
    public struct StarlightData
    {
        public IntPtr DataPath;
        public IntPtr AuLibsPath;
        public IntPtr ChainloaderFunc;
        public IntPtr GarbageCollectionFunc;
        public IntPtr ProfilePath;
    }

    [UnmanagedCallersOnly]
    private static void StartChainloader()
    {
        Il2CppInteropManager.PreloadInteropAssemblies();
        IL2CPPChainloader.Instance.Execute();
    }

    [UnmanagedCallersOnly]
    private static void GarbageCollection()
    {
        Task.Run(() =>
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized);
            GC.Collect();
        });
    }

    public delegate int StartDelegate(StarlightData* data);

    [UnmanagedCallersOnly(EntryPoint = "Start")]
    public static int Start(StarlightData* data)
    {
        Console.SetOut(new StarlightInterop.InteropWriter());
        Console.SetError(new StarlightInterop.InteropWriter());

        var dataPath = Marshal.PtrToStringAnsi(data->DataPath);
        var auLibsPath = Marshal.PtrToStringAnsi(data->AuLibsPath);
        var profilePath = Marshal.PtrToStringAnsi(data->ProfilePath);

        if (File.Exists(profilePath))
        {
            using var profileFile = File.OpenRead(profilePath);
            ModProfileDirectory = Path.GetDirectoryName(profilePath);
            ProfileData = JsonSerializer.Deserialize<ModProfileJson>(profileFile);
        }

        var dotnet = Path.Join(dataPath, "dotnet");
        var bepinPath = Path.Join(dataPath, "BepInEx", "Core");
        var auIl2Cpp = Path.Join(auLibsPath, "libil2cpp.so");

        data->GarbageCollectionFunc = (IntPtr)(delegate* unmanaged<void>)&GarbageCollection;
        data->ChainloaderFunc = (IntPtr)(delegate* unmanaged<void>)&StartChainloader;

        // override doorstop env vars cuz we arent using them.
        Environment.SetEnvironmentVariable("DOORSTOP_INVOKE_DLL_PATH", Assembly.GetExecutingAssembly().Location);
        Environment.SetEnvironmentVariable("DOORSTOP_MANAGED_FOLDER_DIR", dotnet);
        Environment.SetEnvironmentVariable("DOORSTOP_PROCESS_PATH", auIl2Cpp);
        Environment.SetEnvironmentVariable("DOORSTOP_DLL_SEARCH_DIRS", dotnet+Path.PathSeparator+bepinPath);
        Environment.SetEnvironmentVariable("BEPINEX_GAME_ASSEMBLY_PATH", auIl2Cpp);
        Environment.SetEnvironmentVariable("METADATA_PATH", Path.Join(dataPath, "global-metadata.dat"));

        // We set it to the current directory first as a fallback, but try to use the same location as the .exe file.
        var silentExceptionLog = Environment.GetEnvironmentVariable("BEPINEX_PRELOADER_LOG") ?? $"preloader_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log";
        
        try
        {
            EnvVars.LoadVars();

            silentExceptionLog = Path.Combine(dataPath, silentExceptionLog);

            UnityPreloaderRunner.PreloaderMain();
        }
        catch (Exception ex)
        {
            File.WriteAllText(silentExceptionLog, ex.ToString());

            try
            {
                if (PlatformDetection.OS is OSKind.Windows)
                {
                    MessageBox.Show("Failed to start BepInEx", "BepInEx");
                }
                else if (NotifySend.IsSupported)
                {
                    NotifySend.Send("Failed to start BepInEx", "Check logs for details");
                }
                else if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BEPINEX_FAIL_FAST")))
                {
                    // Don't exit the game if we have no way of signaling to the user that a crash happened
                    return 1;
                }
            }
            catch (Exception)
            {
                // ignored
            }

            Environment.Exit(1);
        }

        return 0;
    }
}
