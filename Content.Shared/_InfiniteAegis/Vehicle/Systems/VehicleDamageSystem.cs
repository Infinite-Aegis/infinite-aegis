using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Vehicle.Components;

namespace Content.Shared.Vehicle.Systems;

public sealed partial class VehicleDamageSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleDamageComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<VehicleDamageComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnMapInit(Entity<VehicleDamageComponent> ent, ref MapInitEvent args)
    {
        RefreshState(ent.Owner, ent.Comp);
    }

    private void OnDamageChanged(EntityUid uid, VehicleDamageComponent component, DamageChangedEvent args)
    {
        UpdateState((uid, component), args.Damageable);
    }

    public bool RefreshState(EntityUid uid, VehicleDamageComponent? component = null)
    {
        if (!Resolve(uid, ref component) || !TryComp<DamageableComponent>(uid, out var damageable))
            return false;

        UpdateState((uid, component), damageable);
        return true;
    }

    private void UpdateState(Entity<VehicleDamageComponent> ent, DamageableComponent damageable)
    {
        var maximumDamage = ent.Comp.MaximumDamage;
        var damage = _damageable.GetTotalDamage((ent.Owner, damageable)).Float();
        ent.Comp.State = GetState(damage, maximumDamage);
    }

    public static VehicleDamageState GetState(float damage, float maximumDamage)
    {
        if (maximumDamage <= 0f || damage >= maximumDamage)
            return VehicleDamageState.Disabled;

        var fraction = MathF.Max(0f, damage) / maximumDamage;

        if (fraction >= 0.8f)
            return VehicleDamageState.Damaged80;

        if (fraction >= 0.6f)
            return VehicleDamageState.Damaged60;

        if (fraction >= 0.4f)
            return VehicleDamageState.Damaged40;

        if (fraction >= 0.2f)
            return VehicleDamageState.Damaged20;

        return VehicleDamageState.Normal;
    }
}
