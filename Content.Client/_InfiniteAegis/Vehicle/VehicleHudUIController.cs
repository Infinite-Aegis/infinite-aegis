using Content.Client.Chemistry.Containers.EntitySystems;
using Content.Client.UserInterface.Systems.Inventory.Widgets;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Vehicle.Components;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Client._InfiniteAegis.Vehicle;

public sealed partial class VehicleHudUIController : UIController
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _player = default!;

    [UISystemDependency] private readonly DamageableSystem _damageable = default!;
    [UISystemDependency] private readonly SolutionContainerSystem _solution = default!;

    private EntityUid? _vehicle;
    private VehicleHudState? _lastState;

    private VehicleHud? Hud => UIManager.GetActiveUIWidgetOrNull<InventoryGui>()?.VehicleHud;

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (Hud is not { } hud)
            return;

        if (!TryGetDriverVehicle(out var vehicle))
        {
            Hide(hud);
            return;
        }

        if (_vehicle != vehicle)
        {
            _vehicle = vehicle;
            _lastState = null;
        }

        hud.Visible = true;

        var state = GetState(vehicle);
        if (_lastState == state)
            return;

        _lastState = state;
        hud.UpdateValues(state.Speed, state.Durability, state.Fuel, state.MaximumFuel, state.Gear);
    }

    private bool TryGetDriverVehicle(out EntityUid vehicle)
    {
        vehicle = default;

        if (_player.LocalEntity is not { } player ||
            !_entities.TryGetComponent(player, out VehicleOccupantComponent? occupant) ||
            !_entities.EntityExists(occupant.Vehicle) ||
            !_entities.TryGetComponent(occupant.Vehicle, out VehicleComponent? vehicleComponent))
        {
            return false;
        }

        foreach (var seat in vehicleComponent.Seats)
        {
            if (seat.Id != occupant.SeatId || !seat.Driver)
                continue;

            vehicle = occupant.Vehicle;
            return true;
        }

        return false;
    }

    private VehicleHudState GetState(EntityUid vehicle)
    {
        var speed = 0f;
        if (_entities.TryGetComponent(vehicle, out PhysicsComponent? physics))
        {
            speed = physics.LinearVelocity.Length();
            if (!float.IsFinite(speed))
                speed = 0f;
        }

        speed = MathF.Round(speed, 1);

        var durability = 100;
        if (_entities.TryGetComponent(vehicle, out VehicleDamageComponent? vehicleDamage) &&
            _entities.TryGetComponent(vehicle, out DamageableComponent? damageable))
        {
            if (vehicleDamage.MaximumDamage <= 0f)
            {
                durability = 0;
            }
            else
            {
                var damage = MathF.Max(0f, _damageable.GetTotalDamage((vehicle, damageable)).Float());
                var fraction = Math.Clamp(1f - damage / vehicleDamage.MaximumDamage, 0f, 1f);
                durability = (int) MathF.Round(fraction * 100f);
            }
        }

        var fuel = FixedPoint2.Zero;
        var maximumFuel = FixedPoint2.Zero;
        if (_entities.TryGetComponent(vehicle, out VehicleFuelTankComponent? tank) &&
            _solution.TryGetSolution(vehicle, tank.Solution, out _, out var tankSolution))
        {
            fuel = tankSolution.GetTotalPrototypeQuantity(tank.FuelReagent);
            maximumFuel = tankSolution.MaxVolume;
        }

        var gear = 1;
        if (_entities.TryGetComponent(vehicle, out VehicleMovementComponent? movement))
            gear = movement.CurrentGear;

        return new VehicleHudState(speed, durability, fuel, maximumFuel, gear);
    }

    private void Hide(VehicleHud hud)
    {
        hud.Visible = false;
        _vehicle = null;
        _lastState = null;
    }

    private readonly record struct VehicleHudState(
        float Speed,
        int Durability,
        FixedPoint2 Fuel,
        FixedPoint2 MaximumFuel,
        int Gear);
}
