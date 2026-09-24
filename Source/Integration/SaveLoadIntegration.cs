using MonoMod.ModInterop;
using System;
using System.Collections.Generic;

namespace Celeste.Mod.SomeSplitButtons.Integration;

/// <summary>SpeedrunTool's save-state registration, imported by ModInterop.</summary>
// ⚠️ Public, type and fields, and it has to be: MonoMod's ModInterop binds by reflecting over public
// static fields, so making either half internal leaves the delegates null. Nothing throws and
// nothing is logged — the mod loads, and the split timers are simply never disarmed around a
// SpeedrunTool save state. Measured, not assumed.
[ModImportName("SpeedrunTool.SaveLoad")]
public static class SaveLoadIntegration {
    public static Func<Action<Dictionary<Type, Dictionary<string, object>>, Level>,
        Action<Dictionary<Type, Dictionary<string, object>>, Level>, Action,
        Action<Level>, Action<Level>, Action, object> RegisterSaveLoadAction;

    public static Action<object> Unregister;
}
