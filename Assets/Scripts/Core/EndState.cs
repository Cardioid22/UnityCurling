using System;
using System.Collections.Generic;

namespace Curling.Core
{
    [Serializable]
    public class EndState
    {
        public List<StoneState> stones = new List<StoneState>();
        public int shot_num;
        public Team hammer;
        public int end_index;

        public EndState() { }

        public EndState DeepClone()
        {
            var c = new EndState
            {
                shot_num = shot_num,
                hammer = hammer,
                end_index = end_index,
                stones = new List<StoneState>(stones.Count)
            };
            foreach (var s in stones) c.stones.Add(s.Clone());
            return c;
        }

        public Team NextToThrow()
        {
            bool firstIsHammer = hammer == Team.Team0;
            int idx = shot_num;
            bool teamTurn = (idx % 2 == 0) ? !firstIsHammer : firstIsHammer;
            return teamTurn ? Team.Team0 : Team.Team1;
        }

        public IEnumerable<StoneState> LiveStones()
        {
            foreach (var s in stones) if (s.in_play) yield return s;
        }
    }
}
