using System.Diagnostics.CodeAnalysis;
using Content.Shared.ActionBlocker;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.Vehicle.Systems;

public sealed partial class VehicleRefuelingSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SolutionTransferSystem _solutionTransfer = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleFuelPortComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<VehicleFuelTankComponent, SolutionTransferAttemptEvent>(OnTransferAttempt);
    }

    public bool CanAttemptRefuel(Entity<VehicleFuelPortComponent> port, EntityUid user, EntityUid used)
    {
        if (!TryGetTransferParts(port, used, out _, out var sourceSolution, out _, out _, out var tankSolution) ||
            sourceSolution.Volume <= 0 ||
            tankSolution.AvailableVolume <= 0 ||
            !_actionBlocker.CanInteract(user, port.Owner) ||
            !_actionBlocker.CanUseHeldEntity(user, used) ||
            !_interaction.InRangeUnobstructed(user, port.Owner))
        {
            return false;
        }

        return true;
    }

    private void OnInteractUsing(Entity<VehicleFuelPortComponent> port, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            !CanAttemptRefuel(port, args.User, args.Used) ||
            !TryGetTransferParts(port,
                args.Used,
                out var source,
                out var sourceSolution,
                out var transferAmount,
                out var target,
                out _))
        {
            return;
        }

        args.Handled = true;

        var tank = Comp<VehicleFuelTankComponent>(port.Comp.Vehicle);
        if (!ContainsOnlyFuel(sourceSolution, tank))
        {
            _popup.PopupClient(Loc.GetString("vehicle-fuel-tank-gasoline-only"), args.User, args.User);
            return;
        }

        var data = new SolutionTransferData(
            args.User,
            args.Used,
            source.Value,
            port.Comp.Vehicle,
            target.Value,
            transferAmount);

        var transferred = _solutionTransfer.Transfer(data);
        if (transferred <= 0)
            return;

        _popup.PopupClient(
            Loc.GetString("vehicle-fuel-tank-refilled", ("amount", transferred)),
            args.User,
            args.User);
    }

    private void OnTransferAttempt(Entity<VehicleFuelTankComponent> tank, ref SolutionTransferAttemptEvent args)
    {
        if (args.CancelReason != null)
            return;

        if (args.From == tank.Owner)
        {
            args.Cancel(Loc.GetString("vehicle-fuel-tank-cannot-drain"));
            return;
        }

        if (args.To != tank.Owner)
            return;

        if (!TryGetTransferSource(args.From, out _, out var sourceSolution, out _) ||
            !ContainsOnlyFuel(sourceSolution, tank.Comp))
        {
            args.Cancel(Loc.GetString("vehicle-fuel-tank-gasoline-only"));
        }
    }

    private bool TryGetTransferParts(
        Entity<VehicleFuelPortComponent> port,
        EntityUid sourceUid,
        [NotNullWhen(true)] out Entity<SolutionComponent>? source,
        [NotNullWhen(true)] out Solution? sourceSolution,
        out FixedPoint2 transferAmount,
        [NotNullWhen(true)] out Entity<SolutionComponent>? target,
        [NotNullWhen(true)] out Solution? targetSolution)
    {
        source = null;
        sourceSolution = null;
        transferAmount = FixedPoint2.Zero;
        target = null;
        targetSolution = null;

        if (!TryGetTransferSource(sourceUid, out source, out sourceSolution, out transferAmount) ||
            !TryComp(port.Comp.Vehicle, out VehicleFuelTankComponent? tank) ||
            !_solution.TryGetSolution(port.Comp.Vehicle, tank.Solution, out target, out targetSolution))
        {
            return false;
        }

        return true;
    }

    private bool TryGetTransferSource(
        EntityUid sourceUid,
        [NotNullWhen(true)] out Entity<SolutionComponent>? source,
        [NotNullWhen(true)] out Solution? sourceSolution,
        out FixedPoint2 transferAmount)
    {
        source = null;
        sourceSolution = null;
        transferAmount = FixedPoint2.Zero;

        if (TryComp(sourceUid, out InjectorComponent? injector))
        {
            if (!_prototype.Resolve(injector.ActiveModeProtoId, out var mode) ||
                !mode.Behavior.HasFlag(InjectorBehavior.Inject) ||
                !_solution.TryGetSolution(sourceUid, injector.SolutionName, out source, out sourceSolution))
            {
                return false;
            }

            transferAmount = injector.CurrentTransferAmount ?? sourceSolution.Volume;
            return true;
        }

        if (!TryComp(sourceUid, out SolutionTransferComponent? transfer) ||
            !transfer.CanSend ||
            !_solution.TryGetDrainableSolution(sourceUid, out source, out sourceSolution))
        {
            return false;
        }

        transferAmount = transfer.TransferAmount;
        return true;
    }

    private static bool ContainsOnlyFuel(Solution solution, VehicleFuelTankComponent tank)
    {
        if (solution.Volume <= 0)
            return false;

        foreach (var (reagent, _) in solution.Contents)
        {
            if (reagent.Prototype != tank.FuelReagent)
                return false;
        }

        return true;
    }
}
