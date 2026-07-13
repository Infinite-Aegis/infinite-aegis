using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Venicle.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class VenicleComponent : Component
{
    [DataField]
    public string DriverSlotId = "driver-slot";

    [ViewVariables]
    public ContainerSlot DriverSlot = default!;

    [DataField]
    public EntityWhitelist? DriverWhitelist;

    [DataField]
    public float EntryDelay = 3f;

    [DataField]
    public float ExitDelay = 3f;

    [DataField]
    public EntProtoId EjectAction = "ActionVenicleEject";

    [DataField]
    public EntityUid? EjectActionEntity;

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
