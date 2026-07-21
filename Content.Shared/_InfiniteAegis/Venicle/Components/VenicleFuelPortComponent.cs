using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.Venicle.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class VenicleFuelPortComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid Venicle;

    [ViewVariables, AutoNetworkedField]
    public Vector2 MarkerOffset;
}
