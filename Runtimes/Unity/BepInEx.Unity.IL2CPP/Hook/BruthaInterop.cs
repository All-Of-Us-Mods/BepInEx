using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace BepInEx.Unity.IL2CPP.Hook;

public static unsafe partial class BruthaInterop
{
    [LibraryImport("brutha", EntryPoint = "write_log", StringMarshalling = StringMarshalling.Utf16)]
    public static unsafe partial void write_log([MarshalAs(UnmanagedType.LPStr)] string message);

    [LibraryImport("brutha", EntryPoint= "flush_log", StringMarshalling = StringMarshalling.Utf16)]
    public static unsafe partial void flush_log();

    [LibraryImport("brutha", EntryPoint = "hook")]
    public static unsafe partial IntPtr hook(IntPtr target, IntPtr detour);

    [LibraryImport("brutha", EntryPoint = "unhook")]
    public static unsafe partial void unhook(IntPtr target);

    
    public class InteropWriter : TextWriter
    {
        public override void WriteLine(string value)
        {
            write_log(value + Environment.NewLine);
        }

        public override void Write(char value)
        {
            write_log(value.ToString());
        }

        public override Encoding Encoding => Encoding.Unicode;
    }
}
