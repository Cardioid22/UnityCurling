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

        [Header("Animation Skip")]
        public KeyCode fastForwardKey = KeyCode.LeftShift;
        public KeyCode skipKey = KeyCode.S;
        public float fastForwardScale = 4f;

        MatchState _state;
        RuleEngine _rules;
        IShotDecider _cpu;
        CancellationTokenSource _cts;
        bool _physicsInProgress;
        bool _skipRequested;

        public MatchState State => _state;

        void Awake()
        {
            UnityEngine.Physics.gravity = new Vector3(0f, -CCore.Gravity, 0f);
        }

        void Update()
        {
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
            var box = new GUIStyle(GUI.skin.box) { fontSize = 12, alignment = TextAnchor.UpperLeft };
            string ffState = (Time.timeScale > 1.01f) ? $"<color=#7fff7f>{Time.timeScale:F1}x</color>" : "1.0x";
            string txt =
                $"[ Animation ]\n" +
                $"Shift hold : 早送り ({fastForwardScale:F1}x)  now: {ffState}\n" +
                $"S         : 物理スキップ (投擲中)";
            GUI.Box(new Rect(12, Screen.height - 80, 360, 68), txt, box);
        }

        public void StartNewMatch(MatchSettings settings)
        {
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
                    _cts = new CancellationTokenSource(Mathf.RoundToInt(_state.settings.thinking_time_sec * 1000f));
                    var task = _cpu.DecideAsync(_state, next, _cts.Token);
                    float t0 = Time.realtimeSinceStartup;
                    while (!task.IsCompleted) yield return null;
                    while (Time.realtimeSinceStartup - t0 < 1.5f) yield return null;
                    shot = task.Result;
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
            CameraFollow.SetTarget(body.transform);

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
            CameraFollow.SetTarget(body.transform);
        }

        Vector3 SpawnPosition()
        {
            return stoneSpawnPoint != null
                ? stoneSpawnPoint.position
                : new Vector3(0f, 0f, CCore.HogLineY - 12f);
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
        }
    }
}
