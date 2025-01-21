using System;
using System.Runtime.InteropServices;
using IDetour = Il2CppInterop.Runtime.Injection.IDetour;

namespace BepInEx.Unity.IL2CPP.Hook;

public unsafe class NativeDetour : IDetour
{
    public IntPtr Target { get; }
    public IntPtr Detour { get; }
    public IntPtr OriginalTrampoline { get; private set; }

    public NativeDetour(IntPtr target, Delegate detour)
    {
        Target = target;
        Detour = Marshal.GetFunctionPointerForDelegate(detour);
        Apply();
    }

    public void Apply()
    {
        if (OriginalTrampoline != IntPtr.Zero)
        {
            return;
        }
        OriginalTrampoline = BruthaInterop.hook(Target, Detour);
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
