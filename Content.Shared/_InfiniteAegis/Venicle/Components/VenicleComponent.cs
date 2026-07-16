using System.Numerics;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Venicle.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class VenicleComponent : Component
{
    [DataField]
    public List<VenicleSeatDefinition> Seats = new();

    [ViewVariables]
    public Dictionary<string, ContainerSlot> SeatContainers = new();

    [ViewVariables]
    public Dictionary<string, EntityUid> SeatMarkers = new();

    [DataField]
    public EntityWhitelist? OccupantWhitelist;

    [DataField]
    public EntProtoId SeatMarkerPrototype = "VenicleSeatMarker";

    [DataField]
    public float EntryDelay = 3f;

    [DataField]
    public float ExitDelay = 3f;

    [DataField]
    public EntProtoId EjectAction = "ActionVenicleEject";

    [DataField]
    public float MaxSeatInteractionSpeed = 0.15f;

    [DataField]
    public float MaxSeatInteractionAngularSpeed = 0.1f;

    [DataField]
    public float MaxForwardSpeed = 8f;

    [DataField]
    public float MaxReverseSpeed = 3f;

    [DataField]
    public float ForwardEngineForce = 6000f;

    [DataField]
    public float ReverseEngineForce = 3500f;

    [DataField]
    public float BrakeForce = 14000f;

    [DataField]
    public float RollingResistance = 250f;

    [DataField]
    public float AerodynamicDrag = 12f;

    [DataField]
    public float FrontCorneringStiffness = 12000f;

    [DataField]
    public float RearCorneringStiffness = 11000f;

    [DataField]
    public float MaxLateralGrip = 14000f;

    [DataField]
    public Angle MaxSteeringAngle = Angle.FromDegrees(35);

    [DataField]
    public float SteeringRate = 4f;

    [DataField]
    public float SteeringReturnRate = 4f;

    [DataField]
    public float WheelBase = 2.2f;

    [DataField]
    public float AngularResistance = 1500f;

    [DataField]
    public float SteeringAngularResistanceModifier = 0.25f;

    [DataField]
    public float TileFrictionModifier = 0.05f;

    [ViewVariables, AutoNetworkedField]
    public float CurrentSteering;
}

[DataDefinition]
public sealed partial class VenicleSeatDefinition
{
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField]
    public bool Driver;

    [DataField(required: true)]
    public Vector2 OccupantOffset;

    [DataField(required: true)]
    public Vector2 ExitOffset;
}
