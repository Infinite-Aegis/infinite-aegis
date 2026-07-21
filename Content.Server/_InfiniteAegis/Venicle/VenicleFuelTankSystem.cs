using Content.Shared.Venicle.Components;
using Robust.Shared.Map;

namespace Content.Server.Venicle;

public sealed partial class VenicleFuelTankSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VenicleFuelTankComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<VenicleFuelTankComponent> tank, ref MapInitEvent args)
    {
        if (tank.Comp.PortMarker is { } existing && Exists(existing))
            return;

        var marker = Spawn(tank.Comp.PortMarkerPrototype, new EntityCoordinates(tank.Owner, tank.Comp.PortOffset));
        var port = EnsureComp<VenicleFuelPortComponent>(marker);
        port.Venicle = tank.Owner;
        port.MarkerOffset = tank.Comp.MarkerOffset;
        Dirty(marker, port);

        tank.Comp.PortMarker = marker;
    }
}
