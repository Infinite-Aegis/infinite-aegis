using Content.Shared.Interaction.Components;

namespace Content.Shared.Movement.Systems;

public abstract partial class SharedMoverController
{
    #region IA

    [Dependency] private EntityQuery<BlockMovementComponent> _blockMovementQuery = default!;

    private bool IsMobMovementBlocked(EntityUid uid)
    {
        return _blockMovementQuery.HasComp(uid);
    }

    #endregion
}
