using Robust.Shared.GameStates;

namespace Content.Shared.Vehicle.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class VehicleDamageComponent : Component
{
    [DataField]
    public float MaximumDamage = 500f;

    [ViewVariables]
    public VehicleDamageState State;

    public bool CanDrive => State != VehicleDamageState.Disabled;

    public float AccelerationModifier => State switch
    {
        VehicleDamageState.Damaged20 => 0.9f,
        VehicleDamageState.Damaged40 => 0.8f,
        VehicleDamageState.Damaged60 => 0.7f,
        VehicleDamageState.Damaged80 => 0.6f,
        VehicleDamageState.Disabled => 0f,
        _ => 1f,
    };

    public float MaximumSpeedModifier => State switch
    {
        VehicleDamageState.Damaged60 => 0.9f,
        VehicleDamageState.Damaged80 => 0.8f,
        VehicleDamageState.Disabled => 0f,
        _ => 1f,
    };
}

public enum VehicleDamageState : byte
{
    Normal,
    Damaged20,
    Damaged40,
    Damaged60,
    Damaged80,
    Disabled,
}
