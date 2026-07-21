using Robust.Shared.GameStates;

namespace Content.Shared.Vehicle.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class VehicleRepairToolboxComponent : Component
{
    [DataField]
    public float RepairDuration = 10f;

    [DataField]
    public int ChargeCost = 20;
}
