using Content.Shared.EntityStore;
using Robust.Shared.Prototypes;

namespace Content.Server.EntityStore;

[RegisterComponent]
public sealed partial class EntityStoreMerchantComponent : Component
{
    [DataField]
    public List<ProtoId<EntityStoreOfferPrototype>> Offers = new();
}
