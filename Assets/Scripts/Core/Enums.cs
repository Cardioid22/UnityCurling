namespace Curling.Core
{
    public enum Team
    {
        Team0 = 0,
        Team1 = 1
    }

    public enum Rotation
    {
        CW = 0,
        CCW = 1
    }

    public enum MatchPhase
    {
        Setup,
        InEnd,
        BetweenEnds,
        Finished
    }

    public enum CpuDifficulty
    {
        Easy,
        Normal,
        Hard
    }

    public static class TeamExtensions
    {
        public static Team Opponent(this Team t) => t == Team.Team0 ? Team.Team1 : Team.Team0;
    }
}
