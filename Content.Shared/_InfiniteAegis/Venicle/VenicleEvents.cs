using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Venicle;

public sealed partial class VenicleEjectEvent : InstantActionEvent
{
}

[Serializable, NetSerializable]
public sealed partial class VenicleEntryEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class VenicleExitEvent : SimpleDoAfterEvent
{
}
