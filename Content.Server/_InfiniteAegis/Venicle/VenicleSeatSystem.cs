using Content.Shared.Access.Components;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Venicle;
using Content.Shared.Venicle.Components;
using Content.Shared.Venicle.Systems;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server.Venicle;

public sealed partial class VenicleSeatSystem : SharedVenicleSeatSystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VenicleComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<VenicleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<VenicleComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VenicleComponent, VenicleEjectEvent>(OnEjectAction);
        SubscribeLocalEvent<VenicleComponent, VenicleExitEvent>(OnExit);
        SubscribeLocalEvent<VenicleComponent, DoAfterAttemptEvent<VenicleExitEvent>>(OnExitAttempt);
        SubscribeLocalEvent<VenicleComponent, EntityStorageIntoContainerAttemptEvent>(OnEntityStorageDump);
        SubscribeLocalEvent<VenicleComponent, GetAdditionalAccessEvent>(OnGetAdditionalAccess);
        SubscribeLocalEvent<VenicleComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
        SubscribeLocalEvent<VenicleSeatComponent, InteractHandEvent>(OnSeatInteractHand);
        SubscribeLocalEvent<VenicleSeatComponent, DragDropTargetEvent>(OnSeatDragDrop);
        SubscribeLocalEvent<VenicleSeatComponent, VenicleEntryEvent>(OnEntry);
        SubscribeLocalEvent<VenicleSeatComponent, DoAfterAttemptEvent<VenicleEntryEvent>>(OnEntryAttempt);
    }

    private void OnInit(EntityUid uid, VenicleComponent component, ComponentInit args)
    {
        foreach (var seat in component.Seats)
        {
            if (component.SeatContainers.ContainsKey(seat.Id))
                continue;

            component.SeatContainers.Add(seat.Id, _container.EnsureContainer<ContainerSlot>(uid, GetContainerId(seat.Id)));
        }
    }

    private void OnMapInit(EntityUid uid, VenicleComponent component, MapInitEvent args)
    {
        foreach (var seat in component.Seats)
        {
            if (component.SeatMarkers.TryGetValue(seat.Id, out var existing) && Exists(existing))
                continue;

            var marker = Spawn(component.SeatMarkerPrototype, new EntityCoordinates(uid, seat.ExitOffset));
            var markerComponent = EnsureComp<VenicleSeatComponent>(marker);
            markerComponent.Venicle = uid;
            markerComponent.SeatId = seat.Id;
            markerComponent.MarkerOffset = seat.OccupantOffset - seat.ExitOffset;
            markerComponent.Available = component.SeatContainers.TryGetValue(seat.Id, out var container) &&
                                        container.ContainedEntity == null;
            Dirty(marker, markerComponent);
            component.SeatMarkers[seat.Id] = marker;
        }
    }

    private void OnShutdown(EntityUid uid, VenicleComponent component, ComponentShutdown args)
    {
        foreach (var container in component.SeatContainers.Values)
        {
            if (container.ContainedEntity is { } occupant)
                TryEject(occupant, true);
        }
    }

    private void OnEjectAction(EntityUid uid, VenicleComponent component, VenicleEjectEvent args)
    {
        if (args.Handled ||
            !TryComp(args.Performer, out VenicleOccupantComponent? occupant) ||
            occupant.Venicle != uid)
            return;

        args.Handled = true;

        if (IsMoving(uid, component))
        {
            _popup.PopupEntity(Loc.GetString("venicle-moving"), args.Performer);
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, args.Performer, component.ExitDelay, new VenicleExitEvent(), uid)
        {
            AttemptFrequency = AttemptFrequency.EveryTick,
            DistanceThreshold = null,
            RequireCanInteract = false,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnSeatInteractHand(EntityUid uid, VenicleSeatComponent component, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        BeginEntry((uid, component), args.User, args.User, true);
    }

    private void OnSeatDragDrop(Entity<VenicleSeatComponent> entity, ref DragDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = BeginEntry(entity, args.User, args.Dragged, false);
    }

    private bool BeginEntry(
        Entity<VenicleSeatComponent> marker,
        EntityUid actor,
        EntityUid occupant,
        bool popupFailures)
    {
        if (!TryComp(marker.Comp.Venicle, out VenicleComponent? venicle))
            return false;

        if (IsMoving(marker.Comp.Venicle, venicle))
        {
            if (popupFailures)
                _popup.PopupEntity(Loc.GetString("venicle-moving"), actor, actor);
            return false;
        }

        if (marker.Comp.PendingOccupant != null ||
            !venicle.SeatContainers.TryGetValue(marker.Comp.SeatId, out var container) ||
            container.ContainedEntity != null)
        {
            if (popupFailures)
                _popup.PopupEntity(Loc.GetString("venicle-seat-occupied"), actor, actor);
            return false;
        }

        if (!CanInsert(marker, actor, occupant, venicle))
        {
            if (popupFailures)
                _popup.PopupEntity(Loc.GetString("venicle-no-enter"), actor, actor);
            return false;
        }

        marker.Comp.PendingOccupant = occupant;
        SetSeatAvailable(marker, false);

        var doAfterArgs = new DoAfterArgs(EntityManager, actor, venicle.EntryDelay, new VenicleEntryEvent(), marker, target: occupant, used: marker)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
            DuplicateCondition = DuplicateConditions.None,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
        {
            ReleaseReservation(marker, venicle);
            return false;
        }

        if (actor != occupant)
        {
            _popup.PopupEntity(
                Loc.GetString("venicle-being-seated"),
                occupant,
                occupant,
                PopupType.MediumCaution);
        }

        return true;
    }

    private void OnEntry(Entity<VenicleSeatComponent> marker, ref VenicleEntryEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!TryComp(marker.Comp.Venicle, out VenicleComponent? venicle) ||
            args.Target is not { } occupant ||
            marker.Comp.PendingOccupant != occupant)
            return;

        if (args.Cancelled)
        {
            ReleaseReservation(marker, venicle);
            return;
        }

        if (!TryInsert(marker, args.User, occupant, venicle, true))
        {
            ReleaseReservation(marker, venicle);
            _popup.PopupEntity(Loc.GetString("venicle-no-enter"), args.User, args.User);
            return;
        }

        marker.Comp.PendingOccupant = null;
        SetSeatAvailable(marker, false);
    }

    private void OnEntryAttempt(Entity<VenicleSeatComponent> marker, ref DoAfterAttemptEvent<VenicleEntryEvent> args)
    {
        if (!TryComp(marker.Comp.Venicle, out VenicleComponent? venicle) ||
            args.DoAfter.Args.Target is not { } occupant ||
            marker.Comp.PendingOccupant != occupant ||
            !CanInsert(marker, args.DoAfter.Args.User, occupant, venicle, true))
        {
            args.Cancel();
        }
    }

    private void OnExit(EntityUid uid, VenicleComponent component, VenicleExitEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = TryEject(args.User);
    }

    private void OnExitAttempt(EntityUid uid, VenicleComponent component, DoAfterAttemptEvent<VenicleExitEvent> args)
    {
        if (!CanEject(args.DoAfter.Args.User, uid, component))
            args.Cancel();
    }

    private void OnEntityStorageDump(EntityUid uid, VenicleComponent component, ref EntityStorageIntoContainerAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnGetAdditionalAccess(EntityUid uid, VenicleComponent component, ref GetAdditionalAccessEvent args)
    {
        foreach (var seat in component.Seats)
        {
            if (!seat.Driver ||
                !component.SeatContainers.TryGetValue(seat.Id, out var container) ||
                container.ContainedEntity is not { } driver)
            {
                continue;
            }

            args.Entities.Add(driver);
            return;
        }
    }

    private void OnEntRemoved(EntityUid uid, VenicleComponent component, EntRemovedFromContainerMessage args)
    {
        if (!TryComp(args.Entity, out VenicleOccupantComponent? occupant) || occupant.Venicle != uid)
            return;

        if (!component.SeatContainers.TryGetValue(occupant.SeatId, out var container) || args.Container != container)
            return;

        PlaceAtExit(uid, args.Entity, occupant.SeatId, component);
        CleanupOccupant(uid, args.Entity);

        if (component.SeatMarkers.TryGetValue(occupant.SeatId, out var marker) &&
            TryComp(marker, out VenicleSeatComponent? seat))
        {
            seat.PendingOccupant = null;
            SetSeatAvailable((marker, seat), true);
        }
    }

    public bool CanInsert(
        Entity<VenicleSeatComponent> marker,
        EntityUid actor,
        EntityUid occupant,
        VenicleComponent? component = null,
        bool allowReservation = false)
    {
        if (!Resolve(marker.Comp.Venicle, ref component) ||
            !component.SeatContainers.TryGetValue(marker.Comp.SeatId, out var container))
            return false;

        return container.ContainedEntity == null &&
               CanUseSeat(marker, actor, occupant, allowReservation);
    }

    public bool TryInsert(
        Entity<VenicleSeatComponent> marker,
        EntityUid actor,
        EntityUid occupant,
        VenicleComponent? component = null,
        bool allowReservation = false)
    {
        if (!Resolve(marker.Comp.Venicle, ref component))
            return false;

        if (!CanInsert(marker, actor, occupant, component, allowReservation) ||
            !component.SeatContainers.TryGetValue(marker.Comp.SeatId, out var container))
            return false;

        if (!_container.Insert(occupant, container))
            return false;

        SetupOccupant(marker.Comp.Venicle, occupant, marker.Comp.SeatId, component);
        return true;
    }

    public bool TryEject(EntityUid occupantUid, bool force = false)
    {
        if (!TryComp(occupantUid, out VenicleOccupantComponent? occupant) ||
            !TryComp(occupant.Venicle, out VenicleComponent? component) ||
            !CanEject(occupantUid, occupant.Venicle, component, force) ||
            !component.SeatContainers.TryGetValue(occupant.SeatId, out var container))
            return false;

        var exitCoordinates = GetExitCoordinates(occupant.Venicle, occupant.SeatId, component);

        if (!_container.RemoveEntity(occupant.Venicle, occupantUid))
            return false;

        if (exitCoordinates != null)
            _transform.SetCoordinates(occupantUid, exitCoordinates.Value);

        CleanupOccupant(occupant.Venicle, occupantUid);
        return true;
    }

    private void SetupOccupant(EntityUid venicle, EntityUid occupantUid, string seatId, VenicleComponent component)
    {
        var occupant = EnsureComp<VenicleOccupantComponent>(occupantUid);
        occupant.Venicle = venicle;
        occupant.SeatId = seatId;
        Dirty(occupantUid, occupant);

        if (TryGetSeat(component, seatId, out var seat) && seat.Driver)
            _mover.SetRelay(occupantUid, venicle);

        _actions.AddAction(occupantUid, ref occupant.EjectActionEntity, component.EjectAction, venicle);
    }

    private void CleanupOccupant(EntityUid venicle, EntityUid occupantUid)
    {
        if (TryComp<VenicleOccupantComponent>(occupantUid, out var occupant) && occupant.Venicle == venicle)
            RemComp<VenicleOccupantComponent>(occupantUid);

        if (TryComp<RelayInputMoverComponent>(occupantUid, out var relay) && relay.RelayEntity == venicle)
            RemComp<RelayInputMoverComponent>(occupantUid);

        _actions.RemoveProvidedActions(occupantUid, venicle);
    }

    private bool CanEject(EntityUid occupantUid, EntityUid venicleUid, VenicleComponent component, bool force = false)
    {
        return TryComp(occupantUid, out VenicleOccupantComponent? occupant)
               && occupant.Venicle == venicleUid
               && component.SeatContainers.TryGetValue(occupant.SeatId, out var container)
               && container.ContainedEntity == occupantUid
               && (force || !IsMoving(venicleUid, component));
    }

    private void ReleaseReservation(Entity<VenicleSeatComponent> marker, VenicleComponent component)
    {
        marker.Comp.PendingOccupant = null;
        var available = component.SeatContainers.TryGetValue(marker.Comp.SeatId, out var container) &&
                        container.ContainedEntity == null;
        SetSeatAvailable(marker, available);
    }

    private void SetSeatAvailable(Entity<VenicleSeatComponent> marker, bool available)
    {
        if (marker.Comp.Available == available)
            return;

        marker.Comp.Available = available;
        Dirty(marker);
    }

    private void PlaceAtExit(EntityUid venicleUid, EntityUid occupantUid, string seatId, VenicleComponent component)
    {
        if (GetExitCoordinates(venicleUid, seatId, component) is { } coordinates)
            _transform.SetCoordinates(occupantUid, coordinates);
    }

    private EntityCoordinates? GetExitCoordinates(EntityUid venicleUid, string seatId, VenicleComponent component)
    {
        if (Terminating(venicleUid) || !TryGetSeat(component, seatId, out var seat))
            return null;

        var venicleTransform = Transform(venicleUid);
        if (venicleTransform.MapID == MapId.Nullspace || !Exists(venicleTransform.ParentUid))
            return null;

        var mapCoordinates = _transform.ToMapCoordinates(new EntityCoordinates(venicleUid, seat.ExitOffset));
        return _transform.ToCoordinates(venicleTransform.ParentUid, mapCoordinates);
    }

    private static bool TryGetSeat(VenicleComponent component, string seatId, out VenicleSeatDefinition seat)
    {
        foreach (var candidate in component.Seats)
        {
            if (candidate.Id != seatId)
                continue;

            seat = candidate;
            return true;
        }

        seat = default!;
        return false;
    }

    private static string GetContainerId(string seatId)
    {
        return $"venicle-seat-{seatId}";
    }
}
