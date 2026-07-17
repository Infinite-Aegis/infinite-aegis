using System.Linq;
using System.Numerics;
using Content.Client.Popups;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Venicle;
using Content.Shared.Buckle.Components;
using Content.Shared.DragDrop;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Venicle.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._InfiniteAegis.Venicle;

[TestFixture]
[TestOf(typeof(VenicleSeatSystem))]
public sealed class VenicleSeatTests : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
        DummyTicker = false,
    };

    [SidedDependency(Side.Client)] private readonly PopupSystem _popups = null!;

    [Test]
    public async Task SeatIsReachableFromItsDoorAtEveryVehicleAngleButNotThroughBody()
    {
        var map = await Pair.CreateTestMap();
        var seatSystem = SEntMan.System<VenicleSeatSystem>();
        var interaction = SEntMan.System<SharedInteractionSystem>();
        var transform = SEntMan.System<SharedTransformSystem>();

        EntityUid player = default;
        EntityUid venicle = default;
        EntityUid marker = default;
        VenicleSeatComponent markerComponent = null!;

        await Server.WaitAssertion(() =>
        {
            player = SEntMan.SpawnEntity("MobHuman", map.MapCoords);
            venicle = SEntMan.SpawnEntity("Moskvich", map.MapCoords);
            var venicleComponent = SEntMan.GetComponent<VenicleComponent>(venicle);
            marker = venicleComponent.SeatMarkers["driver"];
            markerComponent = SEntMan.GetComponent<VenicleSeatComponent>(marker);
        });

        foreach (var degrees in new[] { 0f, 45f, 90f, 135f })
        {
            await Server.WaitAssertion(() =>
            {
                var rotation = Angle.FromDegrees(degrees);
                transform.SetWorldRotation(venicle, rotation);

                var markerPosition = transform.GetWorldPosition(marker);
                var outward = rotation.RotateVec(new Vector2(-0.3f, 0f));
                transform.SetWorldPosition(player, markerPosition + outward);
            });
            await Pair.RunTicksSync(2);

            await Server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(interaction.InRangeUnobstructed(player, marker), Is.True,
                        $"near-side marker must be reachable at {degrees} degrees");
                    Assert.That(seatSystem.CanUseSeat((marker, markerComponent), player, player), Is.True,
                        $"seat must be usable at {degrees} degrees");
                });

                var veniclePosition = transform.GetWorldPosition(venicle);
                var rotation = Angle.FromDegrees(degrees);
                var oppositeSide = rotation.RotateVec(new Vector2(1.4f, -0.45f));
                transform.SetWorldPosition(player, veniclePosition + oppositeSide);
            });
            await Pair.RunTicksSync(2);

            await Server.WaitAssertion(() =>
            {
                var body = SEntMan.GetComponent<PhysicsComponent>(venicle);
                var fixtures = SEntMan.GetComponent<FixturesComponent>(venicle);

                Assert.Multiple(() =>
                {
                    Assert.That(body.CollisionLayer & (int) CollisionGroup.InteractImpassable, Is.Not.Zero,
                        "vehicle body must retain its interaction-blocking layer");
                    Assert.That(fixtures.Fixtures.Values.All(fixture => fixture.Hard), Is.True,
                        "vehicle body fixtures must remain hard ray blockers");
                    Assert.That(seatSystem.CanUseSeat((marker, markerComponent), player, player), Is.False,
                        $"seat must not be usable through the vehicle at {degrees} degrees");
                });
            });
        }
    }

    [Test]
    public async Task DraggingAnotherPersonReservesSeatNotifiesAndInsertsIntoContainer()
    {
        var session = ServerSession;
        Assert.That(session?.AttachedEntity, Is.Not.Null);

        var occupant = session!.AttachedEntity!.Value;
        var map = await Pair.CreateTestMap();
        var transform = SEntMan.System<SharedTransformSystem>();

        EntityUid actor = default;
        EntityUid venicle = default;
        EntityUid marker = default;
        VenicleComponent venicleComponent = null!;
        VenicleSeatComponent markerComponent = null!;

        await Server.WaitAssertion(() =>
        {
            venicle = SEntMan.SpawnEntity("Moskvich", map.MapCoords);
            venicleComponent = SEntMan.GetComponent<VenicleComponent>(venicle);
            venicleComponent.EntryDelay = 0.25f;
            marker = venicleComponent.SeatMarkers["rear-left-passenger"];
            markerComponent = SEntMan.GetComponent<VenicleSeatComponent>(marker);
            actor = SEntMan.SpawnEntity("MobHuman", map.MapCoords);
            transform.SetMapCoordinates(occupant, map.MapCoords);

            var markerPosition = transform.GetWorldPosition(marker);
            transform.SetWorldPosition(actor, markerPosition + new Vector2(-0.3f, -0.4f));
            transform.SetWorldPosition(occupant, markerPosition + new Vector2(-0.3f, 0.4f));

            var canDrop = new CanDropTargetEvent(actor, occupant);
            SEntMan.EventBus.RaiseLocalEvent(marker, ref canDrop);
            Assert.Multiple(() =>
            {
                Assert.That(canDrop.Handled, Is.True);
                Assert.That(canDrop.CanDrop, Is.True);
            });

            var drop = new DragDropTargetEvent(actor, occupant);
            SEntMan.EventBus.RaiseLocalEvent(marker, ref drop);
            Assert.Multiple(() =>
            {
                Assert.That(drop.Handled, Is.True);
                Assert.That(markerComponent.PendingOccupant, Is.EqualTo(occupant));
                Assert.That(markerComponent.Available, Is.False);
                Assert.That(SEntMan.HasComponent<VenicleOccupantComponent>(occupant), Is.False);
            });
        });

        await Pair.RunTicksSync(5);

        var notification = Client.ResolveDependency<ILocalizationManager>()
            .GetString("venicle-being-seated");
        await Client.WaitAssertion(() =>
            Assert.That(_popups.WorldLabels.Select(x => x.Text), Does.Contain(notification)));

        await PoolManager.WaitUntil(Server,
            () => SEntMan.HasComponent<VenicleOccupantComponent>(occupant),
            maxTicks: 60);
        await Pair.RunUntilSynced();

        await Server.WaitAssertion(() =>
        {
            var container = venicleComponent.SeatContainers["rear-left-passenger"];
            Assert.Multiple(() =>
            {
                Assert.That(container.ContainedEntity, Is.EqualTo(occupant));
                Assert.That(markerComponent.PendingOccupant, Is.Null);
                Assert.That(markerComponent.Available, Is.False);
                Assert.That(SEntMan.HasComponent<StrapComponent>(venicle), Is.False);
                Assert.That(SEntMan.GetComponent<BuckleComponent>(occupant).Buckled, Is.False);
            });
        });

        var clientMarker = CEntMan.GetEntity(SEntMan.GetNetEntity(marker));
        await Client.WaitAssertion(() =>
        {
            var sprite = CEntMan.GetComponent<SpriteComponent>(clientMarker);
            Assert.That(sprite.Visible, Is.False);
        });
    }
}
