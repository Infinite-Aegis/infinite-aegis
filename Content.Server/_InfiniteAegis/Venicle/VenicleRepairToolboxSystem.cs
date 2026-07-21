using Content.Server.Popups;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Venicle;
using Content.Shared.Venicle.Components;

namespace Content.Server.Venicle;

public sealed partial class VenicleRepairToolboxSystem : EntitySystem
{
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VenicleRepairToolboxComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<VenicleRepairToolboxComponent, VenicleRepairDoAfterEvent>(OnRepair);
        SubscribeLocalEvent<VenicleRepairToolboxComponent, DoAfterAttemptEvent<VenicleRepairDoAfterEvent>>(OnRepairAttempt);
    }

    private void OnAfterInteract(Entity<VenicleRepairToolboxComponent> toolbox, ref AfterInteractEvent args)
    {
        if (args.Target is not { } target ||
            !TryComp<DamageableComponent>(target, out var damageable) ||
            !TryComp<VenicleDamageComponent>(target, out var venicleDamage))
        {
            return;
        }

        args.Handled = true;

        if (!args.CanReach)
            return;

        if (!TryComp<LimitedChargesComponent>(toolbox, out var charges) ||
            !_charges.HasCharges((toolbox.Owner, charges), toolbox.Comp.ChargeCost))
        {
            _popup.PopupEntity(Loc.GetString("venicle-repair-toolbox-empty"), toolbox.Owner, args.User);
            return;
        }

        if (!CanRepair((target, damageable, venicleDamage)))
        {
            _popup.PopupEntity(Loc.GetString("venicle-repair-toolbox-no-damage"), target, args.User);
            return;
        }

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            args.User,
            toolbox.Comp.RepairDuration,
            new VenicleRepairDoAfterEvent(),
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
            _popup.PopupEntity(Loc.GetString("venicle-repair-toolbox-start"), target, args.User, PopupType.Medium);
    }

    private void OnRepair(Entity<VenicleRepairToolboxComponent> toolbox, ref VenicleRepairDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target ||
            !TryComp<LimitedChargesComponent>(toolbox, out var charges) ||
            !_charges.HasCharges((toolbox.Owner, charges), toolbox.Comp.ChargeCost) ||
            !TryComp<DamageableComponent>(target, out var damageable) ||
            !TryComp<VenicleDamageComponent>(target, out var venicleDamage) ||
            !CanRepair((target, damageable, venicleDamage)))
        {
            return;
        }

        args.Handled = true;
        var totalDamage = _damageable.GetTotalDamage((target, damageable)).Float();
        var repair = CalculateRepairAmount(totalDamage, venicleDamage.MaximumDamage);
        var healed = _damageable.HealEvenly(
            new Entity<DamageableComponent?>(target, damageable),
            -FixedPoint2.New(repair),
            origin: args.User);

        if (healed.Empty)
            return;

        _charges.TryUseCharges((toolbox.Owner, charges), toolbox.Comp.ChargeCost);
        _popup.PopupEntity(Loc.GetString("venicle-repair-toolbox-finish"), target, args.User, PopupType.Medium);
    }

    private void OnRepairAttempt(
        Entity<VenicleRepairToolboxComponent> toolbox,
        ref DoAfterAttemptEvent<VenicleRepairDoAfterEvent> args)
    {
        if (args.DoAfter.Args.Target is not { } target ||
            !TryComp<LimitedChargesComponent>(toolbox, out var charges) ||
            !_charges.HasCharges((toolbox.Owner, charges), toolbox.Comp.ChargeCost) ||
            !TryComp<DamageableComponent>(target, out var damageable) ||
            !TryComp<VenicleDamageComponent>(target, out var venicleDamage) ||
            !CanRepair((target, damageable, venicleDamage)))
        {
            args.Cancel();
        }
    }

    private bool CanRepair(Entity<DamageableComponent, VenicleDamageComponent> target)
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
