using System.Diagnostics.CodeAnalysis;
using Content.Shared.ActionBlocker;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Venicle.Components;

namespace Content.Shared.Venicle.Systems;

public sealed partial class VenicleFuelSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SolutionTransferSystem _solutionTransfer = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VenicleFuelPortComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<VenicleFuelTankComponent, SolutionTransferAttemptEvent>(OnTransferAttempt);
    }

    public bool CanAttemptRefuel(Entity<VenicleFuelPortComponent> port, EntityUid user, EntityUid used)
    {
        if (!TryGetTransferParts(port, used, out var transfer, out _, out var sourceSolution, out _, out var tankSolution) ||
            !transfer.CanSend ||
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

    private void OnInteractUsing(Entity<VenicleFuelPortComponent> port, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            !CanAttemptRefuel(port, args.User, args.Used) ||
            !TryGetTransferParts(port,
                args.Used,
                out var transfer,
                out var source,
                out var sourceSolution,
                out var target,
                out _))
        {
            return;
        }

        args.Handled = true;

        var tank = Comp<VenicleFuelTankComponent>(port.Comp.Venicle);
        if (!ContainsOnlyFuel(sourceSolution, tank))
        {
            _popup.PopupClient(Loc.GetString("venicle-fuel-tank-gasoline-only"), args.User, args.User);
            return;
        }

        var data = new SolutionTransferData(
            args.User,
            args.Used,
            source.Value,
            port.Comp.Venicle,
            target.Value,
            transfer.TransferAmount);

        var transferred = _solutionTransfer.Transfer(data);
        if (transferred <= 0)
            return;

        _popup.PopupClient(
            Loc.GetString("venicle-fuel-tank-refilled", ("amount", transferred)),
            args.User,
            args.User);
    }

    private void OnTransferAttempt(Entity<VenicleFuelTankComponent> tank, ref SolutionTransferAttemptEvent args)
    {
        if (args.CancelReason != null)
            return;

        if (args.From == tank.Owner)
        {
            args.Cancel(Loc.GetString("venicle-fuel-tank-cannot-drain"));
            return;
        }

        if (args.To != tank.Owner)
            return;

        if (!_solution.TryGetDrainableSolution(args.From, out _, out var sourceSolution) ||
            !ContainsOnlyFuel(sourceSolution, tank.Comp))
        {
            args.Cancel(Loc.GetString("venicle-fuel-tank-gasoline-only"));
        }
    }

    private bool TryGetTransferParts(
        Entity<VenicleFuelPortComponent> port,
        EntityUid sourceUid,
        [NotNullWhen(true)] out SolutionTransferComponent? transfer,
        [NotNullWhen(true)] out Entity<SolutionComponent>? source,
        [NotNullWhen(true)] out Solution? sourceSolution,
        [NotNullWhen(true)] out Entity<SolutionComponent>? target,
        [NotNullWhen(true)] out Solution? targetSolution)
    {
        transfer = null;
        source = null;
        sourceSolution = null;
        target = null;
        targetSolution = null;

        if (!TryComp(sourceUid, out transfer) ||
            !_solution.TryGetDrainableSolution(sourceUid, out source, out sourceSolution) ||
            !TryComp(port.Comp.Venicle, out VenicleFuelTankComponent? tank) ||
            !_solution.TryGetSolution(port.Comp.Venicle, tank.Solution, out target, out targetSolution))
        {
            return false;
        }

        return true;
    }

    private static bool ContainsOnlyFuel(Solution solution, VenicleFuelTankComponent tank)
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
