using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Curling.Core;
using Curling.Rules;
using Curling.AI;
using CCore = Curling.Core.Constants;
using CurlingStoneBody = Curling.Physics.StoneBody;
using ShotInputController = Curling.Input.ShotInputController;

namespace Curling.Match
{
    public class MatchManager : MonoBehaviour
    {
        public ShotInputController humanInput;
        public CurlingStoneBody[] stonePool;
        public Transform stoneSpawnPoint;
        public float postSettleHoldSeconds = 1f;
        public float shotTimeoutSeconds = 90f;

        [Header("CPU Timing")]
        public float cpuDecisionTimeoutSeconds = 5f;
        public float minimumCpuThinkSeconds = 1.5f;

        [Header("Animation Skip")]
        public KeyCode fastForwardKey = KeyCode.LeftShift;
        public KeyCode skipKey = KeyCode.S;
        public float fastForwardScale = 4f;

        [Header("Interrupt")]
        public KeyCode interruptKey = KeyCode.Escape;

        MatchState _state;
        RuleEngine _rules;
        IShotDecider _cpu;
        CancellationTokenSource _cts;
        bool _physicsInProgress;
        bool _skipRequested;
        bool _interrupted;

        [Header("Commentary (Ollama + VOICEVOX)")]
        public CommentaryService commentary;

        public MatchState State => _state;

        [Header("Scoreboard (3D 掲示板表示)")]
        public ScoreboardDisplay scoreboard;

        void Awake()
        {
            UnityEngine.Physics.gravity = new Vector3(0f, -CCore.Gravity, 0f);
            EnsureCommentary();
            EnsureScoreboard();
        }

        void EnsureCommentary()
        {
            if (commentary == null) commentary = GetComponent<CommentaryService>();
            if (commentary == null) commentary = gameObject.AddComponent<CommentaryService>();
        }

        void EnsureScoreboard()
        {
            if (scoreboard == null) scoreboard = GetComponent<ScoreboardDisplay>();
            if (scoreboard == null) scoreboard = gameObject.AddComponent<ScoreboardDisplay>();
            scoreboard.manager = this;
        }

        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(interruptKey))
            {
                if (_interrupted) QuitGame();
                else InterruptGame();
                return;
            }

            if (_interrupted)
            {
                Time.timeScale = 0f;
                return;
            }

            bool ff = UnityEngine.Input.GetKey(fastForwardKey) || UnityEngine.Input.GetKey(KeyCode.RightShift);
            Time.timeScale = ff ? fastForwardScale : 1f;
            if (_physicsInProgress && UnityEngine.Input.GetKeyDown(skipKey))
            {
                _skipRequested = true;
            }
        }

        void OnDisable()
        {
            Time.timeScale = 1f;
        }

        void OnGUI()
        {
            if (_interrupted)
            {
                DrawInterruptedOverlay();
                return;
            }

            var box = new GUIStyle(GUI.skin.box) { fontSize = 12, alignment = TextAnchor.UpperLeft };
            string ffState = (Time.timeScale > 1.01f) ? $"<color=#7fff7f>{Time.timeScale:F1}x</color>" : "1.0x";
            string txt =
                $"[ Animation ]\n" +
                $"Shift hold : 早送り ({fastForwardScale:F1}x)  now: {ffState}\n" +
                $"S         : 物理スキップ (投擲中)";
            GUI.Box(new Rect(12, Screen.height - 80, 360, 68), txt, box);
        }

        void DrawInterruptedOverlay()
        {
            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            var body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };

            float w = 360f;
            float h = 130f;
            var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(rect, string.Empty);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 24f, rect.width - 24f, 38f), "Game interrupted", title);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 70f, rect.width - 24f, 32f), "Press Esc again to quit.", body);
        }

        public void InterruptGame()
        {
            if (_interrupted) return;

            _interrupted = true;
            _physicsInProgress = false;
            _skipRequested = false;
            _cts?.Cancel();
            if (humanInput != null) humanInput.SetHumanTurn(false);
            StopAllCoroutines();
            Time.timeScale = 0f;
            Debug.Log("[Curling] Match interrupted by Escape.");
        }

        public void QuitGame()
        {
            Time.timeScale = 1f;
            Debug.Log("[Curling] Quitting after second Escape.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void StartNewMatch(MatchSettings settings)
        {
            _interrupted = false;
            Time.timeScale = 1f;
            _state = new MatchState(settings);
            _rules = new RuleEngine(settings);
            _cpu = new HeuristicAI(settings.cpu_difficulty);
            if (humanInput != null) humanInput.SetHumanTurn(false);
            ResetStonesForEnd();
            StartCoroutine(GameLoop());
        }

        IEnumerator GameLoop()
        {
            while (_state.phase != MatchPhase.Finished)
            {
                Team next = _state.current_end.NextToThrow();
                StageNextStone(next);
                ShotInput shot = null;

                if (next == _state.settings.human_team)
                {
                    if (humanInput == null)
                    {
                        Debug.LogError("[Curling] Human shot requested without a ShotInputController.");
                        yield break;
                    }

                    humanInput.SetHumanTurn(true);
                    shot = null;
                    bool fired = false;
                    void Handler(ShotInput s) { shot = s; fired = true; }
                    humanInput.OnShotFired += Handler;
                    while (!fired) yield return null;
                    humanInput.OnShotFired -= Handler;
                    humanInput.SetHumanTurn(false);
                }
                else
                {
                    if (humanInput != null) humanInput.SetHumanTurn(false);
                    float decisionTimeout = Mathf.Max(0.1f, Mathf.Min(_state.settings.thinking_time_sec, cpuDecisionTimeoutSeconds));
                    _cts = new CancellationTokenSource(Mathf.CeilToInt(decisionTimeout * 1000f));
                    var task = _cpu.DecideAsync(_state, next, _cts.Token);
                    float t0 = Time.realtimeSinceStartup;
                    while (!task.IsCompleted && Time.realtimeSinceStartup - t0 < decisionTimeout) yield return null;

                    if (!task.IsCompleted)
                    {
                        _cts.Cancel();
                        shot = BuildCpuTimeoutFallbackShot();
                        Debug.LogWarning($"[Curling] CPU decision exceeded {decisionTimeout:F1}s. Using fallback shot.");
                    }
                    else if (task.IsFaulted || task.IsCanceled)
                    {
                        shot = BuildCpuTimeoutFallbackShot();
                        Debug.LogWarning($"[Curling] CPU decision failed. Using fallback shot. status={task.Status}");
                    }
                    else
                    {
                        float minimumThink = Mathf.Min(minimumCpuThinkSeconds, decisionTimeout);
                        while (Time.realtimeSinceStartup - t0 < minimumThink) yield return null;
                        shot = task.Result;
                    }
                }

                yield return StartCoroutine(PerformShot(next, shot));

                if (_state.phase == MatchPhase.Finished) yield break;
            }
        }

        IEnumerator PerformShot(Team thrower, ShotInput shot)
        {
            var before = _state.current_end.DeepClone();

            int slot = NextEmptyStoneSlot(thrower);
            if (slot < 0) yield break;

            Vector3 spawn = SpawnPosition();
            var afterPhysics = before.DeepClone();
            var thrownStone = afterPhysics.stones[slot];
            thrownStone.team = thrower;
            thrownStone.stone_index = slot;
            thrownStone.position = CurlingStoneBody.PlanePos(spawn);
            thrownStone.linear_velocity = shot.velocity;
            thrownStone.angular_velocity = shot.angular_velocity;
            thrownStone.in_play = true;

            var body = stonePool[slot];
            body.team = thrower;
            body.stoneIndex = slot;
            ApplyEndStateToBodies(afterPhysics);
            Debug.Log($"[Curling] {thrower} shoots slot={slot} v=({shot.velocity.x:F2},{shot.velocity.y:F2}) ω={shot.angular_velocity:F2} from {spawn}");
            Debug.Log($"[Curling] Simulation launch slot={slot} pos=({thrownStone.position.x:F2},{thrownStone.position.y:F2}) speed={thrownStone.linear_velocity.Magnitude:F3} w={thrownStone.angular_velocity:F2}");
            CameraFollow.SetShotTarget(body.transform);

            _skipRequested = false;
            _physicsInProgress = true;
            int stillFrames = 0;
            int waitedFrames = 0;
            const int MinWaitFrames = 30;
            var simulator = new Curling.Physics.IceSimulator { Dt = Time.fixedDeltaTime };
            int maxWaitFrames = Mathf.Max(MinWaitFrames, Mathf.CeilToInt(shotTimeoutSeconds / Time.fixedDeltaTime));
            bool skipped = false;
            while ((stillFrames < CCore.StopFramesRequired || waitedFrames < MinWaitFrames) && waitedFrames < maxWaitFrames)
            {
                yield return new WaitForFixedUpdate();
                waitedFrames++;

                if (_skipRequested)
                {
                    simulator.SimulateToRest(afterPhysics.stones);
                    ApplyEndStateToBodies(afterPhysics);
                    _skipRequested = false;
                    skipped = true;
                    stillFrames = CCore.StopFramesRequired;
                    Debug.Log($"[Curling] Shot slot={slot} skipped at frame {waitedFrames} → fast-forwarded to rest.");
                    break;
                }

                simulator.Step(afterPhysics.stones);
                ApplyEndStateToBodies(afterPhysics, simulator.Dt);
                if (waitedFrames == 1)
                {
                    Debug.Log($"[Curling] First simulation step slot={slot} pos=({thrownStone.position.x:F2},{thrownStone.position.y:F2}) speed={thrownStone.linear_velocity.Magnitude:F3} w={thrownStone.angular_velocity:F2}");
                }

                bool anyMoving = AnyMovingStone(afterPhysics);
                stillFrames = anyMoving ? 0 : stillFrames + 1;
            }
            _physicsInProgress = false;

            bool settled = stillFrames >= CCore.StopFramesRequired;
            if (settled && postSettleHoldSeconds > 0f && !skipped)
            {
                yield return new WaitForSeconds(postSettleHoldSeconds);
            }
            else if (!settled)
            {
                Debug.LogWarning($"[Curling] Shot slot={slot} timed out after {waitedFrames * Time.fixedDeltaTime:F1}s before all stones settled.");
            }

            var finished = afterPhysics.stones[slot];
            Debug.Log($"[Curling] Shot slot={slot} {(settled ? "settled" : "ended")} after {waitedFrames * Time.fixedDeltaTime:F1}s at ({finished.position.x:F2},{finished.position.y:F2}) speed={finished.linear_velocity.Magnitude:F3} inPlay={finished.in_play}");

            simulator.RemoveBeforeHogLine(afterPhysics.stones);

            CameraFollow.ClearTarget();

            var result = _rules.ApplyShot(before, afterPhysics, thrower);

            _state.current_end = result.resultingEnd;
            ApplyEndStateToBodies(_state.current_end);

            var shotInfo = new ShotInfo
            {
                thrower = thrower,
                endComplete = result.endComplete,
                endScore = result.endScore,
                endScorer = result.endScorer,
            };
            // 毎ショット後の実況（非ブロッキング。盤面は呼び出し時点で文字列化される）。
            commentary?.Comment(_state, shotInfo);

            if (result.endComplete)
            {
                int t0 = result.endScorer == Team.Team0 ? result.endScore : 0;
                int t1 = result.endScorer == Team.Team1 ? result.endScore : 0;
                _state.score.RecordEnd(t0, t1);

                int endsPlayed = _state.score.EndsPlayed;
                if (endsPlayed >= _state.settings.standard_end_count && !_rules.ShouldGoToExtraEnd(_state.score, endsPlayed))
                {
                    _state.winner = _rules.DetermineWinner(_state.score, endsPlayed);
                    _state.phase = MatchPhase.Finished;
                    // 勝敗確定の実況で上書き（force=true で進行中の実況を中断）。
                    commentary?.Comment(_state, shotInfo, force: true);
                    yield break;
                }

                if (_rules.ShouldGoToExtraEnd(_state.score, endsPlayed) && !_state.in_extra_end)
                {
                    _state.GrantExtraEndTime();
                }

                Team nextHammer = _rules.NextEndHammer(_state.current_end, result.endScore, result.endScorer);
                _state.current_end = _state.NewEnd(_state.current_end.end_index + 1, nextHammer);
                ResetStonesForEnd();
                _state.phase = MatchPhase.InEnd;
            }
        }

        int NextEmptyStoneSlot(Team t)
        {
            int teamBase = t == Team.Team0 ? 0 : CCore.StonesPerTeamStandard;
            int thrown = _state.current_end.shot_num / 2;
            int idx = teamBase + thrown;
            return idx < stonePool.Length ? idx : -1;
        }

        void StageNextStone(Team t)
        {
            int slot = NextEmptyStoneSlot(t);
            if (slot < 0) return;

            var body = stonePool[slot];
            if (body == null) return;

            body.team = t;
            body.stoneIndex = slot;
            body.Stage(SpawnPosition());
            CameraFollow.SetTarget(body.transform, true);
        }

        Vector3 SpawnPosition()
        {
            return stoneSpawnPoint != null
                ? stoneSpawnPoint.position
                : new Vector3(0f, 0f, CCore.HogLineY - 12f);
        }

        ShotInput BuildCpuTimeoutFallbackShot()
        {
            const float speed = 2.71f;
            const float angleDeg = 69f;
            float angle = angleDeg * Mathf.Deg2Rad;
            var velocity = new Vec2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed);
            return new ShotInput(velocity, -1.57f);
        }

        static bool AnyMovingStone(EndState end)
        {
            foreach (var s in end.stones)
            {
                if (s.in_play && !s.IsStill) return true;
            }
            return false;
        }

        void ResetStonesForEnd()
        {
            foreach (var s in stonePool) s.Deactivate();
        }

        EndState CaptureCurrentEndState(EndState before)
        {
            var snap = before.DeepClone();
            foreach (var body in stonePool)
            {
                var s = body.ToStoneState();
                if (s.stone_index < snap.stones.Count)
                {
                    snap.stones[s.stone_index] = s;
                }
            }
            return snap;
        }

        void ApplyEndStateToBodies(EndState end, float simulatedStepSeconds = 0f)
        {
            for (int i = 0; i < stonePool.Length && i < end.stones.Count; i++)
            {
                if (simulatedStepSeconds > 0f)
                {
                    stonePool[i].ApplySimulatedState(end.stones[i], simulatedStepSeconds);
                }
                else
                {
                    stonePool[i].ApplyState(end.stones[i]);
                }
            }
        }

        public void Concede(Team t)
        {
            _state.conceded = true;
            _state.winner = t.Opponent();
            _state.phase = MatchPhase.Finished;
            commentary?.Comment(_state, new ShotInfo { thrower = t }, force: true);
        }
    }
}
