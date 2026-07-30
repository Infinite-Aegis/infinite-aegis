using System.Numerics;
using Content.Shared.Damage.Systems;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;

namespace Content.Server.Vehicle;

public sealed partial class VehicleCollisionDamageSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleDamageComponent, StartCollideEvent>(OnStartCollide);
    }

    private void OnStartCollide(Entity<VehicleDamageComponent> vehicle, ref StartCollideEvent args)
    {
        var otherIsVehicle = HasComp<VehicleComponent>(args.OtherEntity);
        if (!args.OurFixture.Hard ||
            !args.OtherFixture.Hard ||
            !IsDamagingObstacle(otherIsVehicle, args.OtherBody.BodyType))
        {
            return;
        }

        var relativeVelocity = args.OurBody.LinearVelocity;
        if (otherIsVehicle)
            relativeVelocity -= args.OtherBody.LinearVelocity;

        var impactSpeed = CalculateImpactSpeed(relativeVelocity, args.WorldNormal);
        var damageScale = CalculateDamageScale(impactSpeed, vehicle.Comp.MinimumCollisionSpeed);
        if (damageScale <= 0f)
            return;

        _damageable.TryChangeDamage(
            vehicle.Owner,
            vehicle.Comp.CollisionDamagePerSpeed * damageScale,
            origin: args.OtherEntity);
    }

    public static bool IsDamagingObstacle(bool isVehicle, BodyType bodyType)
    {
        return isVehicle || bodyType == BodyType.Static;
    }

    public static float CalculateImpactSpeed(Vector2 relativeVelocity, Vector2 collisionNormal)
    {
        var normalLengthSquared = collisionNormal.LengthSquared();
        if (!float.IsFinite(normalLengthSquared) || normalLengthSquared <= 0f)
            return 0f;

        var normal = collisionNormal / MathF.Sqrt(normalLengthSquared);
        var speed = MathF.Abs(Vector2.Dot(relativeVelocity, normal));
        return float.IsFinite(speed) ? speed : 0f;
    }

    public static float CalculateDamageScale(float impactSpeed, float minimumSpeed)
    {
        if (!float.IsFinite(impactSpeed) || !float.IsFinite(minimumSpeed))
            return 0f;

        return MathF.Max(0f, impactSpeed - MathF.Max(0f, minimumSpeed));
    }
}
