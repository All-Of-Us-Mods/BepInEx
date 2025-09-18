using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace BepInEx.Unity.IL2CPP;

internal static partial class StarlightInterop
{
    private const string LIBRARY_NAME = "starlight";

    [LibraryImport(LIBRARY_NAME, EntryPoint = "write_log", StringMarshalling = StringMarshalling.Utf8)]
    public static unsafe partial void write_log([MarshalAs(UnmanagedType.LPStr)] string message);

    [LibraryImport(LIBRARY_NAME)]
    public static unsafe partial IntPtr hook(IntPtr target, IntPtr detour);

    [LibraryImport(LIBRARY_NAME)]
    public static unsafe partial void unhook(IntPtr target);

    [LibraryImport(LIBRARY_NAME)]
    public static unsafe partial void set_loading([MarshalAs(UnmanagedType.I1)] bool loading);

    [LibraryImport(LIBRARY_NAME)]
    public static unsafe partial void set_loading_text([MarshalAs(UnmanagedType.LPStr)] string text);

    [LibraryImport(LIBRARY_NAME)]
    public static unsafe partial void set_loading_count(int count);

    [LibraryImport(LIBRARY_NAME)]
    public static unsafe partial void increment_loading();
    
    public class InteropWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8; 

        private readonly StringBuilder buffer = new(1024);

        public override void WriteLine(string value)
        {
            buffer.AppendLine(value);
            FlushBuffer();
        }

        public override void Write(char value)
        {
            buffer.Append(value);

            if (value == '\n')
            {
                FlushBuffer();
            }
        }

        public void FlushBuffer()
        {
            if (buffer.Length == 0)
                return;

            write_log(buffer.ToString());
            buffer.Clear();
        }
    }
}
