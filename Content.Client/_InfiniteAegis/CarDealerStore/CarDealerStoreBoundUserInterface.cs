using Content.Shared.CarDealerStore;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._InfiniteAegis.CarDealerStore;

[UsedImplicitly]
public sealed class CarDealerStoreBoundUserInterface : BoundUserInterface
{
    private CarDealerStoreMenu? _menu;

    public CarDealerStoreBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<CarDealerStoreMenu>();
        _menu.OnBuyOffer += offerId => SendMessage(new CarDealerStoreBuyMessage(offerId, Guid.NewGuid()));
        SendMessage(new CarDealerStoreRequestUpdateMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is CarDealerStoreBoundUserInterfaceState storeState)
            _menu?.UpdateState(storeState);
    }
}
