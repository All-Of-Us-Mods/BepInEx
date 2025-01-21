using System;
using System.Runtime.InteropServices;

namespace BepInEx.Unity.IL2CPP.Hook;

public static unsafe partial class BruthaInterop
{
    [LibraryImport("brutha", EntryPoint = "write_line", StringMarshalling = StringMarshalling.Utf16)]
    public static unsafe partial void write_line([MarshalAs(UnmanagedType.LPStr)] string message);

    [LibraryImport("brutha", EntryPoint = "hook")]
    public static unsafe partial IntPtr hook(IntPtr target, IntPtr detour);

    [LibraryImport("brutha", EntryPoint = "unhook")]
    public static unsafe partial void unhook(IntPtr target);
}
