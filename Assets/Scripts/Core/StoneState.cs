using System;

namespace Curling.Core
{
    [Serializable]
    public class StoneState
    {
        public Vec2 position;
        public Vec2 linear_velocity;
        public float angular_velocity;
        public bool in_play;
        public Team team;
        public int stone_index;

        public StoneState() { in_play = false; }

        public StoneState Clone() => new StoneState
        {
            position = position,
            linear_velocity = linear_velocity,
            angular_velocity = angular_velocity,
            in_play = in_play,
            team = team,
            stone_index = stone_index
        };

        public bool IsStill =>
            linear_velocity.Magnitude < Constants.StopLinearEps &&
            Math.Abs(angular_velocity) < Constants.StopAngularEps;

        public float DistanceToTee()
        {
            float dx = position.x - Constants.HouseCenterX;
            float dy = position.y - Constants.HouseCenterY;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public bool IsInHouse()
        {
            if (!in_play) return false;
            return DistanceToTee() <= Constants.HouseRadius + Constants.StoneRadius;
        }
    }
}
