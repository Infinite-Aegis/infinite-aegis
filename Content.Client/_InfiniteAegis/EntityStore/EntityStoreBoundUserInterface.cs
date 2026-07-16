using Content.Shared.EntityStore;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._InfiniteAegis.EntityStore;

[UsedImplicitly]
public sealed class EntityStoreBoundUserInterface : BoundUserInterface
{
    private EntityStoreMenu? _menu;

    public EntityStoreBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<EntityStoreMenu>();
        _menu.OnBuyOffer += offerId => SendMessage(new EntityStoreBuyMessage(offerId, Guid.NewGuid()));
        SendMessage(new EntityStoreRequestUpdateMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is EntityStoreBoundUserInterfaceState storeState)
            _menu?.UpdateState(storeState);
    }
}
