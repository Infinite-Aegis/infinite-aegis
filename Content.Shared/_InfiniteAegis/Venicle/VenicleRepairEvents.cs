using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Venicle;

[Serializable, NetSerializable]
public sealed partial class VenicleRepairDoAfterEvent : SimpleDoAfterEvent
{
}
