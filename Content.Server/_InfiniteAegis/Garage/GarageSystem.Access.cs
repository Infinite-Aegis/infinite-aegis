using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared.Garage;
using Content.Shared.UserInterface;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Garage;

public sealed partial class GarageSystem
{
    private async Task RefreshAccessAsync(
        EntityUid owner,
        NetUserId userId,
        int slot,
        int? expectedProfileId = null)
    {
        var character = await _database.GetCarDealerStoreCharacterStateAsync(userId, slot);
        var vehicles = character == null
            ? []
            : await _database.GetPersistentCharacterEntitySummariesAsync(userId, slot);

        if (!TryValidateSession(owner, userId, slot, out _) ||
            character == null ||
            expectedProfileId is { } expected && character.ProfileId != expected ||
            vehicles.Count == 0)
        {
            if (Exists(owner))
                RemComp<GarageComponent>(owner);
            return;
        }

        var garage = EnsureComp<GarageComponent>(owner);
        garage.ProfileId = character.ProfileId;
        _actions.AddAction(owner, ref garage.Action, garage.ActionPrototype);
        _ui.SetUi(owner, GarageUiKey.Key, new InterfaceData(ClientUiType, interactionRange: 0f));
    }

    private async Task UpdateAndOpenAsync(EntityUid owner, bool open)
    {
        if (!TryGetGarageSession(owner, out var garage, out var session, out var slot, out _))
            return;

        var vehicles = await _database.GetPersistentCharacterEntitySummariesAsync(session.UserId, slot);
        if (!TryValidateSession(owner, session.UserId, slot, out var refreshedSession) ||
            !TryComp<GarageComponent>(owner, out var refreshedGarage) ||
            refreshedGarage.ProfileId != garage.ProfileId)
        {
            return;
        }

        if (vehicles.Count == 0)
        {
            RemComp<GarageComponent>(owner);
            return;
        }

        var state = new GarageBoundUserInterfaceState(vehicles
            .Select(vehicle => new GarageVehicleData(vehicle.Id, vehicle.PrototypeId))
            .ToList());
        _ui.SetUiState(owner, GarageUiKey.Key, state);

        if (open)
            _ui.OpenUi(owner, GarageUiKey.Key, refreshedSession);
    }

    private bool TryGetGarageSession(
        EntityUid owner,
        [NotNullWhen(true)] out GarageComponent? garage,
        [NotNullWhen(true)] out ICommonSession? session,
        out int slot,
        out int profileId)
    {
        garage = null;
        session = null;
        slot = default;
        profileId = default;

        if (!TryComp(owner, out garage) ||
            garage.ProfileId <= 0 ||
            !_players.TryGetSessionByEntity(owner, out session) ||
            session.AttachedEntity != owner ||
            !_preferences.HavePreferencesLoaded(session) ||
            !_mobState.IsAlive(owner))
        {
            return false;
        }

        slot = _preferences.GetPreferences(session.UserId).SelectedCharacterIndex;
        profileId = garage.ProfileId;
        return true;
    }

    private bool TryValidateSession(
        EntityUid owner,
        NetUserId userId,
        int slot,
        [NotNullWhen(true)] out ICommonSession? session)
    {
        session = null;
        return Exists(owner) &&
               _players.TryGetSessionByEntity(owner, out session) &&
               session.UserId == userId &&
               session.AttachedEntity == owner &&
               _preferences.HavePreferencesLoaded(session) &&
               _preferences.GetPreferences(userId).SelectedCharacterIndex == slot;
    }

    private void SendAlreadyCalled(ICommonSession session)
    {
        _chat.DispatchServerMessage(session, Loc.GetString("garage-already-called"));
    }
}
