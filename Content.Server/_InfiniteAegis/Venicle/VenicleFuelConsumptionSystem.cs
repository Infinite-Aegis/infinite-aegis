using System.Numerics;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Venicle.Components;

namespace Content.Server.Venicle;

public sealed partial class VenicleFuelConsumptionSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VenicleFuelTankComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<VenicleFuelTankComponent, SolutionChangedEvent>(OnSolutionChanged);
    }

    private void OnMove(Entity<VenicleFuelTankComponent> tank, ref MoveEvent args)
    {
        var consumptionRate = tank.Comp.FuelConsumptionPerDistance;
        var maximumSegment = tank.Comp.MaximumTrackedSegmentLength;
        if (!tank.Comp.HasFuel ||
            args.ParentChanged ||
            !float.IsFinite(consumptionRate) ||
            consumptionRate <= 0f ||
            !float.IsFinite(maximumSegment) ||
            maximumSegment <= 0f)
        {
            return;
        }

        var distance = Vector2.Distance(args.OldPosition.Position, args.NewPosition.Position);
        if (!float.IsFinite(distance) ||
            distance <= 0f ||
            distance > maximumSegment)
        {
            return;
        }

        tank.Comp.PendingFuelConsumption += distance * consumptionRate;
        if (!float.IsFinite(tank.Comp.PendingFuelConsumption))
        {
            tank.Comp.PendingFuelConsumption = 0f;
            return;
        }

        var cents = (int) MathF.Min(
            MathF.Floor(tank.Comp.PendingFuelConsumption / FixedPoint2.Epsilon.Float()),
            int.MaxValue);
        if (cents <= 0)
            return;

        if (!_solution.TryGetSolution(tank.Owner, tank.Comp.Solution, out var solutionEntity, out var solution))
        {
            SetHasFuel(tank, false);
            return;
        }

        var available = solution.GetTotalPrototypeQuantity(tank.Comp.FuelReagent);
        if (available <= FixedPoint2.Zero)
        {
            SetHasFuel(tank, false);
            return;
        }

        var requested = FixedPoint2.FromCents(cents);
        var removed = FixedPoint2.Min(requested, available);
        tank.Comp.PendingFuelConsumption = MathF.Max(0f, tank.Comp.PendingFuelConsumption - removed.Float());
        if (removed < requested)
            tank.Comp.PendingFuelConsumption = 0f;

        _solution.RemoveReagent(solutionEntity.Value, tank.Comp.FuelReagent, removed);
    }

    private void OnSolutionChanged(Entity<VenicleFuelTankComponent> tank, ref SolutionChangedEvent args)
    {
        if (args.Solution.Comp.Id != tank.Comp.Solution)
            return;

        UpdateFuelState(tank);
    }

    private void UpdateFuelState(Entity<VenicleFuelTankComponent> tank)
    {
        var hasFuel = _solution.TryGetSolution(tank.Owner, tank.Comp.Solution, out _, out var solution) &&
                      solution.GetTotalPrototypeQuantity(tank.Comp.FuelReagent) > FixedPoint2.Zero;
        SetHasFuel(tank, hasFuel);
    }

    private void SetHasFuel(Entity<VenicleFuelTankComponent> tank, bool hasFuel)
    {
        if (!hasFuel)
            tank.Comp.PendingFuelConsumption = 0f;

        if (tank.Comp.HasFuel == hasFuel)
            return;

        tank.Comp.HasFuel = hasFuel;
        Dirty(tank);
    }
}
