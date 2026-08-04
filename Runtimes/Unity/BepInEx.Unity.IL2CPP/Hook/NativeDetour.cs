using System;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using Il2CppInterop.Runtime.Injection;

namespace BepInEx.Unity.IL2CPP.Hook;

public unsafe class NativeDetour : IDetour
{
    private static readonly ManualLogSource Log = Logger.CreateLogSource("NativeDetour");

    public IntPtr Target { get; }
    public IntPtr Detour { get; }
    public bool SpecialReturnBuffer { get; }
    public IntPtr OriginalTrampoline { get; private set; }

    public NativeDetour(IntPtr target, Delegate detour)
        : this(target, detour, false)
    {
    }

    public NativeDetour(IntPtr target, Delegate detour, bool specialReturnBuffer)
    {
        Target = target;
        Detour = Marshal.GetFunctionPointerForDelegate(detour);
        SpecialReturnBuffer = specialReturnBuffer;
        Apply();
    }

    public void Apply()
    {
        if (OriginalTrampoline != IntPtr.Zero)
        {
            return;
        }

        var originalTrampoline = StarlightInterop.hook(Target, Detour, SpecialReturnBuffer);
        if (originalTrampoline == IntPtr.Zero)
        {
            Log.LogWarning(
                $"Failed to install native detour at 0x{Target:X}; Starlight returned a null original trampoline");
            return;
        }

        OriginalTrampoline = originalTrampoline;
    }
    
    public void Dispose()
    {
        if (OriginalTrampoline == IntPtr.Zero)
        {
            return;
        }
        StarlightInterop.unhook(Target);
        OriginalTrampoline = IntPtr.Zero;
    }

    public T GenerateTrampoline<T>() where T : Delegate
    {
        return Marshal.GetDelegateForFunctionPointer<T>(OriginalTrampoline);
    }
}
