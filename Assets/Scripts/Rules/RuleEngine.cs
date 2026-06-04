using Curling.Core;

namespace Curling.Rules
{
    public class ShotResult
    {
        public EndState resultingEnd;
        public bool fgzViolation;
        public bool endComplete;
        public int endScore;
        public Team endScorer;
    }

    public class RuleEngine
    {
        public MatchSettings settings;

        public RuleEngine(MatchSettings s) { settings = s; }

        public ShotResult ApplyShot(EndState before, EndState afterPhysics, Team thrower)
        {
            var res = new ShotResult();

            if (FgzValidator.Violated(before, afterPhysics, thrower, settings))
            {
                var rollback = before.DeepClone();
                rollback.shot_num = before.shot_num + 1;
                res.resultingEnd = rollback;
                res.fgzViolation = true;
            }
            else
            {
                afterPhysics.shot_num = before.shot_num + 1;
                res.resultingEnd = afterPhysics;
            }

            if (res.resultingEnd.shot_num >= Constants.ShotsPerEndStandard)
            {
                res.endComplete = true;
                res.endScore = ScoreCalculator.Calculate(res.resultingEnd, out var scorer);
                res.endScorer = scorer;
            }
            return res;
        }

        public Team NextHammer(Team prevHammer, Team scorer, int score)
        {
            if (score == 0) return prevHammer;
            return scorer.Opponent();
        }

        public bool ShouldGoToExtraEnd(MatchScore score, int endsPlayed)
        {
            if (endsPlayed < settings.standard_end_count) return false;
            return score.Team0Total == score.Team1Total;
        }

        public Team? DetermineWinner(MatchScore score, int endsPlayed)
        {
            if (endsPlayed < settings.standard_end_count) return null;
            if (score.Team0Total > score.Team1Total) return Team.Team0;
            if (score.Team1Total > score.Team0Total) return Team.Team1;
            return null;
        }

        public Team NextEndHammer(EndState finishedEnd, int endScore, Team endScorer)
        {
            return NextHammer(finishedEnd.hammer, endScorer, endScore);
        }
    }
}
