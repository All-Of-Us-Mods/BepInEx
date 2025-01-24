using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.Injection;

namespace BepInEx.Unity.IL2CPP.Hook;

// Workaround for CoreCLR collecting all delegates
internal class CacheDetourWrapper : IDetour
{
    public IntPtr Target => wrapped.Target;
    public IntPtr Detour => wrapped.Detour;
    public IntPtr OriginalTrampoline => wrapped.OriginalTrampoline;

    private readonly IDetour wrapped;

    private readonly List<object> cache = [];

    public CacheDetourWrapper(IDetour wrapped, Delegate target)
    {
        this.wrapped = wrapped;
        cache.Add(target);
    }

    public void Apply()
    {
        wrapped.Apply();
    }

    public void Dispose()
    {
        wrapped.Dispose();
        cache.Clear();
    }

    public T GenerateTrampoline<T>() where T : Delegate
    {
        var trampoline = wrapped.GenerateTrampoline<T>();
        cache.Add(trampoline);
        return trampoline;
    }
}
