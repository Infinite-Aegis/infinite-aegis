using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Vehicle;

public sealed partial class VehicleEjectActionEvent : InstantActionEvent
{
}

public sealed partial class VehicleChangeSeatActionEvent : InstantActionEvent
{
}

[Serializable, NetSerializable]
public sealed partial class VehicleEntryDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class VehicleExitDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class VehicleChangeSeatDoAfterEvent : SimpleDoAfterEvent
{
    [DataField(required: true)]
    public string SeatId = string.Empty;

    private VehicleChangeSeatDoAfterEvent()
    {
    }

    public VehicleChangeSeatDoAfterEvent(string seatId)
    {
        SeatId = seatId;
    }
}

[Serializable, NetSerializable]
public sealed partial class VehicleKickDoAfterEvent : SimpleDoAfterEvent
{
}
