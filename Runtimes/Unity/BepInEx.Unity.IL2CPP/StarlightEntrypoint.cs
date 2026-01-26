using System.IO;
using System.Runtime.InteropServices;
using System;
using System.Reflection;
using System.Text.Json;
using BepInEx.Preloader.Core;
using BepInEx.Unity.IL2CPP.Utils;
using MonoMod.Utils;

namespace BepInEx.Unity.IL2CPP;

internal static unsafe class StarlightEntrypoint
{
    public static string? ModProfileDirectory { get; private set; }
    public static string? FilesDirectory { get; private set; }
    public static ModProfileJson? ProfileData { get; private set; }

    [StructLayout(LayoutKind.Sequential)]
    public struct StarlightData
    {
        public IntPtr DataPath;
        public IntPtr FilesPath;
        public IntPtr AuLibsPath;
        public IntPtr ProfilePath;
        public IntPtr ChainloaderFunc;
    }

    [UnmanagedCallersOnly]
    private static void StartChainloader()
    {
        Il2CppInteropManager.PreloadInteropAssemblies();
        IL2CPPChainloader.Instance.Execute();
    }

    public delegate int StartDelegate(StarlightData* data);
    
    private static string ErrorLogPath { get; set; } = "ErrorLog.log";
    private static string SilentExceptionLog { get; set; } = $"preloader_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log";

    [UnmanagedCallersOnly(EntryPoint = "Start")]
    public static int Start(StarlightData* data)
    {
        Console.SetOut(new StarlightInterop.InteropWriter());
        Console.SetError(new StarlightInterop.InteropWriter());

        var dataPath = Marshal.PtrToStringAnsi(data->DataPath);
        var auLibsPath = Marshal.PtrToStringAnsi(data->AuLibsPath);
        var profilePath = Marshal.PtrToStringAnsi(data->ProfilePath);
        FilesDirectory = Marshal.PtrToStringAnsi(data->FilesPath);

        if (dataPath is null || auLibsPath is null || FilesDirectory is null)
        {
            StarlightInterop.create_alert("BepInEx Startup Error", "One or more required paths are null.");
            StarlightInterop.write_log($"[StarlightEntrypoint] One or more required paths are null. DataPath: {dataPath}, AuLibsPath: {auLibsPath}, FilesDirectory: {FilesDirectory}");
            return 1;
        }

        if (File.Exists(profilePath))
        {
            using var profileFile = File.OpenRead(profilePath);
            ModProfileDirectory = Path.GetDirectoryName(profilePath);
            ProfileData = JsonSerializer.Deserialize<ModProfileJson>(profileFile);
        }

        var auIl2Cpp = Path.Join(auLibsPath, "libil2cpp.so");

        data->ChainloaderFunc = (IntPtr)(delegate* unmanaged<void>)&StartChainloader;

        // override doorstop env vars cuz we arent using them.
        Environment.SetEnvironmentVariable("DOORSTOP_INVOKE_DLL_PATH", Assembly.GetExecutingAssembly().Location);
        Environment.SetEnvironmentVariable("DOORSTOP_PROCESS_PATH", auIl2Cpp);
        Environment.SetEnvironmentVariable("DOORSTOP_MANAGED_FOLDER_DIR", FilesDirectory);
        Environment.SetEnvironmentVariable("BEPINEX_GAME_ASSEMBLY_PATH", auIl2Cpp);

        // We set it to the current directory first as a fallback, but try to use the same location as the .exe file.
        SilentExceptionLog = Environment.GetEnvironmentVariable("BEPINEX_PRELOADER_LOG") ??  $"preloader_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log";

        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            try
            {
                if (args.ExceptionObject is Exception ex)
                {
                    File.WriteAllText(ErrorLogPath, ex.ToString());
                }
            }
            catch (Exception)
            {
                // ignored
            }
        };
        
        try
        {
            EnvVars.LoadVars();

            SilentExceptionLog = Path.Combine(FilesDirectory, SilentExceptionLog);
            ErrorLogPath = Path.Combine(FilesDirectory, ErrorLogPath);

            UnityPreloaderRunner.PreloaderMain();
        }
        catch (Exception ex)
        {
            File.WriteAllText(SilentExceptionLog, ex.ToString());

            try
            {
                StarlightInterop.create_alert("Failed to start BepInEx", $"Check log file for details:\n{SilentExceptionLog}");
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
