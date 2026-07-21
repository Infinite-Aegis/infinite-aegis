using Content.IntegrationTests.Fixtures;
using Content.Server.Venicle;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Power.Components;
using Content.Shared.Storage;
using Content.Shared.Venicle;
using Content.Shared.Venicle.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._InfiniteAegis.Venicle;

[TestFixture]
[TestOf(typeof(VenicleRepairToolboxSystem))]
public sealed class VenicleRepairTest : GameTest
{
    private const string StructuralDamage = "Structural";

    [Test]
    public async Task ToolboxRepairsAndConsumesCharges()
    {
        var server = Pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var systemManager = server.ResolveDependency<IEntitySystemManager>();
        var map = await Pair.CreateTestMap();
        EntityUid moskvich = default;
        EntityUid toolbox = default;
        EntityUid user = default;

        await server.WaitPost(() =>
        {
            moskvich = entityManager.SpawnEntity("Moskvich", map.MapCoords);
            toolbox = entityManager.SpawnEntity("VenicleRepairToolbox", map.MapCoords);
            user = entityManager.SpawnEntity(null, map.MapCoords);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var damageableSystem = systemManager.GetEntitySystem<DamageableSystem>();
            var chargesSystem = systemManager.GetEntitySystem<SharedChargesSystem>();
            var structural = prototypeManager.Index<DamageTypePrototype>(StructuralDamage);
            var damageable = entityManager.GetComponent<DamageableComponent>(moskvich);
            var toolboxComponent = entityManager.GetComponent<VenicleRepairToolboxComponent>(toolbox);
            var charges = entityManager.GetComponent<LimitedChargesComponent>(toolbox);

            Assert.Multiple(() =>
            {
                Assert.That(toolboxComponent.RepairDuration, Is.EqualTo(10f));
                Assert.That(toolboxComponent.ChargeCost, Is.EqualTo(20));
                Assert.That(chargesSystem.GetCurrentCharges((toolbox, charges, null)), Is.EqualTo(100));
                Assert.That(entityManager.HasComponent<BatteryComponent>(toolbox), Is.False);
                Assert.That(entityManager.HasComponent<ItemSlotsComponent>(toolbox), Is.False);
                Assert.That(entityManager.HasComponent<StorageComponent>(toolbox), Is.False);
                Assert.That(VenicleRepairToolboxSystem.CalculateRepairAmount(450f, 500f), Is.EqualTo(100f));
                Assert.That(VenicleRepairToolboxSystem.CalculateRepairAmount(1000f, 500f), Is.EqualTo(600f));
            });

            damageableSystem.ChangeDamage(moskvich, new DamageSpecifier(structural, 1000), true);
            RaiseRepairEvent(entityManager, toolbox, moskvich, user);

            Assert.Multiple(() =>
            {
                Assert.That(damageableSystem.GetTotalDamage((moskvich, damageable)), Is.EqualTo(FixedPoint2.New(400)));
                Assert.That(chargesSystem.GetCurrentCharges((toolbox, charges, null)), Is.EqualTo(80));
            });

            damageableSystem.ChangeDamage(moskvich, new DamageSpecifier(structural, 100), true);
            chargesSystem.SetCharges((toolbox, charges), 0);
            RaiseRepairEvent(entityManager, toolbox, moskvich, user);

            Assert.Multiple(() =>
            {
                Assert.That(damageableSystem.GetTotalDamage((moskvich, damageable)), Is.EqualTo(FixedPoint2.New(500)));
                Assert.That(chargesSystem.GetCurrentCharges((toolbox, charges, null)), Is.Zero);
            });

            var interaction = new AfterInteractEvent(
                user,
                toolbox,
                moskvich,
                entityManager.GetComponent<TransformComponent>(moskvich).Coordinates,
                true);
            entityManager.EventBus.RaiseLocalEvent(toolbox, interaction);

            Assert.Multiple(() =>
            {
                Assert.That(interaction.Handled, Is.True);
                Assert.That(entityManager.HasComponent<DoAfterComponent>(user), Is.False);
            });
        });
    }

    private static void RaiseRepairEvent(
        IEntityManager entityManager,
        EntityUid toolbox,
        EntityUid target,
        EntityUid user)
    {
        var repairEvent = new VenicleRepairDoAfterEvent();
        var args = new DoAfterArgs(entityManager, user, 10f, repairEvent, toolbox, target, toolbox);
        repairEvent.DoAfter = new Content.Shared.DoAfter.DoAfter(0, args, TimeSpan.Zero);
        entityManager.EventBus.RaiseLocalEvent(toolbox, repairEvent);
    }
}
