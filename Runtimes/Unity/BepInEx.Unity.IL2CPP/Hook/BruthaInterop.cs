using System;
using System.Runtime.InteropServices;

namespace BepInEx.Unity.IL2CPP.Hook;

public static class BruthaInterop
{
    [DllImport("brutha", EntryPoint = "write_line", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void write_line([MarshalAs(UnmanagedType.LPStr)] string message);

    [DllImport("brutha", EntryPoint = "hook", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr hook(IntPtr target, IntPtr detour);

    [DllImport("brutha", EntryPoint = "unhook", CallingConvention = CallingConvention.Cdecl)]
    public static extern void unhook(IntPtr target);
}
