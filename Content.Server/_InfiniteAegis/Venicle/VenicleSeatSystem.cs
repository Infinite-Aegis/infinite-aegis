using Content.Shared.Access.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Venicle;
using Content.Shared.Venicle.Components;
using Content.Shared.Whitelist;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Server.Venicle;

public sealed partial class VenicleSeatSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
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

        if (!TryComp(component.Venicle, out VenicleComponent? venicle))
            return;

        if (IsMoving(component.Venicle, venicle))
        {
            _popup.PopupEntity(Loc.GetString("venicle-moving"), args.User);
            return;
        }

        if (component.PendingUser != null ||
            !venicle.SeatContainers.TryGetValue(component.SeatId, out var container) ||
            container.ContainedEntity != null)
        {
            _popup.PopupEntity(Loc.GetString("venicle-seat-occupied"), args.User);
            return;
        }

        if (!CanInsert(component.Venicle, component.SeatId, args.User, venicle, component))
        {
            _popup.PopupEntity(Loc.GetString("venicle-no-enter"), args.User);
            return;
        }

        component.PendingUser = args.User;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, venicle.EntryDelay, new VenicleEntryEvent(), uid, target: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
            DuplicateCondition = DuplicateConditions.None,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            component.PendingUser = null;
    }

    private void OnEntry(EntityUid uid, VenicleSeatComponent component, VenicleEntryEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (component.PendingUser == args.User)
            component.PendingUser = null;

        if (args.Cancelled)
            return;

        if (!TryComp(component.Venicle, out VenicleComponent? venicle) ||
            !TryInsert(component.Venicle, component.SeatId, args.User, venicle, component))
        {
            _popup.PopupEntity(Loc.GetString("venicle-no-enter"), args.User);
        }
    }

    private void OnEntryAttempt(EntityUid uid, VenicleSeatComponent component, DoAfterAttemptEvent<VenicleEntryEvent> args)
    {
        if (!TryComp(component.Venicle, out VenicleComponent? venicle) ||
            component.PendingUser != args.DoAfter.Args.User ||
            !CanInsert(component.Venicle, component.SeatId, args.DoAfter.Args.User, venicle, component))
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
    }

    public bool CanInsert(
        EntityUid uid,
        string seatId,
        EntityUid toInsert,
        VenicleComponent? component = null,
        VenicleSeatComponent? seatComponent = null)
    {
        if (!Resolve(uid, ref component) ||
            !component.SeatContainers.TryGetValue(seatId, out var container))
            return false;

        return container.ContainedEntity == null
               && (seatComponent?.PendingUser == null || seatComponent.PendingUser == toInsert)
               && !HasComp<VenicleOccupantComponent>(toInsert)
               && _actionBlocker.CanMove(toInsert)
               && !_whitelist.IsWhitelistFail(component.OccupantWhitelist, toInsert)
               && !IsMoving(uid, component);
    }

    public bool TryInsert(
        EntityUid uid,
        string seatId,
        EntityUid? toInsert,
        VenicleComponent? component = null,
        VenicleSeatComponent? seatComponent = null)
    {
        if (!Resolve(uid, ref component) || toInsert == null)
            return false;

        if (!CanInsert(uid, seatId, toInsert.Value, component, seatComponent) ||
            !component.SeatContainers.TryGetValue(seatId, out var container))
            return false;

        if (!_container.Insert(toInsert.Value, container))
            return false;

        SetupOccupant(uid, toInsert.Value, seatId, component);
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

    private bool IsMoving(EntityUid uid, VenicleComponent component)
    {
        if (!TryComp(uid, out PhysicsComponent? physics))
            return false;

        var maxSpeed = MathF.Max(0f, component.MaxSeatInteractionSpeed);
        var maxAngularSpeed = MathF.Max(0f, component.MaxSeatInteractionAngularSpeed);
        return physics.LinearVelocity.LengthSquared() > maxSpeed * maxSpeed
               || MathF.Abs(physics.AngularVelocity) > maxAngularSpeed;
    }

    private void PlaceAtExit(EntityUid venicleUid, EntityUid occupantUid, string seatId, VenicleComponent component)
    {
        if (GetExitCoordinates(venicleUid, seatId, component) is { } coordinates)
            _transform.SetCoordinates(occupantUid, coordinates);
    }

    private EntityCoordinates? GetExitCoordinates(EntityUid venicleUid, string seatId, VenicleComponent component)
    {
        if (!TryGetSeat(component, seatId, out var seat))
            return null;

        var venicleTransform = Transform(venicleUid);
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
