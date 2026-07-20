using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Server.EntityStore;
using Content.Server.Preferences.Managers;
using Content.Shared.Actions;
using Content.Shared.EntityStore;
using Content.Shared.GameTicking;
using Content.Shared.Garage;
using Content.Shared.Mobs.Systems;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Garage;

public sealed partial class GarageSystem : EntitySystem
{
    private const string ClientUiType = "GarageBoundUserInterface";

    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IServerDbManager _database = default!;
    [Dependency] private IServerPreferencesManager _preferences = default!;
    [Dependency] private ISharedPlayerManager _players = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private readonly Dictionary<int, EntityUid> _activeVehicles = [];
    private readonly HashSet<int> _summonsInFlight = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<GarageComponent, ComponentShutdown>(OnGarageShutdown);
        SubscribeLocalEvent<GarageComponent, OpenGarageEvent>(OnOpenGarage);
        SubscribeLocalEvent<GarageComponent, GarageRequestUpdateMessage>(OnRequestUpdate);
        SubscribeLocalEvent<GarageComponent, GarageSpawnMessage>(OnSpawnRequested);
        SubscribeLocalEvent<PersistentCharacterEntityComponent, EntityTerminatingEvent>(OnVehicleTerminating);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent args)
    {
        if (!_preferences.HavePreferencesLoaded(args.Player))
            return;

        var slot = _preferences.GetPreferences(args.Player.UserId).SelectedCharacterIndex;
        _ = RefreshAccessAsync(args.Mob, args.Player.UserId, slot);
    }

    private void OnGarageShutdown(Entity<GarageComponent> entity, ref ComponentShutdown args)
    {
        _ui.CloseUi(entity.Owner, GarageUiKey.Key);
        _actions.RemoveAction(entity.Comp.Action);
        entity.Comp.Action = null;
    }

    private void OnOpenGarage(Entity<GarageComponent> entity, ref OpenGarageEvent args)
    {
        if (args.Handled || args.Performer != entity.Owner)
            return;

        args.Handled = true;
        _ = UpdateAndOpenAsync(entity.Owner, true);
    }

    private void OnRequestUpdate(Entity<GarageComponent> entity, ref GarageRequestUpdateMessage args)
    {
        if (args.Actor != entity.Owner)
            return;

        _ = UpdateAndOpenAsync(entity.Owner, false);
    }

    private void OnSpawnRequested(Entity<GarageComponent> entity, ref GarageSpawnMessage args)
    {
        if (args.Actor != entity.Owner || args.VehicleId == Guid.Empty)
            return;

        _ui.CloseUi(entity.Owner, GarageUiKey.Key, entity.Owner);
        _ = SpawnVehicleAsync(entity.Owner, args.VehicleId);
    }

    private void OnVehicleTerminating(
        Entity<PersistentCharacterEntityComponent> entity,
        ref EntityTerminatingEvent args)
    {
        NotifyVehicleTerminating(entity.Owner, entity.Comp.CharacterProfileId);
    }

    public void NotifyVehicleTerminating(EntityUid vehicle, int profileId)
    {
        if (profileId > 0 &&
            _activeVehicles.TryGetValue(profileId, out var active) &&
            active == vehicle)
        {
            _activeVehicles.Remove(profileId);
        }
    }

    public void NotifyStoreWrite(EntityUid owner, NetUserId userId, int slot, int profileId)
    {
        _ = RefreshAccessAsync(owner, userId, slot, profileId);
    }

    public void NotifyStoreWrite(EntityUid? owner, int profileId)
    {
        if (owner == null ||
            !Exists(owner.Value) ||
            !_players.TryGetSessionByEntity(owner.Value, out var session) ||
            !_preferences.HavePreferencesLoaded(session))
        {
            return;
        }

        var slot = _preferences.GetPreferences(session.UserId).SelectedCharacterIndex;
        _ = RefreshAccessAsync(owner.Value, session.UserId, slot, profileId);
    }
}
