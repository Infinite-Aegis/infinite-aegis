using Content.Client.Gameplay;
using Content.Client.Interaction;
using Content.Client.Viewport;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Vehicle.Systems;

public sealed partial class VehicleSeatMarkerSystem : SharedVehicleSeatSystem
{
    [Dependency] private DragDropSystem _dragDrop = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleSeatMarkerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VehicleSeatMarkerComponent, AfterAutoHandleStateEvent>(OnAfterHandleState);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var actor = _player.LocalEntity;
        var dragged = _dragDrop.DraggedEntity;
        var occupant = dragged ?? actor;
        var dragging = dragged != null;

        var query = EntityQueryEnumerator<VehicleSeatMarkerComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var seat, out var sprite))
        {
            var available = actor != null &&
                            occupant != null &&
                            CanUseSeat((uid, seat), actor.Value, occupant.Value);

            SetMarkerState(uid, sprite, available, available && dragging);
        }

        if (!dragging && actor is { } localActor && GetHoveredMarker() is { } hovered &&
            TryComp<VehicleSeatMarkerComponent>(hovered, out var hoveredSeat) &&
            TryComp<SpriteComponent>(hovered, out var hoveredSprite) &&
            CanUseSeat((hovered, hoveredSeat), localActor, localActor))
        {
            SetMarkerState(hovered, hoveredSprite, true, true);
        }
    }

    private void OnStartup(Entity<VehicleSeatMarkerComponent> entity, ref ComponentStartup args)
    {
        UpdateMarker(entity);
        if (TryComp<SpriteComponent>(entity, out var sprite))
            SetMarkerState(entity, sprite, false, false);
    }

    private void OnAfterHandleState(Entity<VehicleSeatMarkerComponent> entity, ref AfterAutoHandleStateEvent args)
    {
        UpdateMarker(entity);
    }

    private EntityUid? GetHoveredMarker()
    {
        if (_state.CurrentState is not GameplayStateBase screen ||
            _ui.CurrentlyHovered is not IViewportControl viewport ||
            !_input.MouseScreenPosition.IsValid)
        {
            return null;
        }

        var mouseCoordinates = viewport.PixelToMap(_input.MouseScreenPosition.Position);
        var hovered = viewport is ScalingViewport scaling
            ? screen.GetClickedEntity(mouseCoordinates, scaling.Eye)
            : screen.GetClickedEntity(mouseCoordinates);

        return hovered is { } uid && HasComp<VehicleSeatMarkerComponent>(uid) ? uid : null;
    }

    private void UpdateMarker(Entity<VehicleSeatMarkerComponent> entity)
    {
        if (TryComp<SpriteComponent>(entity, out var sprite))
            _sprite.SetOffset((entity.Owner, sprite), entity.Comp.MarkerOffset);
    }

    private void SetMarkerState(EntityUid uid, SpriteComponent sprite, bool available, bool highlighted)
    {
        _sprite.SetVisible((uid, sprite), available);
        _sprite.SetColor((uid, sprite), highlighted ? Color.White.WithAlpha(0.85f) : Color.Transparent);
    }
}
