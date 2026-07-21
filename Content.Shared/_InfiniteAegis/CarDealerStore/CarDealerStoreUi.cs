using Robust.Shared.Serialization;

namespace Content.Shared.CarDealerStore;

[Serializable, NetSerializable]
public enum CarDealerStoreUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CarDealerStoreOfferData
{
    public readonly string Id;
    public readonly string ProductEntity;
    public readonly string? DescriptionLoc;
    public readonly int Price;

    public CarDealerStoreOfferData(string id, string productEntity, string? descriptionLoc, int price)
    {
        Id = id;
        ProductEntity = productEntity;
        DescriptionLoc = descriptionLoc;
        Price = price;
    }
}

[Serializable, NetSerializable]
public sealed class CarDealerStoreBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly long Balance;
    public readonly List<CarDealerStoreOfferData> Offers;

    public CarDealerStoreBoundUserInterfaceState(long balance, List<CarDealerStoreOfferData> offers)
    {
        Balance = balance;
        Offers = offers;
    }
}

[Serializable, NetSerializable]
public sealed class CarDealerStoreRequestUpdateMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CarDealerStoreBuyMessage : BoundUserInterfaceMessage
{
    public readonly string OfferId;
    public readonly Guid RequestId;

    public CarDealerStoreBuyMessage(string offerId, Guid requestId)
    {
        OfferId = offerId;
        RequestId = requestId;
    }
}
