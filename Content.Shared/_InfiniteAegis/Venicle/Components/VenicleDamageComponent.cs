using Robust.Shared.GameStates;

namespace Content.Shared.Venicle.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class VenicleDamageComponent : Component
{
    [DataField]
    public float MaximumDamage = 500f;

    [ViewVariables]
    public VenicleDamageState State;

    public bool CanDrive => State != VenicleDamageState.Disabled;

    public float AccelerationModifier => State switch
    {
        VenicleDamageState.Damaged20 => 0.9f,
        VenicleDamageState.Damaged40 => 0.8f,
        VenicleDamageState.Damaged60 => 0.7f,
        VenicleDamageState.Damaged80 => 0.6f,
        VenicleDamageState.Disabled => 0f,
        _ => 1f,
    };

    public float MaximumSpeedModifier => State switch
    {
        VenicleDamageState.Damaged60 => 0.9f,
        VenicleDamageState.Damaged80 => 0.8f,
        VenicleDamageState.Disabled => 0f,
        _ => 1f,
    };
}

public enum VenicleDamageState : byte
{
    Normal,
    Damaged20,
    Damaged40,
    Damaged60,
    Damaged80,
    Disabled,
}
