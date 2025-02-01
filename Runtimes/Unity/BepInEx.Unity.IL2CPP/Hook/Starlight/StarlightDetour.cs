using System;

namespace BepInEx.Unity.IL2CPP.Hook.Starlight;

internal class StarlightDetour : BaseNativeDetour<StarlightDetour>
{
    public StarlightDetour(IntPtr originalMethodPtr, Delegate detourMethod) : base(originalMethodPtr, detourMethod)
    {
    }

    protected override void ApplyImpl() { }

    protected override void PrepareImpl()
    {
        TrampolinePtr = StarlightInterop.hook(OriginalMethodPtr, DetourMethodPtr);
    }

    protected override void UndoImpl()
    {
        StarlightInterop.unhook(OriginalMethodPtr);
    }

    protected override void FreeImpl() { }
}
