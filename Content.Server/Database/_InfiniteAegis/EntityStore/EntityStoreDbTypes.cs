using System;

namespace Content.Server.Database;

public enum EntityStorePurchaseStatus : byte
{
    Success,
    CharacterNotFound,
    DuplicateRequest,
    InsufficientFunds,
    InvalidRequest,
    Failed,
}

public sealed record EntityStoreCharacterState(int ProfileId);

public sealed record EntityStorePersistentEntityData(
    Guid Id,
    int ProfileId,
    string OfferId,
    string PrototypeId,
    Guid PurchaseRequestId,
    string EntityState,
    long Revision);

public sealed record EntityStorePurchaseResult(
    EntityStorePurchaseStatus Status,
    int? ProfileId = null);
