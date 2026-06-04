using System.Collections.Generic;
using Curling.Core;

namespace Curling.AI
{
    public enum ShotKind
    {
        Draw,
        Guard,
        Takeout,
        Freeze,
        Tap,
        Hack
    }

    public class ShotPlan
    {
        public ShotKind kind;
        public Vec2 target;
        public Rotation rotation;
        public float speed;
        public float angular_velocity;
    }

    public static class ShotCatalog
    {
        public static IEnumerable<ShotPlan> Generate(EndState end, Team self)
        {
            yield return Draw(new Vec2(0f, Constants.HouseCenterY), Rotation.CCW);
            yield return Draw(new Vec2(0f, Constants.HouseCenterY), Rotation.CW);
            yield return Draw(new Vec2(-0.6f, Constants.HouseCenterY), Rotation.CCW);
            yield return Draw(new Vec2(0.6f, Constants.HouseCenterY), Rotation.CW);
            yield return Guard(new Vec2(0f, Constants.HogLineY + 1.0f), Rotation.CCW);
            yield return Guard(new Vec2(-0.5f, Constants.HogLineY + 1.5f), Rotation.CCW);
            yield return Guard(new Vec2(0.5f, Constants.HogLineY + 1.5f), Rotation.CW);
            yield return Takeout(new Vec2(0f, Constants.HouseCenterY), Rotation.CCW);
            yield return Takeout(new Vec2(0f, Constants.HouseCenterY), Rotation.CW);
            yield return Takeout(new Vec2(-1.0f, Constants.HouseCenterY), Rotation.CCW);
            yield return Takeout(new Vec2(1.0f, Constants.HouseCenterY), Rotation.CW);
        }

        public static ShotPlan Draw(Vec2 target, Rotation rot) => new ShotPlan
        {
            kind = ShotKind.Draw,
            target = target,
            rotation = rot,
            speed = SpeedForDistance(target.y),
            angular_velocity = AngularForRotation(rot, 1.57f)
        };

        public static ShotPlan Guard(Vec2 target, Rotation rot) => new ShotPlan
        {
            kind = ShotKind.Guard,
            target = target,
            rotation = rot,
            speed = SpeedForDistance(target.y) * 0.95f,
            angular_velocity = AngularForRotation(rot, 1.57f)
        };

        public static ShotPlan Takeout(Vec2 target, Rotation rot) => new ShotPlan
        {
            kind = ShotKind.Takeout,
            target = target,
            rotation = rot,
            speed = 3.6f,
            angular_velocity = AngularForRotation(rot, 1.57f)
        };

        static float SpeedForDistance(float targetY)
        {
            float dist = targetY;
            float s = 2.0f + 0.025f * (dist - 30f);
            if (s < 1.5f) s = 1.5f;
            if (s > Constants.MaxSpeed) s = Constants.MaxSpeed;
            return s;
        }

        static float AngularForRotation(Rotation r, float magnitude)
        {
            return r == Rotation.CW ? magnitude : -magnitude;
        }

        public static ShotInput ToShotInput(ShotPlan plan, Vec2 startPos)
        {
            Vec2 dir = (plan.target - startPos).Normalized;
            return new ShotInput(dir * plan.speed, plan.angular_velocity);
        }

        public static Vec2 DefaultStartPos() => new Vec2(0f, Constants.HogLineY - 12.0f);
    }
}
