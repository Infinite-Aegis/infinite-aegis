using Content.Shared.Venicle;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._InfiniteAegis.Venicle;

[UsedImplicitly]
public sealed class VenicleBoundUserInterface : BoundUserInterface
{
    private VenicleSeatMenu? _menu;

    public VenicleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<VenicleSeatMenu>();
        _menu.OnChangeSeat += seatId => SendMessage(new VenicleChangeSeatMessage(seatId));
        _menu.OnKick += occupant => SendMessage(new VenicleKickOccupantMessage(occupant));
        SendMessage(new VenicleRequestUpdateMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is VenicleBoundUserInterfaceState venicleState)
            _menu?.UpdateState(venicleState);
    }
}
