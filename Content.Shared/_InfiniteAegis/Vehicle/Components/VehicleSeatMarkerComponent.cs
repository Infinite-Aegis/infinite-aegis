using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.Vehicle.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class VehicleSeatMarkerComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid Vehicle;

    [ViewVariables, AutoNetworkedField]
    public string SeatId = string.Empty;

    [ViewVariables, AutoNetworkedField]
    public Vector2 MarkerOffset;

    [ViewVariables]
    public EntityUid? PendingOccupant;

    [ViewVariables, AutoNetworkedField]
    public bool Available = true;
}
