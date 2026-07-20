using System.Numerics;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Venicle.Components;

/// <summary>
/// Seat definitions, passenger interaction configuration, and server-side seat runtime state.
/// Vehicle physics belongs to <see cref="VenicleMovementComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
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
    public EntProtoId ChangeSeatAction = "ActionVenicleChangeSeat";

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
