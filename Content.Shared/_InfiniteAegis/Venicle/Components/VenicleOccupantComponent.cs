using Robust.Shared.GameStates;

namespace Content.Shared.Venicle.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VenicleOccupantComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid Venicle;

    [ViewVariables, AutoNetworkedField]
    public string SeatId = string.Empty;

    [DataField]
    public EntityUid? EjectActionEntity;
}
