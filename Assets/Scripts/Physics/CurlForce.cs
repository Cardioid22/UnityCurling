using System;
using Curling.Core;

namespace Curling.Physics
{
    public static class CurlForce
    {
        public static Vec2 Compute(Vec2 velocity, float angularVelocity, float kCurl, float p)
        {
            float speed = velocity.Magnitude;
            if (speed < 1e-6f || Math.Abs(angularVelocity) < 1e-6f) return Vec2.Zero;

            Vec2 dir = velocity / speed;
            Vec2 lateral = dir.Perpendicular();
            float sign = angularVelocity >= 0f ? 1f : -1f;
            float magnitude = -kCurl * sign * (float)Math.Pow(speed, p);
            return lateral * magnitude;
        }

        public static Vec2 FrictionAccel(Vec2 velocity, float mu, float g)
        {
            float speed = velocity.Magnitude;
            if (speed < 1e-6f) return Vec2.Zero;
            return -(velocity / speed) * (mu * g);
        }

        public static float AngularDecel(float angularVelocity, float kOmega)
        {
            if (Math.Abs(angularVelocity) < 1e-6f) return 0f;
            return -Math.Sign(angularVelocity) * kOmega;
        }
    }
}
