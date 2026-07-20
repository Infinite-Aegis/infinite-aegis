using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.EntityStore;
using Content.Shared.EntityStore;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;

namespace Content.Server.Garage;

public sealed partial class GarageSystem
{
    private const float NearbyParkingLotRadius = 64f;

    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private async Task SpawnVehicleAsync(EntityUid owner, Guid vehicleId)
    {
        if (!TryGetGarageSession(owner, out _, out var session, out var slot, out var profileId))
            return;

        if (TryGetActiveVehicle(profileId, out _) || !_summonsInFlight.Add(profileId))
        {
            SendAlreadyCalled(session);
            return;
        }

        try
        {
            var vehicle = await _database.GetPersistentCharacterEntityAsync(session.UserId, slot, vehicleId);
            if (!TryValidateSession(owner, session.UserId, slot, out var refreshedSession) ||
                !TryComp<GarageComponent>(owner, out var garage) ||
                garage.ProfileId != profileId)
            {
                return;
            }

            if (vehicle == null || vehicle.ProfileId != profileId)
            {
                _chat.DispatchServerMessage(refreshedSession, Loc.GetString("garage-call-failed"));
                return;
            }

            if (TryGetActiveVehicle(profileId, out _))
            {
                SendAlreadyCalled(refreshedSession);
                return;
            }

            if (!TryFindParkingLot(owner, out var parkingLot))
            {
                _chat.DispatchServerMessage(refreshedSession, Loc.GetString("garage-no-parking-lot"));
                return;
            }

            if (!TryMaterializeVehicle(vehicle, owner, parkingLot, out var spawned))
            {
                _chat.DispatchServerMessage(refreshedSession, Loc.GetString("garage-call-failed"));
                return;
            }

            _activeVehicles[profileId] = spawned;
        }
        finally
        {
            _summonsInFlight.Remove(profileId);
        }
    }

    private bool TryFindParkingLot(EntityUid owner, out EntityUid parkingLot)
    {
        parkingLot = default;
        var ownerCoordinates = _transform.GetMapCoordinates(owner);
        var nearestDistance = float.MaxValue;

        foreach (var candidate in _lookup.GetEntitiesInRange<ParkingLotComponent>(
                     ownerCoordinates,
                     NearbyParkingLotRadius,
                     LookupFlags.StaticSundries))
        {
            var candidateCoordinates = _transform.GetMapCoordinates(candidate.Owner);
            var distance = Vector2.DistanceSquared(ownerCoordinates.Position, candidateCoordinates.Position);
            if (distance >= nearestDistance)
                continue;

            parkingLot = candidate.Owner;
            nearestDistance = distance;
        }

        if (parkingLot.IsValid())
            return true;

        var query = EntityQueryEnumerator<ParkingLotComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var transform))
        {
            if (transform.MapID != ownerCoordinates.MapId)
                continue;

            var candidateCoordinates = _transform.GetMapCoordinates(uid, transform);
            var distance = Vector2.DistanceSquared(ownerCoordinates.Position, candidateCoordinates.Position);
            if (distance >= nearestDistance)
                continue;

            parkingLot = uid;
            nearestDistance = distance;
        }

        return parkingLot.IsValid();
    }

    private bool TryMaterializeVehicle(
        EntityStorePersistentEntityData vehicle,
        EntityUid owner,
        EntityUid parkingLot,
        out EntityUid spawned)
    {
        spawned = default;
        LoadResult? result = null;

        try
        {
            using var reader = new StringReader(vehicle.EntityState);
            var options = MapLoadOptions.Default;
            options.ExpectedCategory = FileCategory.Save;
            if (!_mapLoader.TryLoadGeneric(reader, $"persistent entity {vehicle.Id}", out result, options))
                return false;

            foreach (var loaded in result.Entities)
            {
                if (TryComp<PersistentCharacterEntityComponent>(loaded, out var loadedPersistence))
                    loadedPersistence.SuppressSave = true;
            }

            if (result.RootNodes.Count != 1)
                return false;

            var root = result.RootNodes.Single();
            if (!TryComp<PersistentCharacterEntityComponent>(root, out var persistence) ||
                !Guid.TryParse(persistence.PersistentEntityId, out var storedId) ||
                storedId != vehicle.Id ||
                persistence.CharacterProfileId != vehicle.ProfileId)
            {
                return false;
            }

            var persistentRoots = result.Entities.Count(HasComp<PersistentCharacterEntityComponent>);
            if (persistentRoots != 1)
                return false;

            persistence.PersistentEntityId = vehicle.Id.ToString();
            persistence.CharacterProfileId = vehicle.ProfileId;
            persistence.CharacterOwner = owner;
            persistence.Revision = vehicle.Revision;

            _transform.SetCoordinates(root, Transform(parkingLot).Coordinates);
            persistence.SuppressSave = false;
            spawned = root;
            result = null;
            return true;
        }
        catch (Exception exception)
        {
            Log.Error($"Failed to materialize persistent entity {vehicle.Id}: {exception}");
            return false;
        }
        finally
        {
            if (result != null)
                _mapLoader.Delete(result);
        }
    }

    private bool TryGetActiveVehicle(int profileId, out EntityUid vehicle)
    {
        if (_activeVehicles.TryGetValue(profileId, out vehicle) &&
            Exists(vehicle) &&
            !EntityManager.IsQueuedForDeletion(vehicle) &&
            TryComp<PersistentCharacterEntityComponent>(vehicle, out var persistence) &&
            !persistence.SuppressSave &&
            persistence.CharacterProfileId == profileId)
        {
            return true;
        }

        _activeVehicles.Remove(profileId);

        var query = EntityQueryEnumerator<PersistentCharacterEntityComponent>();
        while (query.MoveNext(out var uid, out var candidate))
        {
            if (candidate.SuppressSave ||
                candidate.CharacterProfileId != profileId ||
                EntityManager.IsQueuedForDeletion(uid))
            {
                continue;
            }

            _activeVehicles[profileId] = uid;
            vehicle = uid;
            return true;
        }

        vehicle = default;
        return false;
    }
}
