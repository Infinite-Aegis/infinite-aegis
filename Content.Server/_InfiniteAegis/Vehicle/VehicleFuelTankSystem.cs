using Content.Shared.Vehicle.Components;
using Robust.Shared.Map;

namespace Content.Server.Vehicle;

public sealed partial class VehicleFuelTankSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleFuelTankComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<VehicleFuelTankComponent> tank, ref MapInitEvent args)
    {
        RebuildPortMarker(tank.Owner, tank.Comp);
    }

    public bool RebuildPortMarker(EntityUid uid, VehicleFuelTankComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        var childEnumerator = Transform(uid).ChildEnumerator;
        while (childEnumerator.MoveNext(out var child))
        {
            if (HasComp<VehicleFuelPortComponent>(child))
                QueueDel(child);
        }

        var marker = Spawn(component.PortMarkerPrototype, new EntityCoordinates(uid, component.PortOffset));
        var port = EnsureComp<VehicleFuelPortComponent>(marker);
        port.Vehicle = uid;
        port.MarkerOffset = component.MarkerOffset;
        Dirty(marker, port);

        component.PortMarker = marker;
        return true;
    }
}
