using Content.IntegrationTests.Fixtures;
using Content.Server.Destructible;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._InfiniteAegis.Vehicle;

[TestFixture]
[TestOf(typeof(VehicleDamageComponent))]
public sealed class VehicleDamageTest : GameTest
{
    private const string StructuralDamage = "Structural";

    [Test]
    public async Task MoskvichDamageStagesAndNoDestruction()
    {
        var server = Pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var systemManager = server.ResolveDependency<IEntitySystemManager>();
        var map = await Pair.CreateTestMap();
        EntityUid moskvich = default;

        await server.WaitPost(() => moskvich = entityManager.SpawnEntity("Moskvich", map.MapCoords));
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var damageable = entityManager.GetComponent<DamageableComponent>(moskvich);
            var injurable = entityManager.GetComponent<InjurableComponent>(moskvich);
            var vehicleDamage = entityManager.GetComponent<VehicleDamageComponent>(moskvich);
            var damageableSystem = systemManager.GetEntitySystem<DamageableSystem>();
            var structural = prototypeManager.Index<DamageTypePrototype>(StructuralDamage);

            Assert.Multiple(() =>
            {
                Assert.That(damageable.DamageModifierSetId.ToString(), Is.EqualTo("StructuralMetallic"));
                Assert.That(injurable.DamageContainer.ToString(), Is.EqualTo("StructuralInorganic"));
                Assert.That(entityManager.HasComponent<DestructibleComponent>(moskvich), Is.False);
                Assert.That(vehicleDamage.MaximumDamage, Is.EqualTo(500f));
            });
            AssertStage(vehicleDamage, VehicleDamageState.Normal, 1f, 1f, true);

            damageableSystem.ChangeDamage(moskvich, new DamageSpecifier(structural, 99), true);
            AssertStage(vehicleDamage, VehicleDamageState.Normal, 1f, 1f, true);

            damageableSystem.ChangeDamage(moskvich, new DamageSpecifier(structural, 1), true);
            AssertStage(vehicleDamage, VehicleDamageState.Damaged20, 0.9f, 1f, true);

            damageableSystem.ChangeDamage(moskvich, new DamageSpecifier(structural, 100), true);
            AssertStage(vehicleDamage, VehicleDamageState.Damaged40, 0.8f, 1f, true);

            damageableSystem.ChangeDamage(moskvich, new DamageSpecifier(structural, 100), true);
            AssertStage(vehicleDamage, VehicleDamageState.Damaged60, 0.7f, 0.9f, true);

            damageableSystem.ChangeDamage(moskvich, new DamageSpecifier(structural, 100), true);
            AssertStage(vehicleDamage, VehicleDamageState.Damaged80, 0.6f, 0.8f, true);

            damageableSystem.ChangeDamage(moskvich, new DamageSpecifier(structural, 100), true);
            AssertStage(vehicleDamage, VehicleDamageState.Disabled, 0f, 0f, false);

            var fuelTank = entityManager.GetComponent<VehicleFuelTankComponent>(moskvich);
            var mover = entityManager.GetComponent<InputMoverComponent>(moskvich);
            var physics = entityManager.GetComponent<PhysicsComponent>(moskvich);
            var physicsSystem = systemManager.GetEntitySystem<SharedPhysicsSystem>();
            var movementSystem = systemManager.GetEntitySystem<VehicleMovementSystem>();
            fuelTank.HasFuel = true;
            mover.HeldMoveButtons = MoveButtons.Up;
            physicsSystem.SetBodyStatus(moskvich, physics, BodyStatus.OnGround);
            var forceBefore = physics.Force;
            movementSystem.UpdateBeforeSolve(false, 1f / 30f);
            Assert.That(physics.Force, Is.EqualTo(forceBefore));

            damageableSystem.ChangeDamage(moskvich, new DamageSpecifier(structural, 100), true);

            Assert.Multiple(() =>
            {
                Assert.That(entityManager.EntityExists(moskvich), Is.True);
                Assert.That(damageableSystem.GetTotalDamage((moskvich, damageable)), Is.EqualTo(FixedPoint2.New(600)));
            });
            AssertStage(vehicleDamage, VehicleDamageState.Disabled, 0f, 0f, false);

            damageableSystem.ChangeDamage(moskvich, new DamageSpecifier(structural, -500), true);
            AssertStage(vehicleDamage, VehicleDamageState.Damaged20, 0.9f, 1f, true);
        });
    }

    [Test]
    public async Task GasolineCanisterStartsFullAndCloseable()
    {
        var server = Pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var systemManager = server.ResolveDependency<IEntitySystemManager>();
        var map = await Pair.CreateTestMap();
        EntityUid canister = default;

        await server.WaitPost(() => canister = entityManager.SpawnEntity("GasolineCanister", map.MapCoords));
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var openable = entityManager.GetComponent<OpenableComponent>(canister);
            var solutionSystem = systemManager.GetEntitySystem<SharedSolutionContainerSystem>();
            Entity<SolutionComponent>? solutionEntity = null;

            Assert.That(solutionSystem.ResolveSolution(canister, "drink", ref solutionEntity, out var solution), Is.True);
            Assert.That(solution, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(openable.Closeable, Is.True);
                Assert.That(openable.Opened, Is.False);
                Assert.That(entityManager.HasComponent<DrainableSolutionComponent>(canister), Is.True);
                Assert.That(entityManager.HasComponent<SolutionTransferComponent>(canister), Is.True);
                Assert.That(solution!.MaxVolume, Is.EqualTo(FixedPoint2.New(100)));
                Assert.That(solution.Volume, Is.EqualTo(FixedPoint2.New(100)));
                Assert.That(solution.GetReagentQuantity(new ReagentId("Gasoline", null)), Is.EqualTo(FixedPoint2.New(100)));
            });
        });
    }

    private static void AssertStage(
        VehicleDamageComponent component,
        VehicleDamageState state,
        float acceleration,
        float maximumSpeed,
        bool canDrive)
    {
        Assert.Multiple(() =>
        {
            Assert.That(component.State, Is.EqualTo(state));
            Assert.That(component.AccelerationModifier, Is.EqualTo(acceleration));
            Assert.That(component.MaximumSpeedModifier, Is.EqualTo(maximumSpeed));
            Assert.That(component.CanDrive, Is.EqualTo(canDrive));
        });
    }
}
