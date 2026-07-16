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
        Assert.Multiple(() =>
        {
            Assert.That(initial!.Balance, Is.Zero);
            Assert.That(initial.OwnedOffers, Is.Empty);
        });

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

        var duplicateOffer = await db.PurchasePersistentEntityAsync(
            userId,
            0,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "EntityStoreOfferMoskvich",
            "Moskvich",
            "duplicate-state",
            0);
        Assert.That(duplicateOffer.Status, Is.EqualTo(EntityStorePurchaseStatus.AlreadyOwned));

        var replayedRequest = await db.PurchasePersistentEntityAsync(
            userId,
            0,
            Guid.NewGuid(),
            requestId,
            "AnotherOffer",
            "Moskvich",
            "replayed-state",
            0);
        Assert.That(replayedRequest.Status, Is.EqualTo(EntityStorePurchaseStatus.AlreadyOwned));

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
    }

    [Test]
    public async Task PurchaseDoesNotChargeWhenFundsAreInsufficient()
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
        var state = await db.GetEntityStoreCharacterStateAsync(userId, 0);
        Assert.Multiple(() =>
        {
            Assert.That(state!.Balance, Is.Zero);
            Assert.That(state.OwnedOffers, Is.Empty);
        });
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
