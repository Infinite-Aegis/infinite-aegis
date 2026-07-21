using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Vehicle;

[Serializable, NetSerializable]
public sealed partial class VehicleRepairDoAfterEvent : SimpleDoAfterEvent
{
}
