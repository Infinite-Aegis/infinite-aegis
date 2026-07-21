using Robust.Shared.GameStates;

namespace Content.Shared.Vehicle.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleOccupantComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid Vehicle;

    [ViewVariables, AutoNetworkedField]
    public string SeatId = string.Empty;

    [DataField]
    public EntityUid? EjectActionEntity;

    [DataField]
    public EntityUid? ChangeSeatActionEntity;
}
