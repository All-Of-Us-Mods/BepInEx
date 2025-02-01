using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace BepInEx.Unity.IL2CPP;

public static unsafe partial class StarlightInterop
{
#if NET7_0_OR_GREATER
    [LibraryImport("brutha", StringMarshalling = StringMarshalling.Utf8)]
    public static unsafe partial void write_log([MarshalAs(UnmanagedType.LPStr)] string message);

    [LibraryImport("brutha", StringMarshalling = StringMarshalling.Utf8)]
    public static unsafe partial void flush_log();

    [LibraryImport("brutha")]
    public static unsafe partial IntPtr hook(IntPtr target, IntPtr detour);

    [LibraryImport("brutha")]
    public static unsafe partial void unhook(IntPtr target);

    [LibraryImport("brutha")]
    public static unsafe partial void thread_suspend_reload();
#else
    [DllImport("brutha", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void write_log([MarshalAs(UnmanagedType.LPStr)] string message);

    [DllImport("brutha", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void flush_log();

    [DllImport("brutha", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr hook(IntPtr target, IntPtr detour);

    [DllImport("brutha", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void unhook(IntPtr target);

    [DllImport("brutha", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void thread_suspend_reload();
#endif
    
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
