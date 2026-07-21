using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Vehicle;

public sealed partial class VehicleDeserializationSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private VehicleDamageSystem _damage = default!;
    [Dependency] private VehicleFuelTankSystem _fuelTank = default!;
    [Dependency] private VehicleMovementSystem _movement = default!;
    [Dependency] private VehicleSeatSystem _seats = default!;

    public bool TryRestore(EntityUid vehicle)
    {
        return RestorePhysics(vehicle) &&
               RestoreMovement(vehicle) &&
               RestoreSeatMarkers(vehicle) &&
               RestoreFuelSolutions(vehicle) &&
               RestoreFuelPort(vehicle) &&
               RestoreDamageState(vehicle);
    }

    private bool RestorePhysics(EntityUid vehicle)
    {
        return !HasComp<PhysicsComponent>(vehicle) || _physics.WakeBody(vehicle);
    }

    private bool RestoreMovement(EntityUid vehicle)
    {
        return !TryComp(vehicle, out VehicleMovementComponent? component) ||
               _movement.RefreshInertia(vehicle, component);
    }

    private bool RestoreSeatMarkers(EntityUid vehicle)
    {
        return !TryComp(vehicle, out VehicleComponent? component) ||
               _seats.RebuildSeatMarkers(vehicle, component);
    }

    private bool RestoreFuelSolutions(EntityUid vehicle)
    {
        if (!TryComp(vehicle, out VehicleFuelTankComponent? fuelTank))
            return true;

        if (!TryComp(vehicle, out SolutionManagerComponent? manager) ||
            !_container.TryGetContainer(vehicle, manager.Container, out var container))
        {
            return false;
        }

        var solutions = new List<Entity<SolutionComponent>>();
        var solutionIds = new HashSet<string>();

        foreach (var contained in container.ContainedEntities)
        {
            if (!TryComp(contained, out SolutionComponent? solution))
                continue;

            if (!solutionIds.Add(solution.Id))
                return false;

            solutions.Add((contained, solution));
        }

        if (!solutionIds.Contains(fuelTank.Solution))
            return false;

        foreach (var solution in solutions)
        {
            if (!_container.Remove(solution.Owner, container, reparent: false, force: true))
                return false;
        }

        foreach (var solution in solutions)
        {
            if (!_container.Insert(solution.Owner, container, force: true))
                return false;

            _solution.UpdateChemicals(solution);
        }

        return true;
    }

    private bool RestoreFuelPort(EntityUid vehicle)
    {
        return !TryComp(vehicle, out VehicleFuelTankComponent? component) ||
               _fuelTank.RebuildPortMarker(vehicle, component);
    }

    private bool RestoreDamageState(EntityUid vehicle)
    {
        return !TryComp(vehicle, out VehicleDamageComponent? component) ||
               _damage.RefreshState(vehicle, component);
    }
}
