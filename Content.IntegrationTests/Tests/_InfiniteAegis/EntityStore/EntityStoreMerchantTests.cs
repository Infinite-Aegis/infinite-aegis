using System;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Database;
using Content.Server.EntityStore;
using Content.Server.Preferences.Managers;
using Content.Shared.EntityStore;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._InfiniteAegis.EntityStore;

[TestFixture]
[TestOf(typeof(EntityStoreMerchantSystem))]
public sealed class EntityStoreMerchantTests : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
        DummyTicker = false,
    };

    [SidedDependency(Side.Server)] private readonly IServerDbManager _database = null!;
    [SidedDependency(Side.Server)] private readonly IServerPreferencesManager _preferences = null!;
    [SidedDependency(Side.Client)] private readonly Content.Client.Popups.PopupSystem _popups = null!;

    [Test]
    public async Task PurchasesMoskvichThroughTerminalAndStoresEveryInstance()
    {
        var session = ServerSession;
        Assert.That(session, Is.Not.Null);
        Assert.That(session!.AttachedEntity, Is.Not.Null);

        var player = session.AttachedEntity!.Value;
        var slot = _preferences.GetPreferences(session.UserId).SelectedCharacterIndex;
        Assert.That(await _database.GetEntityStoreCharacterStateAsync(session.UserId, slot), Is.Not.Null);
        Assert.That(await _database.GetPersistentCharacterEntitiesAsync(session.UserId, slot), Is.Empty);

        var map = await Pair.CreateTestMap();
        EntityUid merchant = default;
        var firstRequestId = Guid.NewGuid();

        await Server.WaitAssertion(() =>
        {
            Server.System<SharedTransformSystem>().SetCoordinates(player, map.GridCoords);
            merchant = SEntMan.SpawnEntity("EntityStoreAutoDealer", map.GridCoords);
            var message = new EntityStoreBuyMessage("EntityStoreOfferMoskvich", firstRequestId)
            {
                Actor = player,
                UiKey = EntityStoreUiKey.Key,
            };
            SEntMan.EventBus.RaiseLocalEvent(merchant, message);
        });

        await PoolManager.WaitUntil(Server, async () =>
            (await _database.GetPersistentCharacterEntitiesAsync(session.UserId, slot)).Count == 1);
        await Pair.RunTicksSync(5);

        var successMessage = Server.ResolveDependency<ILocalizationManager>()
            .GetString("entity-store-purchase-success");
        await Client.WaitAssertion(() =>
            Assert.That(_popups.WorldLabels.Select(x => x.Text), Does.Contain(successMessage)));

        var firstPurchase = (await _database.GetPersistentCharacterEntitiesAsync(session.UserId, slot)).Single();
        Assert.Multiple(() =>
        {
            Assert.That(firstPurchase.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(firstPurchase.OfferId, Is.EqualTo("EntityStoreOfferMoskvich"));
            Assert.That(firstPurchase.PrototypeId, Is.EqualTo("Moskvich"));
            Assert.That(firstPurchase.PurchaseRequestId, Is.EqualTo(firstRequestId));
            Assert.That(firstPurchase.EntityState, Does.Contain(firstPurchase.Id.ToString()));
        });

        var secondRequestId = Guid.NewGuid();
        await Server.WaitPost(() =>
        {
            var message = new EntityStoreBuyMessage("EntityStoreOfferMoskvich", secondRequestId)
            {
                Actor = player,
                UiKey = EntityStoreUiKey.Key,
            };
            SEntMan.EventBus.RaiseLocalEvent(merchant, message);
        });

        await PoolManager.WaitUntil(Server, async () =>
            (await _database.GetPersistentCharacterEntitiesAsync(session.UserId, slot)).Count == 2);

        var purchases = await _database.GetPersistentCharacterEntitiesAsync(session.UserId, slot);
        Assert.Multiple(() =>
        {
            Assert.That(purchases.Select(x => x.Id).Distinct().ToArray(), Has.Length.EqualTo(2));
            Assert.That(purchases.Select(x => x.PurchaseRequestId),
                Is.EquivalentTo([firstRequestId, secondRequestId]));
            Assert.That(purchases.All(x => x.OfferId == "EntityStoreOfferMoskvich"), Is.True);
            Assert.That(purchases.All(x => x.PrototypeId == "Moskvich"), Is.True);
            Assert.That(purchases.All(x => x.EntityState.Contains(x.Id.ToString())), Is.True);
        });
    }
}
