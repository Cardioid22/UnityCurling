#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Curling.Core;
using Curling.Rules;

namespace Curling.Tests.EditMode
{
    public class RuleEngineTests
    {
        EndState NewEmptyEnd(Team hammer)
        {
            var e = new EndState { hammer = hammer, shot_num = 0, end_index = 0 };
            for (int i = 0; i < Constants.StonesPerTeamStandard * 2; i++)
            {
                e.stones.Add(new StoneState
                {
                    in_play = false,
                    stone_index = i,
                    team = i < Constants.StonesPerTeamStandard ? Team.Team0 : Team.Team1
                });
            }
            return e;
        }

        StoneState PlaceStone(EndState e, int idx, Team t, Vec2 pos)
        {
            var s = e.stones[idx];
            s.team = t;
            s.position = pos;
            s.in_play = true;
            s.linear_velocity = Vec2.Zero;
            s.angular_velocity = 0f;
            return s;
        }

        [Test]
        public void SingleStoneAtCenter_ScoresOne()
        {
            var e = NewEmptyEnd(Team.Team0);
            PlaceStone(e, 0, Team.Team0, new Vec2(Constants.HouseCenterX, Constants.HouseCenterY));
            int score = ScoreCalculator.Calculate(e, out var scorer);
            Assert.AreEqual(Team.Team0, scorer);
            Assert.AreEqual(1, score);
        }

        [Test]
        public void NoStonesInHouse_ScoresZero()
        {
            var e = NewEmptyEnd(Team.Team0);
            PlaceStone(e, 0, Team.Team0, new Vec2(0f, Constants.HogLineY + 0.5f));
            int score = ScoreCalculator.Calculate(e, out _);
            Assert.AreEqual(0, score);
        }

        [Test]
        public void ClosestStoneTeam_WinsEnd()
        {
            var e = NewEmptyEnd(Team.Team1);
            PlaceStone(e, 0, Team.Team0, new Vec2(0.5f, Constants.HouseCenterY));
            PlaceStone(e, Constants.StonesPerTeamStandard, Team.Team1, new Vec2(0.2f, Constants.HouseCenterY));
            int score = ScoreCalculator.Calculate(e, out var scorer);
            Assert.AreEqual(Team.Team1, scorer);
            Assert.AreEqual(1, score);
        }

        [Test]
        public void ConsecutiveStones_CountTowardScore()
        {
            var e = NewEmptyEnd(Team.Team0);
            PlaceStone(e, 0, Team.Team0, new Vec2(0.1f, Constants.HouseCenterY));
            PlaceStone(e, 1, Team.Team0, new Vec2(0.3f, Constants.HouseCenterY));
            PlaceStone(e, 2, Team.Team0, new Vec2(0.6f, Constants.HouseCenterY));
            PlaceStone(e, Constants.StonesPerTeamStandard, Team.Team1, new Vec2(0.9f, Constants.HouseCenterY));
            int score = ScoreCalculator.Calculate(e, out var scorer);
            Assert.AreEqual(Team.Team0, scorer);
            Assert.AreEqual(3, score);
        }

        [Test]
        public void HammerPreservedOnBlankEnd()
        {
            var rules = new RuleEngine(new MatchSettings());
            var next = rules.NextHammer(Team.Team0, Team.Team0, 0);
            Assert.AreEqual(Team.Team0, next);
        }

        [Test]
        public void HammerSwitchesAfterScore()
        {
            var rules = new RuleEngine(new MatchSettings());
            var next = rules.NextHammer(Team.Team0, Team.Team0, 2);
            Assert.AreEqual(Team.Team1, next);
        }

        [Test]
        public void FgzViolationDetected_WhenOpponentGuardRemovedInFirstFiveShots()
        {
            var settings = new MatchSettings { five_rock_rule = true };
            var before = NewEmptyEnd(Team.Team0);
            before.shot_num = 1;
            PlaceStone(before, Constants.StonesPerTeamStandard, Team.Team1,
                new Vec2(0f, Constants.HogLineY + 1.0f));

            var after = before.DeepClone();
            after.stones[Constants.StonesPerTeamStandard].in_play = false;

            bool violated = FgzValidator.Violated(before, after, Team.Team0, settings);
            Assert.IsTrue(violated);
        }

        [Test]
        public void FgzNotApplied_AfterFifthShot()
        {
            var settings = new MatchSettings { five_rock_rule = true };
            var before = NewEmptyEnd(Team.Team0);
            before.shot_num = 5;
            PlaceStone(before, Constants.StonesPerTeamStandard, Team.Team1,
                new Vec2(0f, Constants.HogLineY + 1.0f));

            var after = before.DeepClone();
            after.stones[Constants.StonesPerTeamStandard].in_play = false;

            bool violated = FgzValidator.Violated(before, after, Team.Team0, settings);
            Assert.IsFalse(violated);
        }
    }
}
#endif
