using System;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using Il2CppInterop.Runtime.Injection;

namespace BepInEx.Unity.IL2CPP.Hook;

public unsafe class NativeDetour : IDetour
{
    protected static readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("NativeDetour");

    public IntPtr Target { get; }
    public IntPtr Detour { get; }
    public IntPtr OriginalTrampoline { get; private set; }

    public NativeDetour(IntPtr target, Delegate detour)
    {
        Target = target;
        Detour = Marshal.GetFunctionPointerForDelegate(detour);
        Logger.Log(LogLevel.Debug, $"Creating detour from 0x{Target:X2} to 0x{Detour:X2}");
        Apply();
    }

    public void Apply()
    {
        if (OriginalTrampoline != IntPtr.Zero)
        {
            return;
        }
        OriginalTrampoline = BruthaInterop.hook(Target, Detour);
        Logger.Log(LogLevel.Debug, $"Original: {Target:X}, Trampoline: {OriginalTrampoline:X}, diff: {Math.Abs(Target - OriginalTrampoline):X}");
    }
    
    public void Dispose()
    {
        if (OriginalTrampoline == IntPtr.Zero)
        {
            return;
        }
        BruthaInterop.unhook(Target);
    }

    public T GenerateTrampoline<T>() where T : Delegate
    {
        return Marshal.GetDelegateForFunctionPointer<T>(OriginalTrampoline);
    }
}
