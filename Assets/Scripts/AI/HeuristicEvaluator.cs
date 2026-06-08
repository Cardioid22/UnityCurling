using System;
using Curling.Core;
using Curling.Rules;

namespace Curling.AI
{
    public static class HeuristicEvaluator
    {
        public static float Evaluate(EndState end, Team self)
        {
            float value = 0f;
            float bestSelfDist = float.MaxValue;
            float bestOppDist = float.MaxValue;

            foreach (var s in end.LiveStones())
            {
                if (s.IsInHouse())
                {
                    float d = s.DistanceToTee();
                    float closeness = 1f - (d / (Constants.HouseRadius + Constants.StoneRadius));
                    float houseValue = 2.0f + closeness * 4.0f;

                    if (s.team == self)
                    {
                        value += houseValue;
                        if (d < bestSelfDist) bestSelfDist = d;
                    }
                    else
                    {
                        value -= houseValue;
                        if (d < bestOppDist) bestOppDist = d;
                    }
                }
                else if (IsGuardZoneStone(s))
                {
                    float guardValue = GuardValue(s);
                    value += s.team == self ? guardValue : -guardValue * 0.85f;
                }
            }

            int score = ScoreCalculator.Calculate(end, out var scorer);
            if (score > 0)
                value += scorer == self ? score * 7.0f : -score * 7.0f;

            if (bestSelfDist < bestOppDist) value += 5.0f;
            else if (bestOppDist < bestSelfDist) value -= 5.0f;

            if (end.shot_num >= Constants.ShotsPerEndStandard - 2)
                value += EndShotBonus(score, scorer, self, end.hammer);

            return value;
        }

        static float EndShotBonus(int score, Team scorer, Team self, Team hammer)
        {
            if (score == 0)
                return hammer == self ? 2.0f : -1.0f;

            float signedScore = scorer == self ? score : -score;
            return signedScore * 4.0f;
        }

        static bool IsGuardZoneStone(StoneState s)
        {
            if (!s.in_play) return false;
            if (s.position.y < Constants.HogLineY) return false;
            if (s.position.y > Constants.TeeLineY) return false;
            return !s.IsInHouse();
        }

        static float GuardValue(StoneState s)
        {
            float centerLine = 1f - Math.Min(1f, Math.Abs(s.position.x) / Constants.SheetHalfWidth);
            float ySpan = Math.Max(0.001f, Constants.TeeLineY - Constants.HogLineY);
            float teeProgress = Math.Min(1f, Math.Max(0f, (s.position.y - Constants.HogLineY) / ySpan));
            return 0.8f + centerLine * 1.0f + teeProgress * 0.6f;
        }
    }
}
