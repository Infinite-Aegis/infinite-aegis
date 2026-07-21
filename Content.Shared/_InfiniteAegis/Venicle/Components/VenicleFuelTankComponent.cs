using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Venicle.Components;

[RegisterComponent, NetworkedComponent]
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

    [ViewVariables]
    public EntityUid? PortMarker;
}
