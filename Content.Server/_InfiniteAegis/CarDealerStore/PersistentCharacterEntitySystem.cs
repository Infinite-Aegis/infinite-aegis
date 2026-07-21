using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.Garage;
using Content.Shared.CarDealerStore;
using Content.Shared.GameTicking;
using Robust.Shared.Asynchronous;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.CarDealerStore;

public sealed partial class PersistentCharacterEntitySystem : EntitySystem
{
    [Dependency] private IServerDbManager _database = default!;
    [Dependency] private GarageSystem _garage = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private ITaskManager _taskManager = default!;

    private readonly HashSet<EntityUid> _capturedBeforeDeletion = [];
    private readonly List<Task> _pendingSaveTasks = [];

    public override void Initialize()
    {
        base.Initialize();

        EntityManager.EntityQueueDeleted += OnEntityQueuedForDeletion;
        EntityManager.EntityDeleted += OnEntityDeleted;

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<PersistentCharacterEntityComponent, ContainerGettingInsertedAttemptEvent>(OnInsertAttempt);
    }

    public override void Shutdown()
    {
        EntityManager.EntityQueueDeleted -= OnEntityQueuedForDeletion;
        EntityManager.EntityDeleted -= OnEntityDeleted;

        if (_pendingSaveTasks.Count != 0)
            _taskManager.BlockWaitOnTask(Task.WhenAll(_pendingSaveTasks.ToArray()));

        base.Shutdown();
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

    private void OnEntityQueuedForDeletion(EntityUid queuedEntity)
    {
        if (!TryComp(queuedEntity, out TransformComponent? queuedTransform))
            return;

        var pending = new Stack<(EntityUid Uid, TransformComponent Transform)>();
        pending.Push((queuedEntity, queuedTransform));

        while (pending.TryPop(out var current))
        {
            if (TryComp(current.Uid, out PersistentCharacterEntityComponent? persistence) &&
                !_capturedBeforeDeletion.Contains(current.Uid) &&
                !persistence.SuppressSave &&
                TryCaptureSnapshot((current.Uid, persistence), out var snapshot))
            {
                _capturedBeforeDeletion.Add(current.Uid);
                ScheduleSave(snapshot);
            }

            var children = current.Transform.ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                if (TryComp(child, out TransformComponent? childTransform))
                    pending.Push((child, childTransform));
            }
        }
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        var query = EntityQueryEnumerator<PersistentCharacterEntityComponent>();
        while (query.MoveNext(out var uid, out var persistence))
        {
            if (_capturedBeforeDeletion.Contains(uid) || persistence.SuppressSave)
                continue;

            if (!TryCaptureSnapshot((uid, persistence), out var snapshot))
                continue;

            _capturedBeforeDeletion.Add(uid);
            ScheduleSave(snapshot);
        }
    }

    private bool TryCaptureSnapshot(
        Entity<PersistentCharacterEntityComponent> entity,
        out PersistentEntitySnapshot snapshot)
    {
        snapshot = default;
        if (entity.Comp.SuppressSave ||
            TerminatingOrDeleted(entity.Owner) ||
            !Guid.TryParse(entity.Comp.PersistentEntityId, out var persistentEntityId) ||
            entity.Comp.CharacterProfileId <= 0 ||
            entity.Comp.Revision < 0)
        {
            return false;
        }

        using var writer = new StringWriter();
        var options = SerializationOptions.Default with { Category = FileCategory.Save };
        if (!_mapLoader.TrySaveGeneric(entity.Owner, writer, out _, options))
        {
            Log.Error($"Failed to serialize persistent entity {ToPrettyString(entity.Owner)} while it was still alive.");
            return false;
        }

        var serializedState = writer.ToString();
        if (string.IsNullOrWhiteSpace(serializedState))
        {
            Log.Error($"Serializer returned an empty state for persistent entity {ToPrettyString(entity.Owner)}.");
            return false;
        }

        snapshot = new PersistentEntitySnapshot(
            persistentEntityId,
            entity.Comp.CharacterProfileId,
            entity.Comp.Revision,
            serializedState,
            entity.Comp.CharacterOwner,
            ToPrettyString(entity.Owner).ToString());
        return true;
    }

    private void ScheduleSave(PersistentEntitySnapshot snapshot)
    {
        var task = SaveSnapshotAsync(snapshot);
        _pendingSaveTasks.Add(task);
        _ = RemoveCompletedSaveAsync(task);
    }

    private async Task SaveSnapshotAsync(PersistentEntitySnapshot snapshot)
    {
        try
        {
            var updated = await _database.UpdatePersistentEntityStateAsync(
                snapshot.PersistentEntityId,
                snapshot.CharacterProfileId,
                snapshot.ExpectedRevision,
                snapshot.SerializedState);

            if (!updated)
            {
                Log.Error(
                    $"Rejected stale or owner-mismatched persistent entity save for {snapshot.PersistentEntityId} " +
                    $"({snapshot.EntityDescription}).");
                return;
            }

            _garage.NotifyStoreWrite(snapshot.CharacterOwner, snapshot.CharacterProfileId);
        }
        catch (Exception exception)
        {
            Log.Error(
                $"Failed to save persistent entity {snapshot.PersistentEntityId} " +
                $"({snapshot.EntityDescription}): {exception}");
        }
    }

    private async Task RemoveCompletedSaveAsync(Task task)
    {
        try
        {
            await task;
        }
        finally
        {
            _pendingSaveTasks.Remove(task);
        }
    }

    private void OnEntityDeleted(Entity<MetaDataComponent> entity)
    {
        _capturedBeforeDeletion.Remove(entity.Owner);
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

    private readonly record struct PersistentEntitySnapshot(
        Guid PersistentEntityId,
        int CharacterProfileId,
        long ExpectedRevision,
        string SerializedState,
        EntityUid? CharacterOwner,
        string EntityDescription);
}
