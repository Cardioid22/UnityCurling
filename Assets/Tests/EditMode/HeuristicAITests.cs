#if UNITY_INCLUDE_TESTS
using System.Threading;
using NUnit.Framework;
using Curling.AI;
using Curling.Core;
using Curling.Physics;

namespace Curling.Tests.EditMode
{
    public class HeuristicAITests
    {
        [Test]
        public void HardSearch_FindsHouseDrawOnEmptyEnd()
        {
            var settings = new MatchSettings
            {
                cpu_difficulty = CpuDifficulty.Hard,
                first_hammer = Team.Team0
            };
            var state = new MatchState(settings);
            var ai = new HeuristicAI(CpuDifficulty.Hard, 123);

            var shot = ai.DecideAsync(state, Team.Team1, CancellationToken.None).Result;

            var end = state.current_end.DeepClone();
            int idx = Constants.StonesPerTeamStandard;
            var stone = end.stones[idx];
            stone.team = Team.Team1;
            stone.stone_index = idx;
            stone.position = ShotCatalog.DefaultStartPos();
            stone.linear_velocity = shot.velocity;
            stone.angular_velocity = shot.angular_velocity;
            stone.in_play = true;

            var sim = new IceSimulator { Dt = 0.02f, MaxSteps = 6000 };
            sim.SimulateToRest(end.stones);
            sim.RemoveBeforeHogLine(end.stones);

            Assert.IsTrue(end.stones[idx].in_play, $"Stone stopped out of play at {end.stones[idx].position}");
            Assert.IsTrue(end.stones[idx].IsInHouse(), $"Stone stopped outside the house at {end.stones[idx].position}");
        }
    }
}
#endif
