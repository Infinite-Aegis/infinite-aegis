namespace Content.Shared.EntityStore;

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
    /// Prevents the transient purchase snapshot from being written back by its own deletion event.
    /// </summary>
    [ViewVariables]
    public bool SuppressSave;
}
