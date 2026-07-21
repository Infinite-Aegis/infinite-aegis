using System;

namespace Content.Server.Database;

public enum CarDealerStorePurchaseStatus : byte
{
    Success,
    CharacterNotFound,
    DuplicateRequest,
    DuplicatePersistentId,
    InsufficientFunds,
    InvalidRequest,
    Failed,
}

public sealed record CarDealerStoreCharacterState(int ProfileId);

public sealed record CarDealerStorePersistentEntityData(
    Guid Id,
    int ProfileId,
    string OfferId,
    string PrototypeId,
    Guid PurchaseRequestId,
    string EntityState,
    long Revision);

public sealed record CarDealerStorePersistentEntitySummary(
    Guid Id,
    string OfferId,
    string PrototypeId);

public sealed record CarDealerStorePurchaseResult(
    CarDealerStorePurchaseStatus Status,
    int? ProfileId = null);
