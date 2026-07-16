using Robust.Shared.Serialization;

namespace Content.Shared.EntityStore;

[Serializable, NetSerializable]
public enum EntityStoreUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class EntityStoreOfferData
{
    public readonly string Id;
    public readonly string ProductEntity;
    public readonly string? DescriptionLoc;
    public readonly int Price;
    public readonly bool Owned;

    public EntityStoreOfferData(string id, string productEntity, string? descriptionLoc, int price, bool owned)
    {
        Id = id;
        ProductEntity = productEntity;
        DescriptionLoc = descriptionLoc;
        Price = price;
        Owned = owned;
    }
}

[Serializable, NetSerializable]
public sealed class EntityStoreBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly long Balance;
    public readonly List<EntityStoreOfferData> Offers;

    public EntityStoreBoundUserInterfaceState(long balance, List<EntityStoreOfferData> offers)
    {
        Balance = balance;
        Offers = offers;
    }
}

[Serializable, NetSerializable]
public sealed class EntityStoreRequestUpdateMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class EntityStoreBuyMessage : BoundUserInterfaceMessage
{
    public readonly string OfferId;
    public readonly Guid RequestId;

    public EntityStoreBuyMessage(string offerId, Guid requestId)
    {
        OfferId = offerId;
        RequestId = requestId;
    }
}
