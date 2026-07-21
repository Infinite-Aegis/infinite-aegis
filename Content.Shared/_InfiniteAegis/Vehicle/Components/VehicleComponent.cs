using System.Numerics;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Vehicle.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class VehicleComponent : Component
{
    [DataField]
    public List<VehicleSeatDefinition> Seats = new();

    [ViewVariables]
    public Dictionary<string, ContainerSlot> SeatContainers = new();

    [ViewVariables]
    public Dictionary<string, EntityUid> SeatMarkers = new();

    [DataField]
    public EntityWhitelist? OccupantWhitelist;

    [DataField]
    public EntProtoId SeatMarkerPrototype = "VehicleSeatMarker";

    [DataField]
    public float EntryDelay = 3f;

    [DataField]
    public float ExitDelay = 3f;

    [DataField]
    public EntProtoId EjectAction = "ActionVehicleEject";

    [DataField]
    public EntProtoId ChangeSeatAction = "ActionVehicleChangeSeat";

    [DataField]
    public float ChangeSeatDelay = 3f;

    [DataField]
    public float KickDelay = 10f;

    [DataField]
    public float MaxSeatInteractionSpeed = 0.15f;

    [DataField]
    public float MaxSeatInteractionAngularSpeed = 0.1f;

    [ViewVariables]
    public HashSet<EntityUid> OccupantsChangingSeats = new();
}

[DataDefinition]
public sealed partial class VehicleSeatDefinition
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
