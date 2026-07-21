namespace Content.Shared.CarDealerStore;

/// <summary>
/// Identifies the root of an entity graph owned by a specific character profile.
/// Runtime owner references are rebound when the graph is materialized and are not serialized.
/// </summary>
[RegisterComponent]
public sealed partial class PersistentCharacterEntityComponent : Component
{
    [DataField(required: true)]
    public string PersistentEntityId = string.Empty;

    [DataField(required: true)]
    public int CharacterProfileId;

    [DataField]
    public long Revision;

    [ViewVariables]
    public EntityUid? CharacterOwner;

    /// <summary>
    /// Prevents transient entity graphs from being written back when they are queued for deletion or a round ends.
    /// </summary>
    [ViewVariables]
    public bool SuppressSave;
}
