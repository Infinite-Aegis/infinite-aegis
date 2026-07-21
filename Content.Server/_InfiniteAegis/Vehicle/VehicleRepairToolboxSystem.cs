using Content.Server.Popups;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;

namespace Content.Server.Vehicle;

public sealed partial class VehicleRepairToolboxSystem : EntitySystem
{
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleRepairToolboxComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<VehicleRepairToolboxComponent, VehicleRepairDoAfterEvent>(OnRepair);
        SubscribeLocalEvent<VehicleRepairToolboxComponent, DoAfterAttemptEvent<VehicleRepairDoAfterEvent>>(OnRepairAttempt);
    }

    private void OnAfterInteract(Entity<VehicleRepairToolboxComponent> toolbox, ref AfterInteractEvent args)
    {
        if (args.Target is not { } target ||
            !TryComp<DamageableComponent>(target, out var damageable) ||
            !TryComp<VehicleDamageComponent>(target, out var vehicleDamage))
        {
            return;
        }

        args.Handled = true;

        if (!args.CanReach)
            return;

        if (!TryComp<LimitedChargesComponent>(toolbox, out var charges) ||
            !_charges.HasCharges((toolbox.Owner, charges), toolbox.Comp.ChargeCost))
        {
            _popup.PopupEntity(Loc.GetString("vehicle-repair-toolbox-empty"), toolbox.Owner, args.User);
            return;
        }

        if (!CanRepair((target, damageable, vehicleDamage)))
        {
            _popup.PopupEntity(Loc.GetString("vehicle-repair-toolbox-no-damage"), target, args.User);
            return;
        }

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            args.User,
            toolbox.Comp.RepairDuration,
            new VehicleRepairDoAfterEvent(),
            toolbox.Owner,
            target,
            toolbox.Owner)
        {
            AttemptFrequency = AttemptFrequency.EveryTick,
            Broadcast = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs))
            _popup.PopupEntity(Loc.GetString("vehicle-repair-toolbox-start"), target, args.User, PopupType.Medium);
    }

    private void OnRepair(Entity<VehicleRepairToolboxComponent> toolbox, ref VehicleRepairDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target ||
            !TryComp<LimitedChargesComponent>(toolbox, out var charges) ||
            !_charges.HasCharges((toolbox.Owner, charges), toolbox.Comp.ChargeCost) ||
            !TryComp<DamageableComponent>(target, out var damageable) ||
            !TryComp<VehicleDamageComponent>(target, out var vehicleDamage) ||
            !CanRepair((target, damageable, vehicleDamage)))
        {
            return;
        }

        args.Handled = true;
        var totalDamage = _damageable.GetTotalDamage((target, damageable)).Float();
        var repair = CalculateRepairAmount(totalDamage, vehicleDamage.MaximumDamage);
        var healed = _damageable.HealEvenly(
            new Entity<DamageableComponent?>(target, damageable),
            -FixedPoint2.New(repair),
            origin: args.User);

        if (healed.Empty)
            return;

        _charges.TryUseCharges((toolbox.Owner, charges), toolbox.Comp.ChargeCost);
        _popup.PopupEntity(Loc.GetString("vehicle-repair-toolbox-finish"), target, args.User, PopupType.Medium);
    }

    private void OnRepairAttempt(
        Entity<VehicleRepairToolboxComponent> toolbox,
        ref DoAfterAttemptEvent<VehicleRepairDoAfterEvent> args)
    {
        if (args.DoAfter.Args.Target is not { } target ||
            !TryComp<LimitedChargesComponent>(toolbox, out var charges) ||
            !_charges.HasCharges((toolbox.Owner, charges), toolbox.Comp.ChargeCost) ||
            !TryComp<DamageableComponent>(target, out var damageable) ||
            !TryComp<VehicleDamageComponent>(target, out var vehicleDamage) ||
            !CanRepair((target, damageable, vehicleDamage)))
        {
            args.Cancel();
        }
    }

    private bool CanRepair(Entity<DamageableComponent, VehicleDamageComponent> target)
    {
        return target.Comp2.MaximumDamage > 0f &&
               _damageable.GetTotalDamage((target.Owner, target.Comp1)) > FixedPoint2.Zero;
    }

    public static float CalculateRepairAmount(float totalDamage, float maximumDamage)
    {
        if (maximumDamage <= 0f || totalDamage <= 0f)
            return 0f;

        var repair = maximumDamage / 5f;
        if (totalDamage > maximumDamage)
            repair += totalDamage - maximumDamage;

        return repair;
    }
}
