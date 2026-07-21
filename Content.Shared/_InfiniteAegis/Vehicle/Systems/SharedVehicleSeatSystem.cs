using Content.Shared.ActionBlocker;
using Content.Shared.DragDrop;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Vehicle.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Physics.Components;

namespace Content.Shared.Vehicle.Systems;

public abstract partial class SharedVehicleSeatSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleSeatMarkerComponent, CanDropTargetEvent>(OnCanDropTarget);
    }

    private void OnCanDropTarget(Entity<VehicleSeatMarkerComponent> entity, ref CanDropTargetEvent args)
    {
        if (args.Handled || !CanUseSeat(entity, args.User, args.Dragged))
            return;

        // Invalid vehicle seats deliberately leave the event unhandled. DragDropSystem
        // interprets that as "not a target" instead of drawing its red invalid outline.
        args.CanDrop = true;
        args.Handled = true;
    }

    public bool CanUseSeat(
        Entity<VehicleSeatMarkerComponent> marker,
        EntityUid actor,
        EntityUid occupant,
        bool allowReservation = false)
    {
        if (!TryComp(marker.Comp.Vehicle, out VehicleComponent? vehicle) ||
            (!marker.Comp.Available &&
             (!allowReservation || marker.Comp.PendingOccupant != occupant)) ||
            HasComp<VehicleOccupantComponent>(occupant) ||
            _whitelist.IsWhitelistFail(vehicle.OccupantWhitelist, occupant) ||
            IsMoving(marker.Comp.Vehicle, vehicle) ||
            !_actionBlocker.CanInteract(actor, marker.Owner) ||
            !_interaction.InRangeUnobstructed(actor, marker.Owner))
        {
            return false;
        }

        if (actor == occupant)
            return _actionBlocker.CanMove(occupant);

        return HasComp<HandsComponent>(actor) &&
               _interaction.InRangeUnobstructed(actor, occupant);
    }

    protected bool IsMoving(EntityUid uid, VehicleComponent component)
    {
        if (!TryComp(uid, out PhysicsComponent? physics))
            return false;

        var maxSpeed = MathF.Max(0f, component.MaxSeatInteractionSpeed);
        var maxAngularSpeed = MathF.Max(0f, component.MaxSeatInteractionAngularSpeed);
        return physics.LinearVelocity.LengthSquared() > maxSpeed * maxSpeed ||
               MathF.Abs(physics.AngularVelocity) > maxAngularSpeed;
    }
}
