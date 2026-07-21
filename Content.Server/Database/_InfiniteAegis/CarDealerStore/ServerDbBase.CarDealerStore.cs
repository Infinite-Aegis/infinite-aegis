using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Robust.Shared.Network;

namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    public async Task<CarDealerStoreCharacterState?> GetCarDealerStoreCharacterStateAsync(
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

        return new CarDealerStoreCharacterState(profileId.Value);
    }

    public async Task<List<CarDealerStorePersistentEntityData>> GetPersistentCharacterEntitiesAsync(
        NetUserId userId,
        int characterSlot)
    {
        await using var db = await GetDb();

        return await db.DbContext.PersistentCharacterEntity
            .Where(x => x.Profile.Preference.UserId == userId.UserId && x.Profile.Slot == characterSlot)
            .Select(x => new CarDealerStorePersistentEntityData(
                x.Id,
                x.ProfileId,
                x.OfferId,
                x.PrototypeId,
                x.PurchaseRequestId,
                x.EntityState,
                x.Revision))
            .ToListAsync();
    }

    public async Task<List<CarDealerStorePersistentEntitySummary>> GetPersistentCharacterEntitySummariesAsync(
        NetUserId userId,
        int characterSlot)
    {
        await using var db = await GetDb();

        return await db.DbContext.PersistentCharacterEntity
            .Where(x => x.Profile.Preference.UserId == userId.UserId && x.Profile.Slot == characterSlot)
            .OrderBy(x => x.UpdatedAt)
            .Select(x => new CarDealerStorePersistentEntitySummary(
                x.Id,
                x.OfferId,
                x.PrototypeId))
            .ToListAsync();
    }

    public async Task<CarDealerStorePersistentEntityData?> GetPersistentCharacterEntityAsync(
        NetUserId userId,
        int characterSlot,
        Guid persistentEntityId)
    {
        if (persistentEntityId == Guid.Empty)
            return null;

        await using var db = await GetDb();

        return await db.DbContext.PersistentCharacterEntity
            .Where(x => x.Id == persistentEntityId &&
                        x.Profile.Preference.UserId == userId.UserId &&
                        x.Profile.Slot == characterSlot)
            .Select(x => new CarDealerStorePersistentEntityData(
                x.Id,
                x.ProfileId,
                x.OfferId,
                x.PrototypeId,
                x.PurchaseRequestId,
                x.EntityState,
                x.Revision))
            .SingleOrDefaultAsync();
    }

    public async Task<CarDealerStorePurchaseResult> PurchasePersistentEntityAsync(
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
            return new CarDealerStorePurchaseResult(CarDealerStorePurchaseStatus.InvalidRequest);
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
                return new CarDealerStorePurchaseResult(CarDealerStorePurchaseStatus.CharacterNotFound);

            var duplicate = await db.DbContext.PersistentCharacterEntity.AnyAsync(x =>
                x.PurchaseRequestId == purchaseRequestId);

            if (duplicate)
                return new CarDealerStorePurchaseResult(CarDealerStorePurchaseStatus.DuplicateRequest, profileId);

            var persistentIdExists = await db.DbContext.PersistentCharacterEntity.AnyAsync(x =>
                x.Id == persistentEntityId);

            if (persistentIdExists)
                return new CarDealerStorePurchaseResult(CarDealerStorePurchaseStatus.DuplicatePersistentId, profileId);

            if (price > 0)
                return new CarDealerStorePurchaseResult(CarDealerStorePurchaseStatus.InsufficientFunds, profileId);

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
            return new CarDealerStorePurchaseResult(CarDealerStorePurchaseStatus.Success, profileId);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return new CarDealerStorePurchaseResult(CarDealerStorePurchaseStatus.DuplicateRequest);
        }
        catch
        {
            await transaction.RollbackAsync();
            return new CarDealerStorePurchaseResult(CarDealerStorePurchaseStatus.Failed);
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
