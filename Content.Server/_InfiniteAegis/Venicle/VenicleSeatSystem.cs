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
using Content.Shared.Venicle;
using Content.Shared.Venicle.Components;
using Content.Shared.Venicle.Systems;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server.Venicle;

public sealed partial class VenicleSeatSystem : SharedVenicleSeatSystem
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

        SubscribeLocalEvent<VenicleComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<VenicleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<VenicleComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VenicleComponent, VenicleEjectEvent>(OnEjectAction);
        SubscribeLocalEvent<VenicleComponent, VenicleChangeSeatActionEvent>(OnChangeSeatAction);
        SubscribeLocalEvent<VenicleComponent, VenicleExitEvent>(OnExit);
        SubscribeLocalEvent<VenicleComponent, DoAfterAttemptEvent<VenicleExitEvent>>(OnExitAttempt);
        SubscribeLocalEvent<VenicleComponent, VenicleChangeSeatDoAfterEvent>(OnChangeSeat);
        SubscribeLocalEvent<VenicleComponent, DoAfterAttemptEvent<VenicleChangeSeatDoAfterEvent>>(OnChangeSeatAttempt);
        SubscribeLocalEvent<VenicleComponent, VenicleKickDoAfterEvent>(OnKick);
        SubscribeLocalEvent<VenicleComponent, DoAfterAttemptEvent<VenicleKickDoAfterEvent>>(OnKickAttempt);
        SubscribeLocalEvent<VenicleComponent, VenicleRequestUpdateMessage>(OnRequestUpdate);
        SubscribeLocalEvent<VenicleComponent, VenicleChangeSeatMessage>(OnChangeSeatRequested);
        SubscribeLocalEvent<VenicleComponent, VenicleKickOccupantMessage>(OnKickRequested);
        SubscribeLocalEvent<VenicleComponent, EntityStorageIntoContainerAttemptEvent>(OnEntityStorageDump);
        SubscribeLocalEvent<VenicleComponent, GetAdditionalAccessEvent>(OnGetAdditionalAccess);
        SubscribeLocalEvent<VenicleComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
        SubscribeLocalEvent<VenicleSeatComponent, InteractHandEvent>(OnSeatInteractHand);
        SubscribeLocalEvent<VenicleSeatComponent, DragDropTargetEvent>(OnSeatDragDrop);
        SubscribeLocalEvent<VenicleSeatComponent, VenicleEntryEvent>(OnEntry);
        SubscribeLocalEvent<VenicleSeatComponent, DoAfterAttemptEvent<VenicleEntryEvent>>(OnEntryAttempt);
        SubscribeLocalEvent<VenicleOccupantComponent, IdentityChangedEvent>(OnOccupantIdentityChanged);
    }

    private void OnInit(EntityUid uid, VenicleComponent component, ComponentInit args)
    {
        foreach (var seat in component.Seats)
        {
            if (component.SeatContainers.TryGetValue(seat.Id, out var existing))
            {
                existing.ShowContents = true;
                existing.OccludesLight = false;
                continue;
            }

            var container = _container.EnsureContainer<ContainerSlot>(uid, GetContainerId(seat.Id));
            container.ShowContents = true;
            container.OccludesLight = false;
            component.SeatContainers.Add(seat.Id, container);
        }

        Dirty(uid, Comp<ContainerManagerComponent>(uid));
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
        _ui.CloseUi(uid, VenicleUiKey.Seats);

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

    private void OnChangeSeatAction(EntityUid uid, VenicleComponent component, VenicleChangeSeatActionEvent args)
    {
        if (args.Handled || !CanManipulateSeats(args.Performer, uid, component))
            return;

        args.Handled = true;
        UpdateUi(uid, component);
        _ui.OpenUi(uid, VenicleUiKey.Seats, args.Performer);
    }

    private void OnRequestUpdate(Entity<VenicleComponent> venicle, ref VenicleRequestUpdateMessage args)
    {
        if (!CanManipulateSeats(args.Actor, venicle.Owner, venicle.Comp))
        {
            _ui.CloseUi(venicle.Owner, VenicleUiKey.Seats, args.Actor);
            return;
        }

        UpdateUi(venicle);
    }

    private void OnChangeSeatRequested(Entity<VenicleComponent> venicle, ref VenicleChangeSeatMessage args)
    {
        BeginChangeSeat(venicle, args.Actor, args.SeatId);
    }

    private void OnKickRequested(Entity<VenicleComponent> venicle, ref VenicleKickOccupantMessage args)
    {
        var target = GetEntity(args.Occupant);
        if (!CanKick(args.Actor, target, venicle.Owner, venicle.Comp))
        {
            _popup.PopupEntity(Loc.GetString("venicle-cannot-kick"), args.Actor, args.Actor);
            return;
        }

        _popup.PopupEntity(
            Loc.GetString("venicle-kick-start-self", ("target", Identity.Entity(target, EntityManager, args.Actor))),
            args.Actor,
            args.Actor,
            PopupType.MediumCaution);
        _popup.PopupEntity(
            Loc.GetString("venicle-kick-start-target", ("user", Identity.Entity(args.Actor, EntityManager, target))),
            target,
            target,
            PopupType.MediumCaution);

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            args.Actor,
            venicle.Comp.KickDelay,
            new VenicleKickDoAfterEvent(),
            venicle.Owner,
            target: target)
        {
            AttemptFrequency = AttemptFrequency.EveryTick,
            DistanceThreshold = null,
            RequireCanInteract = false,
            DuplicateCondition = DuplicateConditions.None,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void BeginChangeSeat(Entity<VenicleComponent> venicle, EntityUid actor, string seatId)
    {
        if (!CanChangeSeat(actor, venicle.Owner, seatId, venicle.Comp) ||
            !venicle.Comp.SeatMarkers.TryGetValue(seatId, out var markerUid) ||
            !TryComp(markerUid, out VenicleSeatComponent? marker))
        {
            _popup.PopupEntity(Loc.GetString("venicle-cannot-change-seat"), actor, actor);
            return;
        }

        marker.PendingOccupant = actor;
        SetSeatAvailable((markerUid, marker), false);

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            actor,
            venicle.Comp.ChangeSeatDelay,
            new VenicleChangeSeatDoAfterEvent(seatId),
            venicle.Owner)
        {
            AttemptFrequency = AttemptFrequency.EveryTick,
            DistanceThreshold = null,
            RequireCanInteract = false,
            DuplicateCondition = DuplicateConditions.None,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            ReleaseReservation((markerUid, marker), venicle.Comp);

        UpdateUi(venicle);
    }

    private void OnChangeSeat(Entity<VenicleComponent> venicle, ref VenicleChangeSeatDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!venicle.Comp.SeatMarkers.TryGetValue(args.SeatId, out var markerUid) ||
            !TryComp(markerUid, out VenicleSeatComponent? marker))
            return;

        if (args.Cancelled || !CanChangeSeat(args.User, venicle.Owner, args.SeatId, venicle.Comp, true))
        {
            ReleaseReservation((markerUid, marker), venicle.Comp);
            UpdateUi(venicle);
            return;
        }

        if (!TryChangeSeat(venicle, args.User, args.SeatId))
        {
            ReleaseReservation((markerUid, marker), venicle.Comp);
            _popup.PopupEntity(Loc.GetString("venicle-cannot-change-seat"), args.User, args.User);
        }

        UpdateUi(venicle);
    }

    private void OnChangeSeatAttempt(
        Entity<VenicleComponent> venicle,
        ref DoAfterAttemptEvent<VenicleChangeSeatDoAfterEvent> args)
    {
        if (!CanChangeSeat(args.DoAfter.Args.User, venicle.Owner, args.Event.SeatId, venicle.Comp, true))
            args.Cancel();
    }

    private void OnKick(Entity<VenicleComponent> venicle, ref VenicleKickDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        if (!CanKick(args.User, target, venicle.Owner, venicle.Comp))
            return;

        // Resolve the target from the current seat rather than the seat shown when the do-after started.
        foreach (var container in venicle.Comp.SeatContainers.Values)
        {
            if (container.ContainedEntity != target)
                continue;

            TryEject(target);
            return;
        }
    }

    private void OnKickAttempt(Entity<VenicleComponent> venicle, ref DoAfterAttemptEvent<VenicleKickDoAfterEvent> args)
    {
        if (args.DoAfter.Args.Target is not { } target ||
            !CanKick(args.DoAfter.Args.User, target, venicle.Owner, venicle.Comp))
        {
            args.Cancel();
        }
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
        UpdateUi(marker.Comp.Venicle, venicle);
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

        if (component.OccupantsChangingSeats.Contains(args.Entity))
        {
            if (component.SeatMarkers.TryGetValue(occupant.SeatId, out var oldMarker) &&
                TryComp(oldMarker, out VenicleSeatComponent? oldSeat))
            {
                oldSeat.PendingOccupant = null;
                SetSeatAvailable((oldMarker, oldSeat), true);
            }

            return;
        }

        PlaceAtExit(uid, args.Entity, occupant.SeatId, component);
        CleanupOccupant(uid, args.Entity);

        if (component.SeatMarkers.TryGetValue(occupant.SeatId, out var marker) &&
            TryComp(marker, out VenicleSeatComponent? seat))
        {
            seat.PendingOccupant = null;
            SetSeatAvailable((marker, seat), true);
        }

        UpdateUi(uid, component);
    }

    private void OnOccupantIdentityChanged(Entity<VenicleOccupantComponent> occupant, ref IdentityChangedEvent args)
    {
        if (TryComp(occupant.Comp.Venicle, out VenicleComponent? venicle))
            UpdateUi(occupant.Comp.Venicle, venicle);
    }

    private bool TryChangeSeat(Entity<VenicleComponent> venicle, EntityUid occupantUid, string seatId)
    {
        if (!CanChangeSeat(occupantUid, venicle.Owner, seatId, venicle.Comp, true) ||
            !venicle.Comp.SeatContainers.TryGetValue(seatId, out var destination) ||
            !venicle.Comp.SeatMarkers.TryGetValue(seatId, out var markerUid) ||
            !TryComp(markerUid, out VenicleSeatComponent? marker))
        {
            return false;
        }

        venicle.Comp.OccupantsChangingSeats.Add(occupantUid);
        try
        {
            if (!_container.Insert(occupantUid, destination))
                return false;
        }
        finally
        {
            venicle.Comp.OccupantsChangingSeats.Remove(occupantUid);
        }

        marker.PendingOccupant = null;
        SetSeatAvailable((markerUid, marker), false);
        SetupOccupant(venicle.Owner, occupantUid, seatId, venicle.Comp);
        return true;
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

        if (TryComp<RelayInputMoverComponent>(occupantUid, out var relay) && relay.RelayEntity == venicle)
            RemComp<RelayInputMoverComponent>(occupantUid);

        if (TryGetSeat(component, seatId, out var seat))
        {
            _transform.SetCoordinates(occupantUid, new EntityCoordinates(venicle, seat.OccupantOffset));
            if (seat.Driver)
                _mover.SetRelay(occupantUid, venicle);
        }

        _actions.AddAction(occupantUid, ref occupant.EjectActionEntity, component.EjectAction, venicle);
        _actions.AddAction(occupantUid, ref occupant.ChangeSeatActionEntity, component.ChangeSeatAction, venicle);
        UpdateUi(venicle, component);
    }

    private void CleanupOccupant(EntityUid venicle, EntityUid occupantUid)
    {
        _ui.CloseUi(venicle, VenicleUiKey.Seats, occupantUid);

        if (TryComp<VenicleOccupantComponent>(occupantUid, out var occupant) && occupant.Venicle == venicle)
            RemComp<VenicleOccupantComponent>(occupantUid);

        if (TryComp<RelayInputMoverComponent>(occupantUid, out var relay) && relay.RelayEntity == venicle)
            RemComp<RelayInputMoverComponent>(occupantUid);

        _actions.RemoveProvidedActions(occupantUid, venicle);
    }

    private bool CanManipulateSeats(EntityUid actor, EntityUid venicleUid, VenicleComponent component)
    {
        if (!Exists(actor) ||
            !Exists(venicleUid) ||
            !_mobState.IsAlive(actor) ||
            !TryComp(actor, out HandsComponent? hands) ||
            hands.Count == 0 ||
            TryComp(actor, out CuffableComponent? cuffable) && cuffable.CuffedHandCount > 0 ||
            !TryComp(actor, out VenicleOccupantComponent? occupant) ||
            occupant.Venicle != venicleUid ||
            !component.SeatContainers.TryGetValue(occupant.SeatId, out var currentContainer) ||
            currentContainer.ContainedEntity != actor)
        {
            return false;
        }

        return true;
    }

    private bool CanChangeSeat(
        EntityUid actor,
        EntityUid venicleUid,
        string seatId,
        VenicleComponent component,
        bool allowReservation = false)
    {
        if (!CanManipulateSeats(actor, venicleUid, component) ||
            !TryComp(actor, out VenicleOccupantComponent? occupant) ||
            occupant.SeatId == seatId ||
            !TryGetSeat(component, seatId, out _) ||
            !component.SeatContainers.TryGetValue(seatId, out var destination) ||
            destination.ContainedEntity != null ||
            !component.SeatMarkers.TryGetValue(seatId, out var markerUid) ||
            !TryComp(markerUid, out VenicleSeatComponent? marker) ||
            marker.PendingOccupant != null && (!allowReservation || marker.PendingOccupant != actor))
        {
            return false;
        }

        return true;
    }

    private bool CanKick(EntityUid actor, EntityUid target, EntityUid venicleUid, VenicleComponent component)
    {
        if (!CanManipulateSeats(actor, venicleUid, component) ||
            !Exists(target) ||
            IsMoving(venicleUid, component) ||
            !TryComp(target, out VenicleOccupantComponent? targetOccupant) ||
            targetOccupant.Venicle != venicleUid ||
            !component.SeatContainers.TryGetValue(targetOccupant.SeatId, out var targetContainer) ||
            targetContainer.ContainedEntity != target)
        {
            return false;
        }

        return true;
    }

    private void UpdateUi(Entity<VenicleComponent> venicle)
    {
        UpdateUi(venicle.Owner, venicle.Comp);
    }

    private void UpdateUi(EntityUid uid, VenicleComponent component)
    {
        var seats = new List<VenicleSeatUiData>(component.Seats.Count);
        foreach (var seat in component.Seats)
        {
            EntityUid? occupant = null;
            if (component.SeatContainers.TryGetValue(seat.Id, out var container))
                occupant = container.ContainedEntity;

            seats.Add(new VenicleSeatUiData(
                seat.Id,
                seat.OccupantOffset,
                seat.Driver,
                occupant is { } entity ? GetNetEntity(entity) : null,
                occupant is { } named ? Identity.Name(named, EntityManager) : null));
        }

        _ui.SetUiState(uid, VenicleUiKey.Seats, new VenicleBoundUserInterfaceState(seats));
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
