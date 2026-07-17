using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Robust.Shared.Network;

namespace Content.Server.Database;

public sealed partial class ServerDbManager
{
    public Task<EntityStoreCharacterState?> GetEntityStoreCharacterStateAsync(NetUserId userId, int characterSlot)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetEntityStoreCharacterStateAsync(userId, characterSlot));
    }

    public Task<List<EntityStorePersistentEntityData>> GetPersistentCharacterEntitiesAsync(
        NetUserId userId,
        int characterSlot)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetPersistentCharacterEntitiesAsync(userId, characterSlot));
    }

    public Task<EntityStorePurchaseResult> PurchasePersistentEntityAsync(
        NetUserId userId,
        int characterSlot,
        Guid persistentEntityId,
        Guid purchaseRequestId,
        string offerId,
        string prototypeId,
        string entityState,
        int price)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.PurchasePersistentEntityAsync(
            userId,
            characterSlot,
            persistentEntityId,
            purchaseRequestId,
            offerId,
            prototypeId,
            entityState,
            price));
    }

    public Task<bool> UpdatePersistentEntityStateAsync(
        Guid persistentEntityId,
        int profileId,
        long expectedRevision,
        string entityState)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.UpdatePersistentEntityStateAsync(
            persistentEntityId,
            profileId,
            expectedRevision,
            entityState));
    }
}
