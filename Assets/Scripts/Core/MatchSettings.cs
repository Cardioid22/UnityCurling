using System;

namespace Curling.Core
{
    [Serializable]
    public class MatchSettings
    {
        public byte standard_end_count = Constants.DefaultStandardEndCount;
        public bool five_rock_rule = true;
        public bool no_tick_rule = false;
        public float thinking_time_sec = Constants.DefaultThinkingTimeSec;
        public float extra_end_thinking_time_sec = Constants.DefaultExtraEndThinkingTimeSec;
        public Team first_hammer = Team.Team1;
        public float max_speed = Constants.MaxSpeed;
        public PlayerSkill[] team0_players = new PlayerSkill[Constants.PlayersPerTeamStandard];
        public PlayerSkill[] team1_players = new PlayerSkill[Constants.PlayersPerTeamStandard];
        public CpuDifficulty cpu_difficulty = CpuDifficulty.Normal;
        public Team human_team = Team.Team0;

        public MatchSettings() { FillDefaultSkills(); }

        public void FillDefaultSkills()
        {
            for (int i = 0; i < Constants.PlayersPerTeamStandard; i++)
            {
                if (team0_players[i] == null) team0_players[i] = new PlayerSkill();
                if (team1_players[i] == null) team1_players[i] = new PlayerSkill();
            }
        }

        public int ShotsPerEnd => Constants.ShotsPerEndStandard;
        public int StonesPerTeam => Constants.StonesPerTeamStandard;
    }
}
