using System.Collections.Generic;
using System.Text;
using Curling.Core;

namespace Curling.Match
{
    /// <summary>
    /// 直前のショットに関する付随情報。RuleEngine.ShotResult から MatchManager が組み立てる。
    /// </summary>
    public struct ShotInfo
    {
        public Team thrower;     // 直前に投げたチーム
        public bool endComplete; // このショットでエンドが終了したか
        public int endScore;     // 終了時の得点（endComplete のときのみ意味を持つ）
        public Team endScorer;   // 得点したチーム（endScore > 0 のときのみ意味を持つ）
    }

    /// <summary>
    /// MatchState を、LLM(ずんだもん実況)に渡す日本語の盤面要約テキストへ変換する純ロジック。
    /// Unity 非依存。呼び出し時点の状態を同期的に文字列化する（後続の状態変化と競合しないため）。
    /// </summary>
    public static class CurlingDescriber
    {
        public static string Describe(MatchState s, ShotInfo shot)
        {
            if (s == null || s.current_end == null) return "(状況不明)";

            var end = s.current_end;
            Team human = s.settings != null ? s.settings.human_team : Team.Team0;
            string Name(Team t) => t == human ? "プレイヤー" : "CPU";

            var sb = new StringBuilder();

            int endNo = end.end_index + 1;
            int totalEnds = s.settings != null ? s.settings.standard_end_count : 0;
            sb.AppendLine($"第{endNo}エンド（全{totalEnds}エンド）/ 第{end.shot_num}投を投げ終えた（1エンドは全16投）");
            sb.AppendLine($"ハンマー（後攻）: {Name(end.hammer)}");
            sb.AppendLine($"直前に投げたのは: {Name(shot.thrower)}");

            int humanScore = human == Team.Team0 ? s.score.Team0Total : s.score.Team1Total;
            int cpuScore = human == Team.Team0 ? s.score.Team1Total : s.score.Team0Total;
            sb.AppendLine($"累計スコア（消化済みエンドまで）: プレイヤー {humanScore} - CPU {cpuScore}（消化 {s.score.EndsPlayed}エンド）");

            var live = new List<StoneState>();
            foreach (var st in end.LiveStones()) live.Add(st);

            int inHouse = 0, humanInHouse = 0, cpuInHouse = 0;
            StoneState closest = null;
            float closestDist = float.MaxValue;
            foreach (var st in live)
            {
                if (!st.IsInHouse()) continue;
                inHouse++;
                if (st.team == human) humanInHouse++; else cpuInHouse++;
                float d = st.DistanceToTee();
                if (d < closestDist) { closestDist = d; closest = st; }
            }

            sb.AppendLine($"ハウス内の石: 合計{inHouse}個（プレイヤー{humanInHouse} / CPU{cpuInHouse}）");
            if (closest != null)
                sb.AppendLine($"ティーに最も近い石（ナンバーワン）: {Name(closest.team)}（ティーから{closestDist:F2}m）");
            else
                sb.AppendLine("ハウス内に石は無い");

            if (live.Count > 0)
            {
                sb.AppendLine("石の配置:");
                foreach (var st in live)
                {
                    string zone = st.IsInHouse()
                        ? "ハウス内"
                        : (st.position.y >= Constants.HogLineY ? "ガードゾーン" : "手前");
                    string side = st.position.x < -Constants.StoneRadius
                        ? "左"
                        : (st.position.x > Constants.StoneRadius ? "右" : "中央");
                    sb.AppendLine($"- {Name(st.team)}: {zone}/{side}、ティーから{st.DistanceToTee():F2}m");
                }
            }

            if (shot.endComplete)
            {
                if (shot.endScore > 0)
                    sb.AppendLine($"★このエンド終了: {Name(shot.endScorer)}が{shot.endScore}点を獲得");
                else
                    sb.AppendLine("★このエンドはブランクエンド（0点）");
            }

            if (s.phase == MatchPhase.Finished || s.winner != null)
            {
                if (s.conceded) sb.AppendLine("★コンシード（降参）で試合終了");
                if (s.winner != null) sb.AppendLine($"★試合終了！勝者は {Name(s.winner.Value)}");
                else sb.AppendLine("★試合終了（引き分け）");
            }

            return sb.ToString();
        }
    }
}
