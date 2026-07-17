using System;
using System.IO;
using Content.Server.Database;
using Content.Shared.EntityStore;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.EntityStore;

public sealed partial class PersistentCharacterEntitySystem : EntitySystem
{
    [Dependency] private IServerDbManager _database = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PersistentCharacterEntityComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<PersistentCharacterEntityComponent, ContainerGettingInsertedAttemptEvent>(OnInsertAttempt);
    }

    public bool TryCreatePurchaseSnapshot(
        EntProtoId prototype,
        Guid persistentEntityId,
        int profileId,
        EntityUid owner,
        out string serializedState)
    {
        serializedState = string.Empty;
        var entity = Spawn(prototype, MapCoordinates.Nullspace);
        var persistence = EnsureComp<PersistentCharacterEntityComponent>(entity);
        persistence.PersistentEntityId = persistentEntityId.ToString();
        persistence.CharacterProfileId = profileId;
        persistence.CharacterOwner = owner;
        persistence.Revision = 0;
        persistence.SuppressSave = true;

        try
        {
            using var writer = new StringWriter();
            var options = SerializationOptions.Default with { Category = FileCategory.Save };
            if (!_mapLoader.TrySaveGeneric(entity, writer, out _, options))
                return false;

            serializedState = writer.ToString();
            return !string.IsNullOrWhiteSpace(serializedState);
        }
        finally
        {
            QueueDel(entity);
        }
    }

    private void OnTerminating(
        Entity<PersistentCharacterEntityComponent> entity,
        ref EntityTerminatingEvent args)
    {
        if (entity.Comp.SuppressSave ||
            !Guid.TryParse(entity.Comp.PersistentEntityId, out var persistentEntityId) ||
            entity.Comp.CharacterProfileId <= 0)
        {
            return;
        }

        using var writer = new StringWriter();
        var options = SerializationOptions.Default with { Category = FileCategory.Save };
        if (!_mapLoader.TrySaveGeneric(entity.Owner, writer, out _, options))
        {
            Log.Error($"Failed to serialize persistent entity {ToPrettyString(entity.Owner)} before deletion.");
            return;
        }

        var updated = _database.UpdatePersistentEntityStateAsync(
                persistentEntityId,
                entity.Comp.CharacterProfileId,
                entity.Comp.Revision,
                writer.ToString())
            .GetAwaiter()
            .GetResult();

        if (!updated)
        {
            Log.Error($"Rejected stale or owner-mismatched persistent entity save for {entity.Comp.PersistentEntityId}.");
        }
    }

    private void OnInsertAttempt(
        Entity<PersistentCharacterEntityComponent> entity,
        ref ContainerGettingInsertedAttemptEvent args)
    {
        var containerOwner = args.Container.Owner;
        while (true)
        {
            if (HasComp<PersistentCharacterEntityComponent>(containerOwner))
            {
                args.Cancel();
                return;
            }

            var parent = Transform(containerOwner).ParentUid;
            if (!Exists(parent))
                return;

            containerOwner = parent;
        }
    }
}
