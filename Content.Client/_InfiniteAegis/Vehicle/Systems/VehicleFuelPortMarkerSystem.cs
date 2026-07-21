using Content.Client.Gameplay;
using Content.Client.Viewport;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Vehicle.Systems;

public sealed partial class VehicleFuelPortMarkerSystem : EntitySystem
{
    [Dependency] private VehicleRefuelingSystem _fuel = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleFuelPortComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VehicleFuelPortComponent, AfterAutoHandleStateEvent>(OnAfterHandleState);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var user = _player.LocalEntity;
        var used = user is { } actor ? _hands.GetActiveItem(actor) : null;
        var hovered = GetHoveredMarker();

        var query = EntityQueryEnumerator<VehicleFuelPortComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var port, out var sprite))
        {
            var available = user is { } localUser &&
                            used is { } activeItem &&
                            _fuel.CanAttemptRefuel((uid, port), localUser, activeItem);

            SetMarkerState(uid, sprite, available, available && hovered == uid);
        }
    }

    private void OnStartup(Entity<VehicleFuelPortComponent> port, ref ComponentStartup args)
    {
        UpdateMarker(port);
        if (TryComp<SpriteComponent>(port, out var sprite))
            SetMarkerState(port, sprite, false, false);
    }

    private void OnAfterHandleState(Entity<VehicleFuelPortComponent> port, ref AfterAutoHandleStateEvent args)
    {
        UpdateMarker(port);
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

        return hovered is { } uid && HasComp<VehicleFuelPortComponent>(uid) ? uid : null;
    }

    private void UpdateMarker(Entity<VehicleFuelPortComponent> port)
    {
        if (TryComp<SpriteComponent>(port, out var sprite))
            _sprite.SetOffset((port.Owner, sprite), port.Comp.MarkerOffset);
    }

    private void SetMarkerState(EntityUid uid, SpriteComponent sprite, bool available, bool highlighted)
    {
        _sprite.SetVisible((uid, sprite), available);
        _sprite.SetColor((uid, sprite), highlighted ? Color.White.WithAlpha(0.85f) : Color.Transparent);
    }
}
