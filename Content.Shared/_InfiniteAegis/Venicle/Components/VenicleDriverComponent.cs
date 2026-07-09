using Robust.Shared.GameStates;

namespace Content.Shared.Venicle.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VenicleDriverComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public EntityUid Venicle;
}
