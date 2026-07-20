using Robust.Shared.Prototypes;

namespace Content.Server.Garage;

[RegisterComponent]
public sealed partial class GarageComponent : Component
{
    [DataField]
    public EntProtoId ActionPrototype = "ActionOpenGarage";

    [ViewVariables]
    public int ProfileId;

    [ViewVariables]
    public EntityUid? Action;
}
