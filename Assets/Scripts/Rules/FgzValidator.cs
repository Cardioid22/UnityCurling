using System.Collections.Generic;
using Curling.Core;

namespace Curling.Rules
{
    public static class FgzValidator
    {
        public const int FgzShotCount = 5;

        public static bool IsInFgz(StoneState s)
        {
            if (!s.in_play) return false;
            if (s.position.y < Constants.HogLineY) return false;
            if (s.position.y > Constants.TeeLineY) return false;
            if (s.IsInHouse()) return false;
            return true;
        }

        public static bool ShouldEnforceFgz(EndState before, MatchSettings settings)
        {
            return (settings.five_rock_rule || settings.no_tick_rule) && before.shot_num < FgzShotCount;
        }

        public static bool Violated(EndState before, EndState after, Team thrower, MatchSettings settings)
        {
            if (!ShouldEnforceFgz(before, settings)) return false;

            Team opponent = thrower.Opponent();

            for (int i = 0; i < before.stones.Count; i++)
            {
                var sBefore = before.stones[i];
                if (sBefore.team != opponent) continue;
                if (!IsInFgz(sBefore)) continue;

                var sAfter = FindStoneByIndex(after, sBefore.stone_index);
                if (sAfter == null) continue;

                if (!sAfter.in_play) return true;

                if (settings.no_tick_rule)
                {
                    float dx = sAfter.position.x - sBefore.position.x;
                    float dy = sAfter.position.y - sBefore.position.y;
                    if (dx * dx + dy * dy > 1e-6f) return true;
                }
            }
            return false;
        }

        static StoneState FindStoneByIndex(EndState e, int idx)
        {
            foreach (var s in e.stones) if (s.stone_index == idx) return s;
            return null;
        }
    }
}
