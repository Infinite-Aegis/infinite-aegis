using Content.Shared.Access.Components;
using Content.Shared.Actions;
using Content.Shared.Cuffs.Components;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Hands.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.UserInterface;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server.Vehicle;

public sealed partial class VehicleSeatSystem : SharedVehicleSeatSystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<VehicleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<VehicleComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VehicleComponent, VehicleEjectActionEvent>(OnEjectAction);
        SubscribeLocalEvent<VehicleComponent, VehicleChangeSeatActionEvent>(OnChangeSeatAction);
        SubscribeLocalEvent<VehicleComponent, VehicleExitDoAfterEvent>(OnExit);
        SubscribeLocalEvent<VehicleComponent, DoAfterAttemptEvent<VehicleExitDoAfterEvent>>(OnExitAttempt);
        SubscribeLocalEvent<VehicleComponent, VehicleChangeSeatDoAfterEvent>(OnChangeSeat);
        SubscribeLocalEvent<VehicleComponent, DoAfterAttemptEvent<VehicleChangeSeatDoAfterEvent>>(OnChangeSeatAttempt);
        SubscribeLocalEvent<VehicleComponent, VehicleKickDoAfterEvent>(OnKick);
        SubscribeLocalEvent<VehicleComponent, DoAfterAttemptEvent<VehicleKickDoAfterEvent>>(OnKickAttempt);
        SubscribeLocalEvent<VehicleComponent, VehicleSeatRequestUpdateMessage>(OnRequestUpdate);
        SubscribeLocalEvent<VehicleComponent, VehicleChangeSeatMessage>(OnChangeSeatRequested);
        SubscribeLocalEvent<VehicleComponent, VehicleKickOccupantMessage>(OnKickRequested);
        SubscribeLocalEvent<VehicleComponent, EntityStorageIntoContainerAttemptEvent>(OnEntityStorageDump);
        SubscribeLocalEvent<VehicleComponent, GetAdditionalAccessEvent>(OnGetAdditionalAccess);
        SubscribeLocalEvent<VehicleComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
        SubscribeLocalEvent<VehicleSeatMarkerComponent, InteractHandEvent>(OnSeatInteractHand);
        SubscribeLocalEvent<VehicleSeatMarkerComponent, DragDropTargetEvent>(OnSeatDragDrop);
        SubscribeLocalEvent<VehicleSeatMarkerComponent, VehicleEntryDoAfterEvent>(OnEntry);
        SubscribeLocalEvent<VehicleSeatMarkerComponent, DoAfterAttemptEvent<VehicleEntryDoAfterEvent>>(OnEntryAttempt);
        SubscribeLocalEvent<VehicleOccupantComponent, IdentityChangedEvent>(OnOccupantIdentityChanged);
    }

    private void OnInit(EntityUid uid, VehicleComponent component, ComponentInit args)
    {
        foreach (var seat in component.Seats)
        {
            if (component.SeatContainers.TryGetValue(seat.Id, out var existing))
            {
                existing.ShowContents = false;
                existing.OccludesLight = true;
                continue;
            }

            var container = _container.EnsureContainer<ContainerSlot>(uid, GetContainerId(seat.Id));
            container.ShowContents = false;
            container.OccludesLight = true;
            component.SeatContainers.Add(seat.Id, container);
        }

        Dirty(uid, Comp<ContainerManagerComponent>(uid));
    }

    private void OnMapInit(EntityUid uid, VehicleComponent component, MapInitEvent args)
    {
        RebuildSeatMarkers(uid, component);
    }

    public bool RebuildSeatMarkers(EntityUid uid, VehicleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        var childEnumerator = Transform(uid).ChildEnumerator;
        while (childEnumerator.MoveNext(out var child))
        {
            if (HasComp<VehicleSeatMarkerComponent>(child))
                QueueDel(child);
        }

        component.SeatMarkers.Clear();
        foreach (var seat in component.Seats)
        {
            var marker = Spawn(component.SeatMarkerPrototype, new EntityCoordinates(uid, seat.ExitOffset));
            var markerComponent = EnsureComp<VehicleSeatMarkerComponent>(marker);
            markerComponent.Vehicle = uid;
            markerComponent.SeatId = seat.Id;
            markerComponent.MarkerOffset = seat.OccupantOffset - seat.ExitOffset;
            markerComponent.Available = component.SeatContainers.TryGetValue(seat.Id, out var container) &&
                                        container.ContainedEntity == null;
            Dirty(marker, markerComponent);
            component.SeatMarkers[seat.Id] = marker;
        }

        return true;
    }

    private void OnShutdown(EntityUid uid, VehicleComponent component, ComponentShutdown args)
    {
        _ui.CloseUi(uid, VehicleUiKey.Seats);

        foreach (var container in component.SeatContainers.Values)
        {
            if (container.ContainedEntity is { } occupant)
                TryEject(occupant, true);
        }
    }

    private void OnEjectAction(EntityUid uid, VehicleComponent component, VehicleEjectActionEvent args)
    {
        if (args.Handled ||
            !TryComp(args.Performer, out VehicleOccupantComponent? occupant) ||
            occupant.Vehicle != uid)
            return;

        args.Handled = true;

        if (IsMoving(uid, component))
        {
            _popup.PopupEntity(Loc.GetString("vehicle-moving"), args.Performer);
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, args.Performer, component.ExitDelay, new VehicleExitDoAfterEvent(), uid)
        {
            AttemptFrequency = AttemptFrequency.EveryTick,
            DistanceThreshold = null,
            RequireCanInteract = false,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnChangeSeatAction(EntityUid uid, VehicleComponent component, VehicleChangeSeatActionEvent args)
    {
        if (args.Handled || !CanManipulateSeats(args.Performer, uid, component))
            return;

        args.Handled = true;
        UpdateUi(uid, component);
        _ui.OpenUi(uid, VehicleUiKey.Seats, args.Performer);
    }

    private void OnRequestUpdate(Entity<VehicleComponent> vehicle, ref VehicleSeatRequestUpdateMessage args)
    {
        if (!CanManipulateSeats(args.Actor, vehicle.Owner, vehicle.Comp))
        {
            _ui.CloseUi(vehicle.Owner, VehicleUiKey.Seats, args.Actor);
            return;
        }

        UpdateUi(vehicle);
    }

    private void OnChangeSeatRequested(Entity<VehicleComponent> vehicle, ref VehicleChangeSeatMessage args)
    {
        BeginChangeSeat(vehicle, args.Actor, args.SeatId);
    }

    private void OnKickRequested(Entity<VehicleComponent> vehicle, ref VehicleKickOccupantMessage args)
    {
        var target = GetEntity(args.Occupant);
        if (!CanKick(args.Actor, target, vehicle.Owner, vehicle.Comp))
        {
            _popup.PopupEntity(Loc.GetString("vehicle-cannot-kick"), args.Actor, args.Actor);
            return;
        }

        _popup.PopupEntity(
            Loc.GetString("vehicle-kick-start-self", ("target", Identity.Entity(target, EntityManager, args.Actor))),
            args.Actor,
            args.Actor,
            PopupType.MediumCaution);
        _popup.PopupEntity(
            Loc.GetString("vehicle-kick-start-target", ("user", Identity.Entity(args.Actor, EntityManager, target))),
            target,
            target,
            PopupType.MediumCaution);

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            args.Actor,
            vehicle.Comp.KickDelay,
            new VehicleKickDoAfterEvent(),
            vehicle.Owner,
            target: target)
        {
            AttemptFrequency = AttemptFrequency.EveryTick,
            DistanceThreshold = null,
            RequireCanInteract = false,
            DuplicateCondition = DuplicateConditions.None,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void BeginChangeSeat(Entity<VehicleComponent> vehicle, EntityUid actor, string seatId)
    {
        if (!CanChangeSeat(actor, vehicle.Owner, seatId, vehicle.Comp) ||
            !vehicle.Comp.SeatMarkers.TryGetValue(seatId, out var markerUid) ||
            !TryComp(markerUid, out VehicleSeatMarkerComponent? marker))
        {
            _popup.PopupEntity(Loc.GetString("vehicle-cannot-change-seat"), actor, actor);
            return;
        }

        marker.PendingOccupant = actor;
        SetSeatAvailable((markerUid, marker), false);

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            actor,
            vehicle.Comp.ChangeSeatDelay,
            new VehicleChangeSeatDoAfterEvent(seatId),
            vehicle.Owner)
        {
            AttemptFrequency = AttemptFrequency.EveryTick,
            DistanceThreshold = null,
            RequireCanInteract = false,
            DuplicateCondition = DuplicateConditions.None,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            ReleaseReservation((markerUid, marker), vehicle.Comp);

        UpdateUi(vehicle);
    }

    private void OnChangeSeat(Entity<VehicleComponent> vehicle, ref VehicleChangeSeatDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!vehicle.Comp.SeatMarkers.TryGetValue(args.SeatId, out var markerUid) ||
            !TryComp(markerUid, out VehicleSeatMarkerComponent? marker))
            return;

        if (args.Cancelled || !CanChangeSeat(args.User, vehicle.Owner, args.SeatId, vehicle.Comp, true))
        {
            ReleaseReservation((markerUid, marker), vehicle.Comp);
            UpdateUi(vehicle);
            return;
        }

        if (!TryChangeSeat(vehicle, args.User, args.SeatId))
        {
            ReleaseReservation((markerUid, marker), vehicle.Comp);
            _popup.PopupEntity(Loc.GetString("vehicle-cannot-change-seat"), args.User, args.User);
        }

        UpdateUi(vehicle);
    }

    private void OnChangeSeatAttempt(
        Entity<VehicleComponent> vehicle,
        ref DoAfterAttemptEvent<VehicleChangeSeatDoAfterEvent> args)
    {
        if (!CanChangeSeat(args.DoAfter.Args.User, vehicle.Owner, args.Event.SeatId, vehicle.Comp, true))
            args.Cancel();
    }

    private void OnKick(Entity<VehicleComponent> vehicle, ref VehicleKickDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        if (!CanKick(args.User, target, vehicle.Owner, vehicle.Comp))
            return;

        // Resolve the target from the current seat rather than the seat shown when the do-after started.
        foreach (var container in vehicle.Comp.SeatContainers.Values)
        {
            if (container.ContainedEntity != target)
                continue;

            TryEject(target);
            return;
        }
    }

    private void OnKickAttempt(Entity<VehicleComponent> vehicle, ref DoAfterAttemptEvent<VehicleKickDoAfterEvent> args)
    {
        if (args.DoAfter.Args.Target is not { } target ||
            !CanKick(args.DoAfter.Args.User, target, vehicle.Owner, vehicle.Comp))
        {
            args.Cancel();
        }
    }

    private void OnSeatInteractHand(EntityUid uid, VehicleSeatMarkerComponent component, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        BeginEntry((uid, component), args.User, args.User, true);
    }

    private void OnSeatDragDrop(Entity<VehicleSeatMarkerComponent> entity, ref DragDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = BeginEntry(entity, args.User, args.Dragged, false);
    }

    private bool BeginEntry(
        Entity<VehicleSeatMarkerComponent> marker,
        EntityUid actor,
        EntityUid occupant,
        bool popupFailures)
    {
        if (!TryComp(marker.Comp.Vehicle, out VehicleComponent? vehicle))
            return false;

        if (IsMoving(marker.Comp.Vehicle, vehicle))
        {
            if (popupFailures)
                _popup.PopupEntity(Loc.GetString("vehicle-moving"), actor, actor);
            return false;
        }

        if (marker.Comp.PendingOccupant != null ||
            !vehicle.SeatContainers.TryGetValue(marker.Comp.SeatId, out var container) ||
            container.ContainedEntity != null)
        {
            if (popupFailures)
                _popup.PopupEntity(Loc.GetString("vehicle-seat-occupied"), actor, actor);
            return false;
        }

        if (!CanInsert(marker, actor, occupant, vehicle))
        {
            if (popupFailures)
                _popup.PopupEntity(Loc.GetString("vehicle-no-enter"), actor, actor);
            return false;
        }

        marker.Comp.PendingOccupant = occupant;
        SetSeatAvailable(marker, false);

        var doAfterArgs = new DoAfterArgs(EntityManager, actor, vehicle.EntryDelay, new VehicleEntryDoAfterEvent(), marker, target: occupant, used: marker)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
            DuplicateCondition = DuplicateConditions.None,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
        {
            ReleaseReservation(marker, vehicle);
            return false;
        }

        if (actor != occupant)
        {
            _popup.PopupEntity(
                Loc.GetString("vehicle-being-seated"),
                occupant,
                occupant,
                PopupType.MediumCaution);
        }

        return true;
    }

    private void OnEntry(Entity<VehicleSeatMarkerComponent> marker, ref VehicleEntryDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!TryComp(marker.Comp.Vehicle, out VehicleComponent? vehicle) ||
            args.Target is not { } occupant ||
            marker.Comp.PendingOccupant != occupant)
            return;

        if (args.Cancelled)
        {
            ReleaseReservation(marker, vehicle);
            return;
        }

        if (!TryInsert(marker, args.User, occupant, vehicle, true))
        {
            ReleaseReservation(marker, vehicle);
            _popup.PopupEntity(Loc.GetString("vehicle-no-enter"), args.User, args.User);
            return;
        }

        marker.Comp.PendingOccupant = null;
        SetSeatAvailable(marker, false);
        UpdateUi(marker.Comp.Vehicle, vehicle);
    }

    private void OnEntryAttempt(Entity<VehicleSeatMarkerComponent> marker, ref DoAfterAttemptEvent<VehicleEntryDoAfterEvent> args)
    {
        if (!TryComp(marker.Comp.Vehicle, out VehicleComponent? vehicle) ||
            args.DoAfter.Args.Target is not { } occupant ||
            marker.Comp.PendingOccupant != occupant ||
            !CanInsert(marker, args.DoAfter.Args.User, occupant, vehicle, true))
        {
            args.Cancel();
        }
    }

    private void OnExit(EntityUid uid, VehicleComponent component, VehicleExitDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = TryEject(args.User);
    }

    private void OnExitAttempt(EntityUid uid, VehicleComponent component, DoAfterAttemptEvent<VehicleExitDoAfterEvent> args)
    {
        if (!CanEject(args.DoAfter.Args.User, uid, component))
            args.Cancel();
    }

    private void OnEntityStorageDump(EntityUid uid, VehicleComponent component, ref EntityStorageIntoContainerAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnGetAdditionalAccess(EntityUid uid, VehicleComponent component, ref GetAdditionalAccessEvent args)
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

    private void OnEntRemoved(EntityUid uid, VehicleComponent component, EntRemovedFromContainerMessage args)
    {
        if (!TryComp(args.Entity, out VehicleOccupantComponent? occupant) || occupant.Vehicle != uid)
            return;

        if (!component.SeatContainers.TryGetValue(occupant.SeatId, out var container) || args.Container != container)
            return;

        if (component.OccupantsChangingSeats.Contains(args.Entity))
        {
            if (component.SeatMarkers.TryGetValue(occupant.SeatId, out var oldMarker) &&
                TryComp(oldMarker, out VehicleSeatMarkerComponent? oldSeat))
            {
                oldSeat.PendingOccupant = null;
                SetSeatAvailable((oldMarker, oldSeat), true);
            }

            return;
        }

        PlaceAtExit(uid, args.Entity, occupant.SeatId, component);
        CleanupOccupant(uid, args.Entity);

        if (component.SeatMarkers.TryGetValue(occupant.SeatId, out var marker) &&
            TryComp(marker, out VehicleSeatMarkerComponent? seat))
        {
            seat.PendingOccupant = null;
            SetSeatAvailable((marker, seat), true);
        }

        UpdateUi(uid, component);
    }

    private void OnOccupantIdentityChanged(Entity<VehicleOccupantComponent> occupant, ref IdentityChangedEvent args)
    {
        if (TryComp(occupant.Comp.Vehicle, out VehicleComponent? vehicle))
            UpdateUi(occupant.Comp.Vehicle, vehicle);
    }

    private bool TryChangeSeat(Entity<VehicleComponent> vehicle, EntityUid occupantUid, string seatId)
    {
        if (!CanChangeSeat(occupantUid, vehicle.Owner, seatId, vehicle.Comp, true) ||
            !vehicle.Comp.SeatContainers.TryGetValue(seatId, out var destination) ||
            !vehicle.Comp.SeatMarkers.TryGetValue(seatId, out var markerUid) ||
            !TryComp(markerUid, out VehicleSeatMarkerComponent? marker))
        {
            return false;
        }

        vehicle.Comp.OccupantsChangingSeats.Add(occupantUid);
        try
        {
            if (!_container.Insert(occupantUid, destination))
                return false;
        }
        finally
        {
            vehicle.Comp.OccupantsChangingSeats.Remove(occupantUid);
        }

        marker.PendingOccupant = null;
        SetSeatAvailable((markerUid, marker), false);
        SetupOccupant(vehicle.Owner, occupantUid, seatId, vehicle.Comp);
        return true;
    }

    public bool CanInsert(
        Entity<VehicleSeatMarkerComponent> marker,
        EntityUid actor,
        EntityUid occupant,
        VehicleComponent? component = null,
        bool allowReservation = false)
    {
        if (!Resolve(marker.Comp.Vehicle, ref component) ||
            !component.SeatContainers.TryGetValue(marker.Comp.SeatId, out var container))
            return false;

        return container.ContainedEntity == null &&
               CanUseSeat(marker, actor, occupant, allowReservation);
    }

    public bool TryInsert(
        Entity<VehicleSeatMarkerComponent> marker,
        EntityUid actor,
        EntityUid occupant,
        VehicleComponent? component = null,
        bool allowReservation = false)
    {
        if (!Resolve(marker.Comp.Vehicle, ref component))
            return false;

        if (!CanInsert(marker, actor, occupant, component, allowReservation) ||
            !component.SeatContainers.TryGetValue(marker.Comp.SeatId, out var container))
            return false;

        if (!_container.Insert(occupant, container))
            return false;

        SetupOccupant(marker.Comp.Vehicle, occupant, marker.Comp.SeatId, component);
        return true;
    }

    public bool TryEject(EntityUid occupantUid, bool force = false)
    {
        if (!TryComp(occupantUid, out VehicleOccupantComponent? occupant) ||
            !TryComp(occupant.Vehicle, out VehicleComponent? component) ||
            !CanEject(occupantUid, occupant.Vehicle, component, force) ||
            !component.SeatContainers.TryGetValue(occupant.SeatId, out var container))
            return false;

        var exitCoordinates = GetExitCoordinates(occupant.Vehicle, occupant.SeatId, component);

        if (!_container.RemoveEntity(occupant.Vehicle, occupantUid))
            return false;

        if (exitCoordinates != null)
            _transform.SetCoordinates(occupantUid, exitCoordinates.Value);

        CleanupOccupant(occupant.Vehicle, occupantUid);
        return true;
    }

    public bool EjectAll(EntityUid uid, VehicleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        foreach (var container in component.SeatContainers.Values)
        {
            if (container.ContainedEntity is { } occupant && !TryEject(occupant, true))
                return false;
        }

        return true;
    }

    private void SetupOccupant(EntityUid vehicle, EntityUid occupantUid, string seatId, VehicleComponent component)
    {
        var occupant = EnsureComp<VehicleOccupantComponent>(occupantUid);
        occupant.Vehicle = vehicle;
        occupant.SeatId = seatId;
        Dirty(occupantUid, occupant);

        if (TryComp<RelayInputMoverComponent>(occupantUid, out var relay) && relay.RelayEntity == vehicle)
            RemComp<RelayInputMoverComponent>(occupantUid);

        if (TryGetSeat(component, seatId, out var seat))
        {
            _transform.SetCoordinates(occupantUid, new EntityCoordinates(vehicle, seat.OccupantOffset));
            if (seat.Driver)
                _mover.SetRelay(occupantUid, vehicle);
        }

        _actions.AddAction(occupantUid, ref occupant.EjectActionEntity, component.EjectAction, vehicle);
        _actions.AddAction(occupantUid, ref occupant.ChangeSeatActionEntity, component.ChangeSeatAction, vehicle);
        UpdateUi(vehicle, component);
    }

    private void CleanupOccupant(EntityUid vehicle, EntityUid occupantUid)
    {
        _ui.CloseUi(vehicle, VehicleUiKey.Seats, occupantUid);

        if (TryComp<VehicleOccupantComponent>(occupantUid, out var occupant) && occupant.Vehicle == vehicle)
            RemComp<VehicleOccupantComponent>(occupantUid);

        if (TryComp<RelayInputMoverComponent>(occupantUid, out var relay) && relay.RelayEntity == vehicle)
            RemComp<RelayInputMoverComponent>(occupantUid);

        _actions.RemoveProvidedActions(occupantUid, vehicle);
    }

    private bool CanManipulateSeats(EntityUid actor, EntityUid vehicleUid, VehicleComponent component)
    {
        if (!Exists(actor) ||
            !Exists(vehicleUid) ||
            !_mobState.IsAlive(actor) ||
            !TryComp(actor, out HandsComponent? hands) ||
            hands.Count == 0 ||
            TryComp(actor, out CuffableComponent? cuffable) && cuffable.CuffedHandCount > 0 ||
            !TryComp(actor, out VehicleOccupantComponent? occupant) ||
            occupant.Vehicle != vehicleUid ||
            !component.SeatContainers.TryGetValue(occupant.SeatId, out var currentContainer) ||
            currentContainer.ContainedEntity != actor)
        {
            return false;
        }

        return true;
    }

    private bool CanChangeSeat(
        EntityUid actor,
        EntityUid vehicleUid,
        string seatId,
        VehicleComponent component,
        bool allowReservation = false)
    {
        if (!CanManipulateSeats(actor, vehicleUid, component) ||
            !TryComp(actor, out VehicleOccupantComponent? occupant) ||
            occupant.SeatId == seatId ||
            !TryGetSeat(component, seatId, out _) ||
            !component.SeatContainers.TryGetValue(seatId, out var destination) ||
            destination.ContainedEntity != null ||
            !component.SeatMarkers.TryGetValue(seatId, out var markerUid) ||
            !TryComp(markerUid, out VehicleSeatMarkerComponent? marker) ||
            marker.PendingOccupant != null && (!allowReservation || marker.PendingOccupant != actor))
        {
            return false;
        }

        return true;
    }

    private bool CanKick(EntityUid actor, EntityUid target, EntityUid vehicleUid, VehicleComponent component)
    {
        if (!CanManipulateSeats(actor, vehicleUid, component) ||
            actor == target ||
            !Exists(target) ||
            IsMoving(vehicleUid, component) ||
            !TryComp(target, out VehicleOccupantComponent? targetOccupant) ||
            targetOccupant.Vehicle != vehicleUid ||
            !component.SeatContainers.TryGetValue(targetOccupant.SeatId, out var targetContainer) ||
            targetContainer.ContainedEntity != target)
        {
            return false;
        }

        return true;
    }

    private void UpdateUi(Entity<VehicleComponent> vehicle)
    {
        UpdateUi(vehicle.Owner, vehicle.Comp);
    }

    private void UpdateUi(EntityUid uid, VehicleComponent component)
    {
        var seats = new List<VehicleSeatUiData>(component.Seats.Count);
        foreach (var seat in component.Seats)
        {
            EntityUid? occupant = null;
            if (component.SeatContainers.TryGetValue(seat.Id, out var container))
                occupant = container.ContainedEntity;

            seats.Add(new VehicleSeatUiData(
                seat.Id,
                seat.OccupantOffset,
                seat.Driver,
                occupant is { } entity ? GetNetEntity(entity) : null,
                occupant is { } named ? Identity.Name(named, EntityManager) : null));
        }

        _ui.SetUiState(uid, VehicleUiKey.Seats, new VehicleSeatBoundUserInterfaceState(seats));
    }

    private bool CanEject(EntityUid occupantUid, EntityUid vehicleUid, VehicleComponent component, bool force = false)
    {
        return TryComp(occupantUid, out VehicleOccupantComponent? occupant)
               && occupant.Vehicle == vehicleUid
               && component.SeatContainers.TryGetValue(occupant.SeatId, out var container)
               && container.ContainedEntity == occupantUid
               && (force || !IsMoving(vehicleUid, component));
    }

    private void ReleaseReservation(Entity<VehicleSeatMarkerComponent> marker, VehicleComponent component)
    {
        marker.Comp.PendingOccupant = null;
        var available = component.SeatContainers.TryGetValue(marker.Comp.SeatId, out var container) &&
                        container.ContainedEntity == null;
        SetSeatAvailable(marker, available);
    }

    private void SetSeatAvailable(Entity<VehicleSeatMarkerComponent> marker, bool available)
    {
        if (marker.Comp.Available == available)
            return;

        marker.Comp.Available = available;
        Dirty(marker);
    }

    private void PlaceAtExit(EntityUid vehicleUid, EntityUid occupantUid, string seatId, VehicleComponent component)
    {
        if (GetExitCoordinates(vehicleUid, seatId, component) is { } coordinates)
            _transform.SetCoordinates(occupantUid, coordinates);
    }

    private EntityCoordinates? GetExitCoordinates(EntityUid vehicleUid, string seatId, VehicleComponent component)
    {
        if (Terminating(vehicleUid) || !TryGetSeat(component, seatId, out var seat))
            return null;

        var vehicleTransform = Transform(vehicleUid);
        if (vehicleTransform.MapID == MapId.Nullspace || !Exists(vehicleTransform.ParentUid))
            return null;

        var mapCoordinates = _transform.ToMapCoordinates(new EntityCoordinates(vehicleUid, seat.ExitOffset));
        return _transform.ToCoordinates(vehicleTransform.ParentUid, mapCoordinates);
    }

    private static bool TryGetSeat(VehicleComponent component, string seatId, out VehicleSeatDefinition seat)
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
        return $"vehicle-seat-{seatId}";
    }
}
