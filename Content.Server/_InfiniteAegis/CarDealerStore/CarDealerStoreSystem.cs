using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.Garage;
using Content.Server.Popups;
using Content.Server.Preferences.Managers;
using Content.Shared.CarDealerStore;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.CarDealerStore;

public sealed partial class CarDealerStoreSystem : EntitySystem
{
    private const long Balance = 0;

    [Dependency] private IServerDbManager _database = default!;
    [Dependency] private IServerPreferencesManager _preferences = default!;
    [Dependency] private ISharedPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private PersistentCharacterEntitySystem _persistence = default!;
    [Dependency] private GarageSystem _garage = default!;

    private readonly HashSet<NetUserId> _purchasesInFlight = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CarDealerStoreComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<CarDealerStoreComponent, CarDealerStoreRequestUpdateMessage>(OnRequestUpdate);
        SubscribeLocalEvent<CarDealerStoreComponent, CarDealerStoreBuyMessage>(OnBuy);
    }

    private void OnBeforeUiOpen(
        Entity<CarDealerStoreComponent> merchant,
        ref BeforeActivatableUIOpenEvent args)
    {
        _ = UpdateUiAsync(merchant, args.User);
    }

    private void OnRequestUpdate(
        Entity<CarDealerStoreComponent> merchant,
        ref CarDealerStoreRequestUpdateMessage args)
    {
        _ = UpdateUiAsync(merchant, args.Actor);
    }

    private void OnBuy(
        Entity<CarDealerStoreComponent> merchant,
        ref CarDealerStoreBuyMessage args)
    {
        _ = BuyAsync(merchant, args.Actor, args.OfferId, args.RequestId);
    }

    private async Task BuyAsync(
        Entity<CarDealerStoreComponent> merchant,
        EntityUid actor,
        string requestedOfferId,
        Guid requestId)
    {
        if (!TryGetValidatedSession(merchant, actor, out var session) ||
            requestId == Guid.Empty ||
            !_purchasesInFlight.Add(session.UserId))
        {
            return;
        }

        try
        {
            var offerId = merchant.Comp.Offers.FirstOrDefault(x => x.Id == requestedOfferId);
            if (offerId.Id == null || !_prototypes.TryIndex(offerId, out var offer))
                return;

            if (offer.Price < 0 || !_prototypes.HasIndex(offer.Product))
                return;

            var slot = _preferences.GetPreferences(session.UserId).SelectedCharacterIndex;
            var characterState = await _database.GetCarDealerStoreCharacterStateAsync(session.UserId, slot);
            if (!TryGetValidatedSession(merchant, actor, out var refreshedSession) ||
                refreshedSession.UserId != session.UserId)
            {
                return;
            }

            if (characterState == null)
            {
                _popup.PopupEntity(Loc.GetString("car-dealer-store-purchase-failed"), actor, actor);
                return;
            }

            if (Balance < offer.Price)
            {
                _popup.PopupEntity(Loc.GetString("car-dealer-store-insufficient-funds"), actor, actor);
                return;
            }

            var persistentId = Guid.NewGuid();
            if (!_persistence.TryCreatePurchaseSnapshot(
                    offer.Product,
                    persistentId,
                    characterState.ProfileId,
                    actor,
                    out var serializedState))
            {
                _popup.PopupEntity(Loc.GetString("car-dealer-store-purchase-failed"), actor, actor);
                return;
            }

            var result = await _database.PurchasePersistentEntityAsync(
                session.UserId,
                slot,
                persistentId,
                requestId,
                offer.ID,
                offer.Product.Id,
                serializedState,
                offer.Price);

            if (!Exists(actor))
                return;

            if (result.Status == CarDealerStorePurchaseStatus.Success)
            {
                _garage.NotifyStoreWrite(
                    actor,
                    session.UserId,
                    slot,
                    result.ProfileId ?? characterState.ProfileId);
                _popup.PopupEntity(Loc.GetString("car-dealer-store-purchase-success"), actor, actor);
            }
            else
            {
                var message = result.Status switch
                {
                    CarDealerStorePurchaseStatus.InsufficientFunds => "car-dealer-store-insufficient-funds",
                    _ => "car-dealer-store-purchase-failed",
                };
                _popup.PopupEntity(Loc.GetString(message), actor, actor);
            }
        }
        finally
        {
            _purchasesInFlight.Remove(session.UserId);
            if (Exists(merchant.Owner) && Exists(actor))
                await UpdateUiAsync(merchant, actor);
        }
    }

    private async Task UpdateUiAsync(Entity<CarDealerStoreComponent> merchant, EntityUid actor)
    {
        if (!TryGetValidatedSession(merchant, actor, out var session))
            return;

        var slot = _preferences.GetPreferences(session.UserId).SelectedCharacterIndex;
        var characterState = await _database.GetCarDealerStoreCharacterStateAsync(session.UserId, slot);
        if (characterState == null ||
            !TryGetValidatedSession(merchant, actor, out var refreshedSession) ||
            refreshedSession.UserId != session.UserId)
        {
            return;
        }

        var offers = new List<CarDealerStoreOfferData>(merchant.Comp.Offers.Count);
        foreach (var offerId in merchant.Comp.Offers)
        {
            if (!_prototypes.TryIndex(offerId, out var offer) || offer.Price < 0)
                continue;

            offers.Add(new CarDealerStoreOfferData(
                offer.ID,
                offer.Product.Id,
                offer.Description?.Id,
                offer.Price));
        }

        _ui.SetUiState(
            merchant.Owner,
            CarDealerStoreUiKey.Key,
            new CarDealerStoreBoundUserInterfaceState(Balance, offers));
    }

    private bool TryGetValidatedSession(
        Entity<CarDealerStoreComponent> merchant,
        EntityUid actor,
        [NotNullWhen(true)] out ICommonSession? session)
    {
        session = null;
        return Exists(merchant.Owner) &&
               Exists(actor) &&
               _players.TryGetSessionByEntity(actor, out session) &&
               session.AttachedEntity == actor &&
               _preferences.HavePreferencesLoaded(session) &&
               _mobState.IsAlive(actor) &&
               _interaction.InRangeUnobstructed(actor, merchant.Owner);
    }
}
