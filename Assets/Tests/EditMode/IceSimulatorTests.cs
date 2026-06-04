#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using Curling.Core;
using Curling.Physics;

namespace Curling.Tests.EditMode
{
    public class IceSimulatorTests
    {
        [Test]
        public void Step_AdvancesMovingStone()
        {
            var stone = new StoneState
            {
                position = new Vec2(0f, Constants.HogLineY - 12f),
                linear_velocity = new Vec2(0f, 2.85f),
                angular_velocity = 1.57f,
                in_play = true
            };
            var simulator = new IceSimulator { Dt = 0.02f };

            bool moving = simulator.Step(new List<StoneState> { stone });

            Assert.IsTrue(moving);
            Assert.Greater(stone.position.y, Constants.HogLineY - 12f);
            Assert.Greater(stone.linear_velocity.Magnitude, 0f);
        }

        [Test]
        public void Step_ClockwiseForwardStoneCurlsTowardPositiveX()
        {
            var stone = new StoneState
            {
                position = new Vec2(0f, 20f),
                linear_velocity = new Vec2(0f, 2.85f),
                angular_velocity = 1.57f,
                in_play = true
            };
            var simulator = new IceSimulator { Dt = 0.02f };

            simulator.Step(new List<StoneState> { stone });

            Assert.Greater(stone.position.x, 0f);
        }
    }
}
#endif
