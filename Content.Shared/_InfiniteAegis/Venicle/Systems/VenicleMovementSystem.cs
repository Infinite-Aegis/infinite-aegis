using System.Numerics;
using Content.Shared.ActionBlocker;
using Content.Shared.Friction;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Venicle.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Venicle.Systems;

public sealed partial class VenicleMovementSystem : VirtualController
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(SharedMoverController));
        UpdatesBefore.Add(typeof(TileFrictionController));

        SubscribeLocalEvent<VenicleMovementComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VenicleMovementComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<VenicleMovementComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VenicleMovementComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<VenicleMovementComponent, TileFrictionEvent>(OnTileFriction);
    }

    private void OnStartup(Entity<VenicleMovementComponent> ent, ref ComponentStartup args)
    {
        _actionBlocker.UpdateCanMove(ent.Owner);
    }

    private void OnMapInit(Entity<VenicleMovementComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<PhysicsComponent>(ent, out var physics) || physics.BodyType != BodyType.Dynamic)
            return;

        var multiplier = MathF.Max(1f, ent.Comp.AngularInertiaMultiplier);
        if (multiplier <= 1f || physics.Inertia <= 0f)
            return;

        ent.Comp.BaseAngularInertia = physics.Inertia;
        ent.Comp.AppliedAngularInertiaMultiplier = multiplier;
        _physics.SetInertia(ent, physics, physics.Inertia * multiplier);
    }

    private void OnShutdown(Entity<VenicleMovementComponent> ent, ref ComponentShutdown args)
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

    private void OnUpdateCanMove(Entity<VenicleMovementComponent> ent, ref UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    private void OnTileFriction(Entity<VenicleMovementComponent> ent, ref TileFrictionEvent args)
    {
        args.Modifier *= ent.Comp.TileFrictionModifier;
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        if (frameTime <= 0f)
            return;

        var query = EntityQueryEnumerator<VenicleMovementComponent, InputMoverComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var venicle, out var mover, out var physics, out var xform))
        {
            if (prediction && !physics.Predict ||
                physics.BodyType != BodyType.Dynamic ||
                physics.BodyStatus != BodyStatus.OnGround)
            {
                continue;
            }

            Move((uid, venicle, mover, physics, xform), frameTime);
        }
    }

    private void Move(Entity<VenicleMovementComponent, InputMoverComponent, PhysicsComponent, TransformComponent> ent, float frameTime)
    {
        var (uid, venicle, mover, physics, xform) = ent;
        var buttons = SharedMoverController.GetNormalizedMovement(mover.HeldMoveButtons);
        var throttleInput = GetThrottleInput(buttons);
        var steeringInput = GetSteeringInput(buttons);

        UpdateSteering(uid, venicle, steeringInput, frameTime);

        var rotation = _transform.GetWorldRotation(xform);
        var right = rotation.RotateVec(Vector2.UnitX);
        var forward = rotation.RotateVec(-Vector2.UnitY);
        var forwardSpeed = Vector2.Dot(physics.LinearVelocity, forward);
        var hasEnginePower = !TryComp<VenicleFuelTankComponent>(uid, out var fuelTank) || fuelTank.HasFuel;
        var accelerationModifier = 1f;
        var maximumSpeedModifier = 1f;

        if (TryComp<VenicleDamageComponent>(uid, out var damage))
        {
            hasEnginePower &= damage.CanDrive;
            accelerationModifier = damage.AccelerationModifier;
            maximumSpeedModifier = damage.MaximumSpeedModifier;
        }

        ApplyDrive(
            uid,
            physics,
            venicle,
            forward,
            forwardSpeed,
            throttleInput,
            hasEnginePower,
            accelerationModifier,
            maximumSpeedModifier,
            frameTime);
        ApplyResistance(uid, physics, venicle, frameTime);
        ApplyAxleGrip(uid, physics, venicle, rotation, right, frameTime);
        ApplyAngularResistance(uid, physics, venicle, frameTime);
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

    private void UpdateSteering(EntityUid uid, VenicleMovementComponent venicle, float input, float frameTime)
    {
        var oldSteering = venicle.CurrentSteering;
        var rate = input == 0f ? venicle.SteeringReturnRate : venicle.SteeringRate;
        venicle.CurrentSteering = Approach(venicle.CurrentSteering, input, MathF.Max(0f, rate) * frameTime);
        venicle.CurrentSteering = Math.Clamp(venicle.CurrentSteering, -1f, 1f);

        if (MathF.Abs(oldSteering - venicle.CurrentSteering) > 0.0001f)
            Dirty(uid, venicle);
    }

    private void ApplyDrive(
        EntityUid uid,
        PhysicsComponent physics,
        VenicleMovementComponent venicle,
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
                ApplyLongitudinalBrake(uid, physics, forward, forwardSpeed, venicle.BrakeForce, frameTime);
                return;
            }

            if (!hasEnginePower)
                return;

            var force = GetLimitedEngineForce(
                forwardSpeed,
                MathF.Max(0f, venicle.MaxForwardSpeed * maximumSpeedModifier),
                MathF.Max(0f, venicle.ForwardEngineForce * accelerationModifier));
            _physics.ApplyForce(uid, forward * force * input, body: physics);
            return;
        }

        if (input < 0f)
        {
            if (forwardSpeed > 0.05f)
            {
                ApplyLongitudinalBrake(uid, physics, forward, forwardSpeed, venicle.BrakeForce, frameTime);
                return;
            }

            if (!hasEnginePower)
                return;

            var force = GetLimitedEngineForce(
                -forwardSpeed,
                MathF.Max(0f, venicle.MaxReverseSpeed * maximumSpeedModifier),
                MathF.Max(0f, venicle.ReverseEngineForce * accelerationModifier));
            _physics.ApplyForce(uid, -forward * force * -input, body: physics);
        }
    }

    private void ApplyResistance(
        EntityUid uid,
        PhysicsComponent physics,
        VenicleMovementComponent venicle,
        float frameTime)
    {
        var velocity = physics.LinearVelocity;
        var speed = velocity.Length();
        if (speed < 0.0001f)
            return;

        var magnitude = MathF.Max(0f, venicle.RollingResistance) +
                        MathF.Max(0f, venicle.AerodynamicDrag) * speed * speed;
        magnitude = MathF.Min(magnitude, speed * physics.Mass / frameTime);
        _physics.ApplyForce(uid, -velocity / speed * magnitude, body: physics);
    }

    private void ApplyAxleGrip(
        EntityUid uid,
        PhysicsComponent physics,
        VenicleMovementComponent venicle,
        Angle rotation,
        Vector2 rearRight,
        float frameTime)
    {
        var halfWheelBase = MathF.Max(0.01f, venicle.WheelBase) * 0.5f;
        var frontPoint = -Vector2.UnitY * halfWheelBase;
        var rearPoint = -frontPoint;
        var steeringAngle = new Angle((float) venicle.MaxSteeringAngle.Theta * venicle.CurrentSteering);
        var frontRight = (rotation + steeringAngle).RotateVec(Vector2.UnitX);

        ApplyLateralGrip(
            uid,
            physics,
            frontPoint,
            rotation,
            frontRight,
            venicle.FrontCorneringStiffness,
            venicle.MaxLateralGrip,
            venicle.AppliedAngularInertiaMultiplier,
            frameTime);
        ApplyLateralGrip(
            uid,
            physics,
            rearPoint,
            rotation,
            rearRight,
            venicle.RearCorneringStiffness,
            venicle.MaxLateralGrip,
            venicle.AppliedAngularInertiaMultiplier,
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
        VenicleMovementComponent venicle,
        float frameTime)
    {
        if (MathF.Abs(physics.AngularVelocity) < 0.0001f || physics.InvI <= 0f)
            return;

        var steeringResistance = 1f +
                                 (MathF.Max(0f, venicle.SteeringAngularResistanceModifier) - 1f) *
                                 MathF.Abs(venicle.CurrentSteering);
        var magnitude = MathF.Abs(physics.AngularVelocity) *
                        MathF.Max(0f, venicle.AngularResistance) *
                        steeringResistance *
                        venicle.AppliedAngularInertiaMultiplier;
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
