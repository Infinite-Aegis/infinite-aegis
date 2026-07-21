using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Venicle.Components;

namespace Content.Shared.Venicle.Systems;

public sealed partial class VenicleDamageSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VenicleDamageComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<VenicleDamageComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnMapInit(Entity<VenicleDamageComponent> ent, ref MapInitEvent args)
    {
        if (TryComp<DamageableComponent>(ent, out var damageable))
            UpdateState(ent, damageable);
    }

    private void OnDamageChanged(EntityUid uid, VenicleDamageComponent component, DamageChangedEvent args)
    {
        UpdateState((uid, component), args.Damageable);
    }

    private void UpdateState(Entity<VenicleDamageComponent> ent, DamageableComponent damageable)
    {
        var maximumDamage = ent.Comp.MaximumDamage;
        var damage = _damageable.GetTotalDamage((ent.Owner, damageable)).Float();
        ent.Comp.State = GetState(damage, maximumDamage);
    }

    public static VenicleDamageState GetState(float damage, float maximumDamage)
    {
        if (maximumDamage <= 0f || damage >= maximumDamage)
            return VenicleDamageState.Disabled;

        var fraction = MathF.Max(0f, damage) / maximumDamage;

        if (fraction >= 0.8f)
            return VenicleDamageState.Damaged80;

        if (fraction >= 0.6f)
            return VenicleDamageState.Damaged60;

        if (fraction >= 0.4f)
            return VenicleDamageState.Damaged40;

        if (fraction >= 0.2f)
            return VenicleDamageState.Damaged20;

        return VenicleDamageState.Normal;
    }
}
