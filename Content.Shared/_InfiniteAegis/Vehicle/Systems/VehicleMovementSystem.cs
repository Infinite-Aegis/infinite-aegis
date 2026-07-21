using System.Numerics;
using Content.Shared.ActionBlocker;
using Content.Shared.Friction;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Vehicle.Systems;

public sealed partial class VehicleMovementSystem : VirtualController
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(SharedMoverController));
        UpdatesBefore.Add(typeof(TileFrictionController));

        SubscribeLocalEvent<VehicleMovementComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VehicleMovementComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<VehicleMovementComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VehicleMovementComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<VehicleMovementComponent, TileFrictionEvent>(OnTileFriction);
    }

    private void OnStartup(Entity<VehicleMovementComponent> ent, ref ComponentStartup args)
    {
        _actionBlocker.UpdateCanMove(ent.Owner);
    }

    private void OnMapInit(Entity<VehicleMovementComponent> ent, ref MapInitEvent args)
    {
        RefreshInertia(ent.Owner, ent.Comp);
    }

    public bool RefreshInertia(EntityUid uid, VehicleMovementComponent? component = null)
    {
        if (!Resolve(uid, ref component) || !TryComp<PhysicsComponent>(uid, out var physics))
            return false;

        if (physics.BodyType != BodyType.Dynamic)
            return true;

        var multiplier = MathF.Max(1f, component.AngularInertiaMultiplier);
        var inertia = component.BaseAngularInertia ?? physics.Inertia;
        if (inertia <= 0f)
            return false;

        if (multiplier <= 1f)
        {
            component.BaseAngularInertia = null;
            component.AppliedAngularInertiaMultiplier = 1f;
            _physics.SetInertia(uid, physics, inertia);
            return true;
        }

        component.BaseAngularInertia = inertia;
        component.AppliedAngularInertiaMultiplier = multiplier;
        _physics.SetInertia(uid, physics, inertia * multiplier);
        return true;
    }

    private void OnShutdown(Entity<VehicleMovementComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.BaseAngularInertia is not { } inertia ||
            !TryComp<PhysicsComponent>(ent, out var physics) ||
            physics.BodyType != BodyType.Dynamic)
        {
            return;
        }

        _physics.SetInertia(ent, physics, inertia);
        ent.Comp.BaseAngularInertia = null;
        ent.Comp.AppliedAngularInertiaMultiplier = 1f;
    }

    private void OnUpdateCanMove(Entity<VehicleMovementComponent> ent, ref UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    private void OnTileFriction(Entity<VehicleMovementComponent> ent, ref TileFrictionEvent args)
    {
        args.Modifier *= ent.Comp.TileFrictionModifier;
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        if (frameTime <= 0f)
            return;

        var query = EntityQueryEnumerator<VehicleMovementComponent, InputMoverComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var vehicle, out var mover, out var physics, out var xform))
        {
            if (prediction && !physics.Predict ||
                physics.BodyType != BodyType.Dynamic ||
                physics.BodyStatus != BodyStatus.OnGround)
            {
                continue;
            }

            Move((uid, vehicle, mover, physics, xform), frameTime);
        }
    }

    private void Move(Entity<VehicleMovementComponent, InputMoverComponent, PhysicsComponent, TransformComponent> ent, float frameTime)
    {
        var (uid, vehicle, mover, physics, xform) = ent;
        var buttons = SharedMoverController.GetNormalizedMovement(mover.HeldMoveButtons);
        var throttleInput = GetThrottleInput(buttons);
        var steeringInput = GetSteeringInput(buttons);

        UpdateSteering(uid, vehicle, steeringInput, frameTime);

        var rotation = _transform.GetWorldRotation(xform);
        var right = rotation.RotateVec(Vector2.UnitX);
        var forward = rotation.RotateVec(-Vector2.UnitY);
        var forwardSpeed = Vector2.Dot(physics.LinearVelocity, forward);
        var hasEnginePower = !TryComp<VehicleFuelTankComponent>(uid, out var fuelTank) || fuelTank.HasFuel;
        var accelerationModifier = 1f;
        var maximumSpeedModifier = 1f;

        if (TryComp<VehicleDamageComponent>(uid, out var damage))
        {
            hasEnginePower &= damage.CanDrive;
            accelerationModifier = damage.AccelerationModifier;
            maximumSpeedModifier = damage.MaximumSpeedModifier;
        }

        ApplyDrive(
            uid,
            physics,
            vehicle,
            forward,
            forwardSpeed,
            throttleInput,
            hasEnginePower,
            accelerationModifier,
            maximumSpeedModifier,
            frameTime);
        ApplyResistance(uid, physics, vehicle, frameTime);
        ApplyAxleGrip(uid, physics, vehicle, rotation, right, frameTime);
        ApplyAngularResistance(uid, physics, vehicle, frameTime);
    }

    private static float GetThrottleInput(MoveButtons buttons)
    {
        var input = 0f;

        if ((buttons & MoveButtons.Up) == MoveButtons.Up)
            input += 1f;

        if ((buttons & MoveButtons.Down) == MoveButtons.Down)
            input -= 1f;

        return input;
    }

    private static float GetSteeringInput(MoveButtons buttons)
    {
        var input = 0f;

        if ((buttons & MoveButtons.Left) == MoveButtons.Left)
            input += 1f;

        if ((buttons & MoveButtons.Right) == MoveButtons.Right)
            input -= 1f;

        return input;
    }

    private void UpdateSteering(EntityUid uid, VehicleMovementComponent vehicle, float input, float frameTime)
    {
        var oldSteering = vehicle.CurrentSteering;
        var rate = input == 0f ? vehicle.SteeringReturnRate : vehicle.SteeringRate;
        vehicle.CurrentSteering = Approach(vehicle.CurrentSteering, input, MathF.Max(0f, rate) * frameTime);
        vehicle.CurrentSteering = Math.Clamp(vehicle.CurrentSteering, -1f, 1f);

        if (MathF.Abs(oldSteering - vehicle.CurrentSteering) > 0.0001f)
            Dirty(uid, vehicle);
    }

    private void ApplyDrive(
        EntityUid uid,
        PhysicsComponent physics,
        VehicleMovementComponent vehicle,
        Vector2 forward,
        float forwardSpeed,
        float input,
        bool hasEnginePower,
        float accelerationModifier,
        float maximumSpeedModifier,
        float frameTime)
    {
        if (input > 0f)
        {
            if (forwardSpeed < -0.05f)
            {
                ApplyLongitudinalBrake(uid, physics, forward, forwardSpeed, vehicle.BrakeForce, frameTime);
                return;
            }

            if (!hasEnginePower)
                return;

            var force = GetLimitedEngineForce(
                forwardSpeed,
                MathF.Max(0f, vehicle.MaxForwardSpeed * maximumSpeedModifier),
                MathF.Max(0f, vehicle.ForwardEngineForce * accelerationModifier));
            _physics.ApplyForce(uid, forward * force * input, body: physics);
            return;
        }

        if (input < 0f)
        {
            if (forwardSpeed > 0.05f)
            {
                ApplyLongitudinalBrake(uid, physics, forward, forwardSpeed, vehicle.BrakeForce, frameTime);
                return;
            }

            if (!hasEnginePower)
                return;

            var force = GetLimitedEngineForce(
                -forwardSpeed,
                MathF.Max(0f, vehicle.MaxReverseSpeed * maximumSpeedModifier),
                MathF.Max(0f, vehicle.ReverseEngineForce * accelerationModifier));
            _physics.ApplyForce(uid, -forward * force * -input, body: physics);
        }
    }

    private void ApplyResistance(
        EntityUid uid,
        PhysicsComponent physics,
        VehicleMovementComponent vehicle,
        float frameTime)
    {
        var velocity = physics.LinearVelocity;
        var speed = velocity.Length();
        if (speed < 0.0001f)
            return;

        var magnitude = MathF.Max(0f, vehicle.RollingResistance) +
                        MathF.Max(0f, vehicle.AerodynamicDrag) * speed * speed;
        magnitude = MathF.Min(magnitude, speed * physics.Mass / frameTime);
        _physics.ApplyForce(uid, -velocity / speed * magnitude, body: physics);
    }

    private void ApplyAxleGrip(
        EntityUid uid,
        PhysicsComponent physics,
        VehicleMovementComponent vehicle,
        Angle rotation,
        Vector2 rearRight,
        float frameTime)
    {
        var halfWheelBase = MathF.Max(0.01f, vehicle.WheelBase) * 0.5f;
        var frontPoint = -Vector2.UnitY * halfWheelBase;
        var rearPoint = -frontPoint;
        var steeringAngle = new Angle((float) vehicle.MaxSteeringAngle.Theta * vehicle.CurrentSteering);
        var frontRight = (rotation + steeringAngle).RotateVec(Vector2.UnitX);

        ApplyLateralGrip(
            uid,
            physics,
            frontPoint,
            rotation,
            frontRight,
            vehicle.FrontCorneringStiffness,
            vehicle.MaxLateralGrip,
            vehicle.AppliedAngularInertiaMultiplier,
            frameTime);
        ApplyLateralGrip(
            uid,
            physics,
            rearPoint,
            rotation,
            rearRight,
            vehicle.RearCorneringStiffness,
            vehicle.MaxLateralGrip,
            vehicle.AppliedAngularInertiaMultiplier,
            frameTime);
    }

    private void ApplyLateralGrip(
        EntityUid uid,
        PhysicsComponent physics,
        Vector2 localPoint,
        Angle rotation,
        Vector2 right,
        float stiffness,
        float maxGrip,
        float angularInertiaMultiplier,
        float frameTime)
    {
        var lever = rotation.RotateVec(localPoint - physics.LocalCenter);
        var pointVelocity = physics.LinearVelocity + Vector2Helpers.Cross(physics.AngularVelocity, lever);
        var lateralSpeed = Vector2.Dot(pointVelocity, right);
        if (MathF.Abs(lateralSpeed) < 0.0001f)
            return;

        var magnitude = MathF.Abs(lateralSpeed) * MathF.Max(0f, stiffness);
        magnitude = MathF.Min(magnitude, MathF.Max(0f, maxGrip));
        magnitude = MathF.Min(magnitude, MathF.Abs(lateralSpeed) * physics.Mass * 0.5f / frameTime);
        var force = -MathF.Sign(lateralSpeed) * right * magnitude;
        _physics.ApplyForce(uid, force, body: physics);
        _physics.ApplyTorque(
            uid,
            Vector2Helpers.Cross(lever, force) * angularInertiaMultiplier,
            body: physics);
    }

    private void ApplyAngularResistance(
        EntityUid uid,
        PhysicsComponent physics,
        VehicleMovementComponent vehicle,
        float frameTime)
    {
        if (MathF.Abs(physics.AngularVelocity) < 0.0001f || physics.InvI <= 0f)
            return;

        var steeringResistance = 1f +
                                 (MathF.Max(0f, vehicle.SteeringAngularResistanceModifier) - 1f) *
                                 MathF.Abs(vehicle.CurrentSteering);
        var magnitude = MathF.Abs(physics.AngularVelocity) *
                        MathF.Max(0f, vehicle.AngularResistance) *
                        steeringResistance *
                        vehicle.AppliedAngularInertiaMultiplier;
        magnitude = MathF.Min(magnitude, MathF.Abs(physics.AngularVelocity) / physics.InvI / frameTime);
        _physics.ApplyTorque(uid, -MathF.Sign(physics.AngularVelocity) * magnitude, body: physics);
    }

    private void ApplyLongitudinalBrake(
        EntityUid uid,
        PhysicsComponent physics,
        Vector2 forward,
        float forwardSpeed,
        float brakeForce,
        float frameTime)
    {
        var magnitude = MathF.Min(
            MathF.Max(0f, brakeForce),
            MathF.Abs(forwardSpeed) * physics.Mass / frameTime);
        _physics.ApplyForce(uid, -MathF.Sign(forwardSpeed) * forward * magnitude, body: physics);
    }

    private static float GetLimitedEngineForce(float speed, float maxSpeed, float engineForce)
    {
        if (maxSpeed <= 0f || speed >= maxSpeed)
            return 0f;

        var ratio = Math.Clamp(MathF.Max(0f, speed) / maxSpeed, 0f, 1f);
        return engineForce * (1f - MathF.Pow(ratio, 4f));
    }

    private static float Approach(float value, float target, float amount)
    {
        if (value < target)
            return MathF.Min(value + amount, target);

        if (value > target)
            return MathF.Max(value - amount, target);

        return target;
    }
}
