using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Venicle;

public sealed partial class VenicleEjectEvent : InstantActionEvent
{
}

public sealed partial class VenicleChangeSeatActionEvent : InstantActionEvent
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

[Serializable, NetSerializable]
public sealed partial class VenicleChangeSeatDoAfterEvent : SimpleDoAfterEvent
{
    [DataField(required: true)]
    public string SeatId = string.Empty;

    private VenicleChangeSeatDoAfterEvent()
    {
    }

    public VenicleChangeSeatDoAfterEvent(string seatId)
    {
        SeatId = seatId;
    }
}

[Serializable, NetSerializable]
public sealed partial class VenicleKickDoAfterEvent : SimpleDoAfterEvent
{
}
