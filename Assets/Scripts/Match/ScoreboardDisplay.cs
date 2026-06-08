using UnityEngine;
using Curling.Core;

namespace Curling.Match
{
    // 背景(シート奥の壁)にある得点掲示板に、現在のスコアを 3D テキストで小さく表示する。
    // MatchManager から実行時に生成されるためシーンの編集は不要。
    //
    // 配置はシーンの Scoreboard_* オブジェクトに合わせてある:
    //   Scoreboard_Bg        world (0,   3.0, 41.27)  幅7.5 x 高1.8
    //   Scoreboard_TeamRed   world (-2.4,3.5, 41.24)  ← RED 側
    //   Scoreboard_TeamYellow world(2.4, 3.5, 41.24)  ← YEL 側
    // 前方追従カメラ(投擲側=低Z から +Z を見る)から読めるよう、TextMesh は identity 回転で置く。
    // 文字サイズ・位置は Inspector で微調整可能。日本語は組み込みフォントで豆腐になるため英数字のみ。
    public class ScoreboardDisplay : MonoBehaviour
    {
        public MatchManager manager;

        [Header("掲示板の正面 (ワールド座標)")]
        [Tooltip("テキストを描く Z。掲示板パネル(z≈41.24)より少し手前=投擲側(小さい Z)に置く。")]
        public float boardFrontZ = 41.18f;

        [Header("テキスト位置 (ワールド X/Y)")]
        public Vector2 redLabelPos = new Vector2(-2.4f, 3.45f);
        public Vector2 yellowLabelPos = new Vector2(2.4f, 3.45f);
        public Vector2 redScorePos = new Vector2(-0.75f, 2.95f);
        public Vector2 dashPos = new Vector2(0f, 2.95f);
        public Vector2 yellowScorePos = new Vector2(0.75f, 2.95f);
        public Vector2 titlePos = new Vector2(0f, 2.42f);

        [Header("文字サイズ (TextMesh characterSize)")]
        public int fontResolution = 90;
        public float scoreCharacterSize = 0.060f;
        public float labelCharacterSize = 0.033f;
        public float titleCharacterSize = 0.028f;

        static readonly Color Red = new Color(1f, 0.34f, 0.34f);
        static readonly Color Yellow = new Color(1f, 0.85f, 0.22f);
        static readonly Color Neutral = new Color(0.85f, 0.90f, 0.98f);

        Transform _root;
        TextMesh _redScore;
        TextMesh _yelScore;
        TextMesh _title;
        Font _font;

        void Start()
        {
            BuildTexts();
            Refresh();
        }

        void LateUpdate()
        {
            Refresh();
        }

        void BuildTexts()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 親なしのルート。シートのワールド座標をそのまま使う(原点・等倍・無回転)。
            _root = new GameObject("Scoreboard3D").transform;

            MakeText("RedLabel", redLabelPos, labelCharacterSize, Red, "RED");
            MakeText("YelLabel", yellowLabelPos, labelCharacterSize, Yellow, "YEL");
            _redScore = MakeText("RedScore", redScorePos, scoreCharacterSize, Red, "0");
            MakeText("Dash", dashPos, scoreCharacterSize, Neutral, "-");
            _yelScore = MakeText("YelScore", yellowScorePos, scoreCharacterSize, Yellow, "0");
            _title = MakeText("Title", titlePos, titleCharacterSize, Neutral, "End 1 / 1");
        }

        TextMesh MakeText(string name, Vector2 worldXY, float charSize, Color color, string initial)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = new Vector3(worldXY.x, worldXY.y, boardFrontZ);
            go.transform.localRotation = Quaternion.identity;

            var tm = go.AddComponent<TextMesh>();
            tm.text = initial;
            tm.font = _font;
            tm.fontSize = fontResolution;
            tm.characterSize = charSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;

            // 実行時生成の TextMesh はフォントのマテリアルを明示しないと描画されない。
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && _font != null) mr.sharedMaterial = _font.material;

            return tm;
        }

        void Refresh()
        {
            if (manager == null || _title == null) return;
            var st = manager.State;
            if (st == null || st.score == null) return;

            _redScore.text = st.score.Team0Total.ToString();
            _yelScore.text = st.score.Team1Total.ToString();

            int totalEnds = Mathf.Max(1, st.settings != null ? st.settings.standard_end_count : 1);
            if (st.phase == MatchPhase.Finished)
            {
                if (st.winner == Team.Team0) { _title.text = "RED WINS"; _title.color = Red; }
                else if (st.winner == Team.Team1) { _title.text = "YEL WINS"; _title.color = Yellow; }
                else { _title.text = "DRAW"; _title.color = Neutral; }
            }
            else
            {
                int endNum = Mathf.Clamp((st.current_end != null ? st.current_end.end_index : 0) + 1, 1, totalEnds);
                _title.text = $"End {endNum} / {totalEnds}";
                _title.color = Neutral;
            }
        }
    }
}
