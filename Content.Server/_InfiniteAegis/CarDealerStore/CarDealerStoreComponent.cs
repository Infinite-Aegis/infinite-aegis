using Content.Shared.CarDealerStore;
using Robust.Shared.Prototypes;

namespace Content.Server.CarDealerStore;

[RegisterComponent]
public sealed partial class CarDealerStoreComponent : Component
{
    [DataField]
    public List<ProtoId<CarDealerStoreOfferPrototype>> Offers = new();
}
