using System;

namespace Curling.Core
{
    [Serializable]
    public class ShotInput
    {
        public Vec2 velocity;
        public float angular_velocity;

        public ShotInput() { }
        public ShotInput(Vec2 v, float w)
        {
            velocity = v;
            angular_velocity = w;
        }

        public float ShotAngle => (float)Math.Atan2(velocity.y, velocity.x);
        public float Speed => velocity.Magnitude;
        public Rotation Rotation => angular_velocity >= 0f ? Rotation.CW : Rotation.CCW;

        public ShotInput Clone() => new ShotInput(velocity, angular_velocity);
    }
}
