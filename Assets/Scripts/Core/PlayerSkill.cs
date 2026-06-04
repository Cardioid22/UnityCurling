using System;

namespace Curling.Core
{
    [Serializable]
    public class PlayerSkill
    {
        public float stddev_speed = Constants.DefaultStddevSpeed;
        public float stddev_angle = Constants.DefaultStddevAngle;
        public int seed = -1;

        public PlayerSkill() { }
        public PlayerSkill(float ss, float sa) { stddev_speed = ss; stddev_angle = sa; }
    }
}
