using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using BepInEx.Logging;
using BepInEx.Preloader.Core;
using BepInEx.Unity.IL2CPP.Hook;
using BepInEx.Unity.IL2CPP.Utils;

namespace BepInEx.Unity.IL2CPP;

internal static class Entrypoint
{
    public struct Data
    {
        public string DataPath;
        public string AuLibsPath;
        public string RedirectLibsPath;
    }

    /// <summary>
    ///     The main entrypoint of BepInEx, called from Doorstop.
    /// </summary>
    public static int Start(IntPtr arg, int argLength)
    {
        Console.SetOut(new BruthaInterop.InteropWriter());
        Console.SetError(new BruthaInterop.InteropWriter());

        var data = Marshal.PtrToStructure<Data>(arg);
        var dotnet = Path.Join(data.DataPath, "dotnet");
        var bepinPath = Path.Join(data.DataPath, "BepInEx", "Core");
        var auIl2Cpp = Path.Join(data.AuLibsPath, "libil2cpp.so");

        // override doorstop env vars cuz we arent using them.
        Environment.SetEnvironmentVariable("DOORSTOP_INVOKE_DLL_PATH", Assembly.GetExecutingAssembly().Location);
        Environment.SetEnvironmentVariable("DOORSTOP_MANAGED_FOLDER_DIR", dotnet);
        Environment.SetEnvironmentVariable("DOORSTOP_PROCESS_PATH", auIl2Cpp);
        Environment.SetEnvironmentVariable("DOORSTOP_DLL_SEARCH_DIRS", dotnet+Path.PathSeparator+bepinPath);
        Environment.SetEnvironmentVariable("BEPINEX_GAME_ASSEMBLY_PATH", auIl2Cpp);
        Environment.SetEnvironmentVariable("METADATA_PATH", Path.Join(data.DataPath, "global-metadata.dat"));

        // We set it to the current directory first as a fallback, but try to use the same location as the .exe file.
        var silentExceptionLog = Environment.GetEnvironmentVariable("BEPINEX_PRELOADER_LOG") ?? $"preloader_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log";
        
        // mutex handling is done in native.
        
        try
        {
            EnvVars.LoadVars();

            silentExceptionLog = Path.Combine(data.DataPath, silentExceptionLog);

            UnityPreloaderRunner.PreloaderMain();
        }
        catch (Exception ex)
        {
            File.WriteAllText(silentExceptionLog, ex.ToString());

            try
            {
                if (NotifySend.IsSupported)
                {
                    NotifySend.Send("Failed to start BepInEx", "Check logs for details");
                }
                else if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BEPINEX_FAIL_FAST")))
                {
                    // Don't exit the game if we have no way of signaling to the user that a crash happened
                    return 0;
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
