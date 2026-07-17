using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Robust.Shared.Network;

namespace Content.Server.Database;

public partial interface IServerDbManager
{
    Task<EntityStoreCharacterState?> GetEntityStoreCharacterStateAsync(NetUserId userId, int characterSlot);

    Task<List<EntityStorePersistentEntityData>> GetPersistentCharacterEntitiesAsync(
        NetUserId userId,
        int characterSlot);

    Task<EntityStorePurchaseResult> PurchasePersistentEntityAsync(
        NetUserId userId,
        int characterSlot,
        Guid persistentEntityId,
        Guid purchaseRequestId,
        string offerId,
        string prototypeId,
        string entityState,
        int price);

    Task<bool> UpdatePersistentEntityStateAsync(
        Guid persistentEntityId,
        int profileId,
        long expectedRevision,
        string entityState);
}
