using Robust.Shared.Prototypes;

namespace Content.Shared.CarDealerStore;

[Prototype]
public sealed partial class CarDealerStoreOfferPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField(required: true)]
    public EntProtoId Product { get; private set; } = string.Empty;

    [DataField]
    public LocId? Description { get; private set; }

    [DataField]
    public int Price { get; private set; }
}
