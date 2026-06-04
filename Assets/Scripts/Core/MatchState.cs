using System;

namespace Curling.Core
{
    [Serializable]
    public class MatchState
    {
        public MatchSettings settings;
        public EndState current_end;
        public MatchScore score;
        public bool conceded;
        public Team? winner;
        public MatchPhase phase = MatchPhase.Setup;
        public float team0_time_remaining;
        public float team1_time_remaining;
        public bool in_extra_end;

        public MatchState() { }

        public MatchState(MatchSettings s)
        {
            settings = s;
            score = new MatchScore();
            team0_time_remaining = s.thinking_time_sec;
            team1_time_remaining = s.thinking_time_sec;
            current_end = NewEnd(0, s.first_hammer);
            phase = MatchPhase.InEnd;
        }

        public EndState NewEnd(int endIndex, Team hammer)
        {
            var e = new EndState
            {
                end_index = endIndex,
                hammer = hammer,
                shot_num = 0
            };
            int totalStones = Constants.StonesPerTeamStandard * 2;
            for (int i = 0; i < totalStones; i++)
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

        public float TimeRemaining(Team t) =>
            t == Team.Team0 ? team0_time_remaining : team1_time_remaining;

        public void ConsumeTime(Team t, float dt)
        {
            if (t == Team.Team0) team0_time_remaining = Math.Max(0f, team0_time_remaining - dt);
            else team1_time_remaining = Math.Max(0f, team1_time_remaining - dt);
        }

        public void GrantExtraEndTime()
        {
            team0_time_remaining += settings.extra_end_thinking_time_sec;
            team1_time_remaining += settings.extra_end_thinking_time_sec;
            in_extra_end = true;
        }
    }
}
