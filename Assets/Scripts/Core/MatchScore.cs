using System;
using System.Collections.Generic;

namespace Curling.Core
{
    [Serializable]
    public class MatchScore
    {
        public List<int> team0_per_end = new List<int>();
        public List<int> team1_per_end = new List<int>();

        public int Team0Total
        {
            get { int s = 0; foreach (var v in team0_per_end) s += v; return s; }
        }

        public int Team1Total
        {
            get { int s = 0; foreach (var v in team1_per_end) s += v; return s; }
        }

        public int EndsPlayed => Math.Min(team0_per_end.Count, team1_per_end.Count);

        public void RecordEnd(int team0Score, int team1Score)
        {
            team0_per_end.Add(team0Score);
            team1_per_end.Add(team1Score);
        }

        public Team? LeaderOrNull()
        {
            if (Team0Total > Team1Total) return Team.Team0;
            if (Team1Total > Team0Total) return Team.Team1;
            return null;
        }
    }
}
