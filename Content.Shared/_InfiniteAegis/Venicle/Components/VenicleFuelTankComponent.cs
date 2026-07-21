using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Venicle.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class VenicleFuelTankComponent : Component
{
    [DataField]
    public string Solution = "fuel";

    [DataField]
    public ProtoId<ReagentPrototype> FuelReagent = "Gasoline";

    [DataField]
    public EntProtoId PortMarkerPrototype = "VenicleFuelPortMarker";

    [DataField(required: true)]
    public Vector2 PortOffset;

    [DataField]
    public Vector2 MarkerOffset;

    [DataField]
    public float FuelConsumptionPerDistance;

    [DataField]
    public float MaximumTrackedSegmentLength = 2f;

    [ViewVariables, AutoNetworkedField]
    public bool HasFuel;

    [ViewVariables]
    public float PendingFuelConsumption;

    [ViewVariables]
    public EntityUid? PortMarker;
}
