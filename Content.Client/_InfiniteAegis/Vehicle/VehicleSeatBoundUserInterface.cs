using Content.Shared.Vehicle;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._InfiniteAegis.Vehicle;

[UsedImplicitly]
public sealed class VehicleSeatBoundUserInterface : BoundUserInterface
{
    private VehicleSeatMenu? _menu;

    public VehicleSeatBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<VehicleSeatMenu>();
        _menu.OnChangeSeat += seatId => SendMessage(new VehicleChangeSeatMessage(seatId));
        _menu.OnKick += occupant => SendMessage(new VehicleKickOccupantMessage(occupant));
        SendMessage(new VehicleSeatRequestUpdateMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is VehicleSeatBoundUserInterfaceState vehicleState)
            _menu?.UpdateState(vehicleState);
    }
}
