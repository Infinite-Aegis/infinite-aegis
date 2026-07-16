using System.Collections.Generic;

namespace Content.Server.Database;

public enum EntityStorePurchaseStatus : byte
{
    Success,
    CharacterNotFound,
    AlreadyOwned,
    InsufficientFunds,
    InvalidRequest,
    Failed,
}

public sealed record EntityStoreCharacterState(
    int ProfileId,
    long Balance,
    HashSet<string> OwnedOffers);

public sealed record EntityStorePurchaseResult(
    EntityStorePurchaseStatus Status,
    int? ProfileId = null,
    long? Balance = null);
