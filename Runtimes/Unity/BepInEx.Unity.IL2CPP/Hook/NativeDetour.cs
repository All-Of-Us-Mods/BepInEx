using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.Injection;

namespace BepInEx.Unity.IL2CPP.Hook;

public unsafe class NativeDetour : IDetour
{
    public IntPtr Target { get; }
    public IntPtr Detour { get; }
    public bool UnityFunction { get; }
    public bool SpecialReturnBuffer { get; }
    public IntPtr OriginalTrampoline { get; private set; }

    public NativeDetour(IntPtr target, Delegate detour)
        : this(target, detour, false, false)
    {
    }

    public NativeDetour(IntPtr target, Delegate detour, bool unityFunction, bool specialReturnBuffer)
    {
        Target = target;
        Detour = Marshal.GetFunctionPointerForDelegate(detour);
        UnityFunction = unityFunction;
        SpecialReturnBuffer = specialReturnBuffer;
        Apply();
    }

    public void Apply()
    {
        if (OriginalTrampoline != IntPtr.Zero)
        {
            return;
        }
        OriginalTrampoline = StarlightInterop.hook(Target, Detour, UnityFunction, SpecialReturnBuffer);
    }
    
    public void Dispose()
    {
        if (OriginalTrampoline == IntPtr.Zero)
        {
            return;
        }
        StarlightInterop.unhook(Target);
    }

    public T GenerateTrampoline<T>() where T : Delegate
    {
        return Marshal.GetDelegateForFunctionPointer<T>(OriginalTrampoline);
    }
}
