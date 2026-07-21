using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.Vehicle.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class VehicleFuelPortComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid Vehicle;

    [ViewVariables, AutoNetworkedField]
    public Vector2 MarkerOffset;
}
