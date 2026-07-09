using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.Venicle.Components;

[RegisterComponent]
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
}
