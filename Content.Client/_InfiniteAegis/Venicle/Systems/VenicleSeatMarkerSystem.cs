using Content.Client.Gameplay;
using Content.Client.Viewport;
using Content.Shared.Venicle.Components;
using Robust.Client.GameObjects;
using Robust.Client.Input;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Venicle.Systems;

public sealed partial class VenicleSeatMarkerSystem : EntitySystem
{
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private EntityUid? _hoveredMarker;

    public override void Initialize()
    {
        SubscribeLocalEvent<VenicleSeatComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VenicleSeatComponent, AfterAutoHandleStateEvent>(OnAfterHandleState);
        SubscribeLocalEvent<VenicleSeatComponent, ComponentShutdown>(OnShutdown);
    }

    public override void FrameUpdate(float frameTime)
    {
        var hovered = GetHoveredMarker();
        if (hovered == _hoveredMarker)
            return;

        if (_hoveredMarker is { } previous)
            SetMarkerVisible(previous, false);

        _hoveredMarker = hovered;

        if (_hoveredMarker is { } current)
            SetMarkerVisible(current, true);
    }

    private void OnStartup(Entity<VenicleSeatComponent> entity, ref ComponentStartup args)
    {
        UpdateMarker(entity);
        SetMarkerVisible(entity, false);
    }

    private void OnAfterHandleState(Entity<VenicleSeatComponent> entity, ref AfterAutoHandleStateEvent args)
    {
        UpdateMarker(entity);
    }

    private void OnShutdown(Entity<VenicleSeatComponent> entity, ref ComponentShutdown args)
    {
        if (_hoveredMarker == entity.Owner)
            _hoveredMarker = null;
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

        return hovered is { } uid && HasComp<VenicleSeatComponent>(uid) ? uid : null;
    }

    private void UpdateMarker(Entity<VenicleSeatComponent> entity)
    {
        if (TryComp<SpriteComponent>(entity, out var sprite))
            _sprite.SetOffset((entity.Owner, sprite), entity.Comp.MarkerOffset);
    }

    private void SetMarkerVisible(EntityUid uid, bool visible)
    {
        if (TryComp<SpriteComponent>(uid, out var sprite))
            _sprite.SetColor((uid, sprite), visible ? Color.White.WithAlpha(0.85f) : Color.Transparent);
    }
}
