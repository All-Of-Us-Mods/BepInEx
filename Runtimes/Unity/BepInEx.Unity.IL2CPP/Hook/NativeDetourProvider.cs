using System;
using Il2CppInterop.Runtime.Injection;

namespace BepInEx.Unity.IL2CPP.Hook;

public class NativeDetourProvider : IDetourProvider
{
    public IDetour Create<TDelegate>(IntPtr original, TDelegate target, bool unityFunction) where TDelegate : Delegate
    {
        var detour = new NativeDetour(original, target, unityFunction);
        return new CacheDetourWrapper(detour, target);
    }
}
