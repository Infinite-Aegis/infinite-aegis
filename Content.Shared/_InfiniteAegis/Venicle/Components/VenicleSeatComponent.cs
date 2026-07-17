using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.Venicle.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class VenicleSeatComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid Venicle;

    [ViewVariables, AutoNetworkedField]
    public string SeatId = string.Empty;

    [ViewVariables, AutoNetworkedField]
    public Vector2 MarkerOffset;

    [ViewVariables]
    public EntityUid? PendingOccupant;

    [ViewVariables, AutoNetworkedField]
    public bool Available = true;
}
