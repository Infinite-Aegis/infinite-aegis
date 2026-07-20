using Content.Shared.Garage;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._InfiniteAegis.Garage;

[UsedImplicitly]
public sealed class GarageBoundUserInterface : BoundUserInterface
{
    private GarageMenu? _menu;

    public GarageBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<GarageMenu>();
        _menu.OnCall += vehicleId => SendMessage(new GarageSpawnMessage(vehicleId));
        SendMessage(new GarageRequestUpdateMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is GarageBoundUserInterfaceState garageState)
            _menu?.UpdateState(garageState);
    }
}
