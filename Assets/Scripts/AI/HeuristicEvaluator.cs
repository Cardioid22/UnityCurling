using System;
using Curling.Core;

namespace Curling.AI
{
    public static class HeuristicEvaluator
    {
        public static float Evaluate(EndState end, Team self)
        {
            Team opp = self.Opponent();
            float selfScore = 0f;
            float oppScore = 0f;
            float bestSelfDist = float.MaxValue;
            float bestOppDist = float.MaxValue;

            foreach (var s in end.LiveStones())
            {
                if (!s.IsInHouse()) continue;
                float d = s.DistanceToTee();
                float closeness = 1f - (d / (Constants.HouseRadius + Constants.StoneRadius));
                if (s.team == self)
                {
                    selfScore += 1.5f + closeness * 2.0f;
                    if (d < bestSelfDist) bestSelfDist = d;
                }
                else
                {
                    oppScore += 1.5f + closeness * 2.0f;
                    if (d < bestOppDist) bestOppDist = d;
                }
            }

            float closestBonus = 0f;
            if (bestSelfDist < bestOppDist) closestBonus = 4.0f;
            else if (bestOppDist < bestSelfDist) closestBonus = -4.0f;

            return selfScore - oppScore + closestBonus;
        }
    }
}
