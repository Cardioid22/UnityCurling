using System.Collections.Generic;
using Curling.Core;

namespace Curling.Rules
{
    public static class ScoreCalculator
    {
        public static int Calculate(EndState end, out Team scorer)
        {
            scorer = Team.Team0;
            var inHouse = new List<StoneState>();
            foreach (var s in end.stones)
            {
                if (!s.in_play) continue;
                if (s.IsInHouse()) inHouse.Add(s);
            }

            if (inHouse.Count == 0) return 0;

            inHouse.Sort((a, b) => a.DistanceToTee().CompareTo(b.DistanceToTee()));

            scorer = inHouse[0].team;
            int score = 0;
            foreach (var s in inHouse)
            {
                if (s.team == scorer) score++;
                else break;
            }
            return score;
        }
    }
}
