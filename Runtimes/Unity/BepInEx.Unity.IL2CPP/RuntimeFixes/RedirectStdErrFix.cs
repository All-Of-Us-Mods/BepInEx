using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace BepInEx.IL2CPP.RuntimeFixes;

internal static class RedirectStdErrFix
{
    private const int STD_ERROR_HANDLE = -12;
    private const int INVALID_HANDLE_VALUE = -1;
    private const int FILE_SHARE_READ = 1;
    private const int GENERIC_WRITE = 0x40000000;
    private const int CREATE_ALWAYS = 2;
    private const int FILE_ATTRIBUTE_NORMAL = 0x00000080;

    /*
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint CreateFile(string fileName,
                                          uint desiredAccess,
                                          int shareMode,
                                          nint securityAttributes,
                                          int creationDisposition,
                                          int flagsAndAttributes,
                                          nint templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetStdHandle(int nStdHandle, nint hConsoleOutput);
    */

    // using our custom "doorstop" replacement
    [DllImport("brutha", EntryPoint = "write_line", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
    public static extern void write_line([MarshalAs(UnmanagedType.LPStr)] string message);

    public static void Apply()
    {
        // custom solution, redirecting stuff to our android thingy.
        Console.SetOut(new CustomWriter(write_line));
        Console.SetError(new CustomWriter(write_line));

        /*
        if (PlatformHelper.Is(Platform.Windows))
        {
            var errorFile = CreateFile(Path.Combine(Paths.BepInExRootPath, "ErrorLog.log"), GENERIC_WRITE,
                                       FILE_SHARE_READ,
                                       0, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, 0);
            if (errorFile == INVALID_HANDLE_VALUE)
            {
                Logger.Log(LogLevel.Warning, "Failed to open error log file; skipping error redirection");
                return;
            }

            if (!SetStdHandle(STD_ERROR_HANDLE, errorFile))
                Logger.Log(LogLevel.Warning, "Failed to redirect stderr; skipping error redirection");
        }*/
        // On unix, we can generally redirect stderr to a file "normally" via piping
    }

    private class CustomWriter : TextWriter
    {
        private readonly Action<string> _writeAction;

        public CustomWriter(Action<string> writeAction)
        {
            _writeAction = writeAction;
        }

        public override void WriteLine(string? value)
        {
            _writeAction(value ?? string.Empty);
        }

        public override void Write(char value)
        {
            _writeAction(value.ToString());
        }

        public override Encoding Encoding => Encoding.Unicode;
    }
}
