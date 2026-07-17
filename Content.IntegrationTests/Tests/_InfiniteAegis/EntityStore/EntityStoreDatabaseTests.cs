using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Database;
using Content.Shared.Preferences;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Serialization.Manager;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._InfiniteAegis.EntityStore;

[TestFixture]
public sealed class EntityStoreDatabaseTests : GameTest
{
    [Test]
    public async Task PurchaseIsCharacterBoundAtomicAndRevisionGuarded()
    {
        var db = GetDb(Pair.Server);
        var userId = new NetUserId(Guid.NewGuid());
        await db.InitPrefsAsync(userId, new HumanoidCharacterProfile());

        var initial = await db.GetEntityStoreCharacterStateAsync(userId, 0);
        Assert.That(initial, Is.Not.Null);

        var persistentId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var purchase = await db.PurchasePersistentEntityAsync(
            userId,
            0,
            persistentId,
            requestId,
            "EntityStoreOfferMoskvich",
            "Moskvich",
            "initial-state",
            0);

        Assert.That(purchase.Status, Is.EqualTo(EntityStorePurchaseStatus.Success));

        var secondPersistentId = Guid.NewGuid();
        var secondRequestId = Guid.NewGuid();
        var duplicateOffer = await db.PurchasePersistentEntityAsync(
            userId,
            0,
            secondPersistentId,
            secondRequestId,
            "EntityStoreOfferMoskvich",
            "Moskvich",
            "duplicate-state",
            0);
        Assert.That(duplicateOffer.Status, Is.EqualTo(EntityStorePurchaseStatus.Success));

        var entities = await db.GetPersistentCharacterEntitiesAsync(userId, 0);
        Assert.Multiple(() =>
        {
            Assert.That(entities, Has.Count.EqualTo(2));
            Assert.That(entities.Select(x => x.Id), Is.EquivalentTo([persistentId, secondPersistentId]));
            Assert.That(entities.Select(x => x.PurchaseRequestId), Is.EquivalentTo([requestId, secondRequestId]));
            Assert.That(entities.All(x => x.OfferId == "EntityStoreOfferMoskvich"), Is.True);
            Assert.That(entities.All(x => x.PrototypeId == "Moskvich"), Is.True);
        });

        var replayedRequest = await db.PurchasePersistentEntityAsync(
            userId,
            0,
            Guid.NewGuid(),
            requestId,
            "AnotherOffer",
            "Moskvich",
            "replayed-state",
            0);
        Assert.That(replayedRequest.Status, Is.EqualTo(EntityStorePurchaseStatus.DuplicateRequest));

        Assert.That(await db.UpdatePersistentEntityStateAsync(
            persistentId,
            initial!.ProfileId,
            0,
            "updated-state"), Is.True);
        Assert.That(await db.UpdatePersistentEntityStateAsync(
            persistentId,
            initial.ProfileId,
            0,
            "stale-state"), Is.False);

        await db.SaveCharacterSlotAsync(userId, null, 0);
        Assert.That(await db.GetEntityStoreCharacterStateAsync(userId, 0), Is.Null);
        Assert.That(await db.GetPersistentCharacterEntitiesAsync(userId, 0), Is.Empty);
    }

    [Test]
    public async Task PaidPurchaseIsRejectedWithoutCurrencySystem()
    {
        var db = GetDb(Pair.Server);
        var userId = new NetUserId(Guid.NewGuid());
        await db.InitPrefsAsync(userId, new HumanoidCharacterProfile());

        var result = await db.PurchasePersistentEntityAsync(
            userId,
            0,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "PaidOffer",
            "Moskvich",
            "state",
            1);

        Assert.That(result.Status, Is.EqualTo(EntityStorePurchaseStatus.InsufficientFunds));
        Assert.That(await db.GetPersistentCharacterEntitiesAsync(userId, 0), Is.Empty);
    }

    private static ServerDbSqlite GetDb(RobustIntegrationTest.ServerIntegrationInstance server)
    {
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var serialization = server.ResolveDependency<ISerializationManager>();
        var opsLog = server.ResolveDependency<ILogManager>().GetSawmill("db.ops");
        var builder = new DbContextOptionsBuilder<SqliteServerDbContext>();
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        builder.UseSqlite(connection);
        return new ServerDbSqlite(() => builder.Options, true, cfg, true, opsLog, serialization);
    }
}
