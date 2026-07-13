using Content.Shared.Access.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Verbs;
using Content.Shared.Venicle;
using Content.Shared.Venicle.Components;
using Content.Shared.Whitelist;
using Robust.Server.Containers;
using Robust.Shared.Containers;

namespace Content.Server.Venicle;

public sealed partial class VenicleDriverSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VenicleComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<VenicleComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VenicleComponent, VenicleEjectEvent>(OnEjectAction);
        SubscribeLocalEvent<VenicleComponent, VenicleEntryEvent>(OnEntry);
        SubscribeLocalEvent<VenicleComponent, VenicleExitEvent>(OnExit);
        SubscribeLocalEvent<VenicleComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<VenicleComponent, DragDropTargetEvent>(OnDragDrop);
        SubscribeLocalEvent<VenicleComponent, CanDropTargetEvent>(OnCanDragDrop);
        SubscribeLocalEvent<VenicleComponent, EntityStorageIntoContainerAttemptEvent>(OnEntityStorageDump);
        SubscribeLocalEvent<VenicleComponent, GetAdditionalAccessEvent>(OnGetAdditionalAccess);
        SubscribeLocalEvent<VenicleComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
    }

    private void OnInit(EntityUid uid, VenicleComponent component, ComponentInit args)
    {
        component.DriverSlot = _container.EnsureContainer<ContainerSlot>(uid, component.DriverSlotId);
    }

    private void OnShutdown(EntityUid uid, VenicleComponent component, ComponentShutdown args)
    {
        TryEject(uid, component);
    }

    private void OnEjectAction(EntityUid uid, VenicleComponent component, VenicleEjectEvent args)
    {
        if (args.Handled || component.DriverSlot.ContainedEntity != args.Performer)
            return;

        args.Handled = TryEject(uid, component);
    }

    private void OnEntry(EntityUid uid, VenicleComponent component, VenicleEntryEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (!CanInsert(uid, args.User, component))
        {
            _popup.PopupEntity(Loc.GetString("venicle-no-enter"), args.User);
            args.Handled = true;
            return;
        }

        TryInsert(uid, args.User, component);
        args.Handled = true;
    }

    private void OnExit(EntityUid uid, VenicleComponent component, VenicleExitEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = TryEject(uid, component);
    }

    private void OnGetAlternativeVerbs(EntityUid uid, VenicleComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (CanInsert(uid, args.User, component))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("venicle-verb-enter"),
                Act = () =>
                {
                    var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.EntryDelay, new VenicleEntryEvent(), uid, target: uid)
                    {
                        BreakOnMove = true,
                    };

                    _doAfter.TryStartDoAfter(doAfterEventArgs);
                }
            });

            return;
        }

        if (component.DriverSlot.ContainedEntity == null)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("venicle-verb-exit"),
            Priority = 1,
            Act = () =>
            {
                var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.ExitDelay, new VenicleExitEvent(), uid, target: uid)
                {
                    BreakOnMove = true,
                };

                _doAfter.TryStartDoAfter(doAfterEventArgs);
            }
        });
    }

    private void OnDragDrop(EntityUid uid, VenicleComponent component, ref DragDropTargetEvent args)
    {
        if (args.Handled || !CanInsert(uid, args.Dragged, component))
            return;

        args.Handled = true;

        var doAfterEventArgs = new DoAfterArgs(EntityManager, args.Dragged, component.EntryDelay, new VenicleEntryEvent(), uid, target: uid)
        {
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
    }

    private void OnCanDragDrop(EntityUid uid, VenicleComponent component, ref CanDropTargetEvent args)
    {
        args.Handled = true;
        args.CanDrop |= CanInsert(uid, args.Dragged, component);
    }

    private void OnEntityStorageDump(EntityUid uid, VenicleComponent component, ref EntityStorageIntoContainerAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnGetAdditionalAccess(EntityUid uid, VenicleComponent component, ref GetAdditionalAccessEvent args)
    {
        if (component.DriverSlot.ContainedEntity is { } driver)
            args.Entities.Add(driver);
    }

    private void OnEntRemoved(EntityUid uid, VenicleComponent component, EntRemovedFromContainerMessage args)
    {
        if (args.Container != component.DriverSlot)
            return;

        CleanupDriver(uid, args.Entity);
    }

    public bool CanInsert(EntityUid uid, EntityUid toInsert, VenicleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        return component.DriverSlot.ContainedEntity == null
               && _actionBlocker.CanMove(toInsert)
               && !_whitelist.IsWhitelistFail(component.DriverWhitelist, toInsert);
    }

    public bool TryInsert(EntityUid uid, EntityUid? toInsert, VenicleComponent? component = null)
    {
        if (!Resolve(uid, ref component) || toInsert == null)
            return false;

        if (!CanInsert(uid, toInsert.Value, component))
            return false;

        if (!_container.Insert(toInsert.Value, component.DriverSlot))
            return false;

        SetupDriver(uid, toInsert.Value, component);
        return true;
    }

    public bool TryEject(EntityUid uid, VenicleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (component.DriverSlot.ContainedEntity is not { } driver)
            return false;

        if (!_container.RemoveEntity(uid, driver))
            return false;

        CleanupDriver(uid, driver);
        return true;
    }

    private void SetupDriver(EntityUid venicle, EntityUid driver, VenicleComponent component)
    {
        var driverComponent = EnsureComp<VenicleDriverComponent>(driver);
        driverComponent.Venicle = venicle;
        Dirty(driver, driverComponent);

        _mover.SetRelay(driver, venicle);
        _actions.AddAction(driver, ref component.EjectActionEntity, component.EjectAction, venicle);
    }

    private void CleanupDriver(EntityUid venicle, EntityUid driver)
    {
        if (TryComp<VenicleDriverComponent>(driver, out var driverComponent) && driverComponent.Venicle == venicle)
            RemComp<VenicleDriverComponent>(driver);

        if (TryComp<RelayInputMoverComponent>(driver, out var relay) && relay.RelayEntity == venicle)
            RemComp<RelayInputMoverComponent>(driver);

        _actions.RemoveProvidedActions(driver, venicle);
    }
}
