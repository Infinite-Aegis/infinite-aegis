using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Robust.Shared.Network;

namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    public async Task<EntityStoreCharacterState?> GetEntityStoreCharacterStateAsync(
        NetUserId userId,
        int characterSlot)
    {
        await using var db = await GetDb();

        var profileId = await db.DbContext.Profile
            .Where(x => x.Preference.UserId == userId.UserId && x.Slot == characterSlot)
            .Select(x => (int?) x.Id)
            .SingleOrDefaultAsync();

        if (profileId == null)
            return null;

        var balance = await db.DbContext.CharacterWallet
            .Where(x => x.ProfileId == profileId.Value)
            .Select(x => (long?) x.Balance)
            .SingleOrDefaultAsync() ?? 0;

        var ownedOffers = await db.DbContext.PersistentCharacterEntity
            .Where(x => x.ProfileId == profileId.Value)
            .Select(x => x.OfferId)
            .ToHashSetAsync();

        return new EntityStoreCharacterState(profileId.Value, balance, ownedOffers);
    }

    public async Task<EntityStorePurchaseResult> PurchasePersistentEntityAsync(
        NetUserId userId,
        int characterSlot,
        Guid persistentEntityId,
        Guid purchaseRequestId,
        string offerId,
        string prototypeId,
        string entityState,
        int price)
    {
        if (persistentEntityId == Guid.Empty ||
            purchaseRequestId == Guid.Empty ||
            string.IsNullOrWhiteSpace(offerId) ||
            string.IsNullOrWhiteSpace(prototypeId) ||
            string.IsNullOrWhiteSpace(entityState) ||
            price < 0)
        {
            return new EntityStorePurchaseResult(EntityStorePurchaseStatus.InvalidRequest);
        }

        await using var db = await GetDb();
        await using var transaction = await db.DbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            var profileId = await db.DbContext.Profile
                .Where(x => x.Preference.UserId == userId.UserId && x.Slot == characterSlot)
                .Select(x => (int?) x.Id)
                .SingleOrDefaultAsync();

            if (profileId == null)
                return new EntityStorePurchaseResult(EntityStorePurchaseStatus.CharacterNotFound);

            var duplicate = await db.DbContext.PersistentCharacterEntity.AnyAsync(x =>
                x.PurchaseRequestId == purchaseRequestId ||
                x.ProfileId == profileId.Value && x.OfferId == offerId);

            if (duplicate)
                return new EntityStorePurchaseResult(EntityStorePurchaseStatus.AlreadyOwned, profileId);

            var wallet = await db.DbContext.CharacterWallet.SingleOrDefaultAsync(x => x.ProfileId == profileId.Value);
            if (wallet == null)
            {
                wallet = new CharacterWallet
                {
                    ProfileId = profileId.Value,
                    Balance = 0,
                };
                db.DbContext.CharacterWallet.Add(wallet);
            }

            if (wallet.Balance < price)
                return new EntityStorePurchaseResult(
                    EntityStorePurchaseStatus.InsufficientFunds,
                    profileId,
                    wallet.Balance);

            wallet.Balance -= price;
            db.DbContext.PersistentCharacterEntity.Add(new PersistentCharacterEntity
            {
                Id = persistentEntityId,
                ProfileId = profileId.Value,
                OfferId = offerId,
                PrototypeId = prototypeId,
                PurchaseRequestId = purchaseRequestId,
                EntityState = entityState,
                Revision = 0,
                UpdatedAt = DateTime.UtcNow,
            });

            await db.DbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            return new EntityStorePurchaseResult(
                EntityStorePurchaseStatus.Success,
                profileId,
                wallet.Balance);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return new EntityStorePurchaseResult(EntityStorePurchaseStatus.AlreadyOwned);
        }
        catch
        {
            await transaction.RollbackAsync();
            return new EntityStorePurchaseResult(EntityStorePurchaseStatus.Failed);
        }
    }

    public async Task<bool> UpdatePersistentEntityStateAsync(
        Guid persistentEntityId,
        int profileId,
        long expectedRevision,
        string entityState)
    {
        if (persistentEntityId == Guid.Empty || profileId <= 0 || expectedRevision < 0 || string.IsNullOrWhiteSpace(entityState))
            return false;

        await using var db = await GetDb();
        var updated = await db.DbContext.PersistentCharacterEntity
            .Where(x => x.Id == persistentEntityId &&
                        x.ProfileId == profileId &&
                        x.Revision == expectedRevision)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.EntityState, entityState)
                .SetProperty(x => x.Revision, x => x.Revision + 1)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow));

        return updated == 1;
    }
}
