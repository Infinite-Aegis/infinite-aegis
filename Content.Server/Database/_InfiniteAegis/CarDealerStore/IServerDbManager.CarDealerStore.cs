using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Robust.Shared.Network;

namespace Content.Server.Database;

public partial interface IServerDbManager
{
    Task<CarDealerStoreCharacterState?> GetCarDealerStoreCharacterStateAsync(NetUserId userId, int characterSlot);

    Task<List<CarDealerStorePersistentEntityData>> GetPersistentCharacterEntitiesAsync(
        NetUserId userId,
        int characterSlot);

    Task<List<CarDealerStorePersistentEntitySummary>> GetPersistentCharacterEntitySummariesAsync(
        NetUserId userId,
        int characterSlot);

    Task<CarDealerStorePersistentEntityData?> GetPersistentCharacterEntityAsync(
        NetUserId userId,
        int characterSlot,
        Guid persistentEntityId);

    Task<CarDealerStorePurchaseResult> PurchasePersistentEntityAsync(
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
