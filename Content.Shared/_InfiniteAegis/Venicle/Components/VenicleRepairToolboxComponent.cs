using Robust.Shared.GameStates;

namespace Content.Shared.Venicle.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class VenicleRepairToolboxComponent : Component
{
    [DataField]
    public float RepairDuration = 10f;

    [DataField]
    public int ChargeCost = 20;
}
