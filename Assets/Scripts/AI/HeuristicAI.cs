using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Curling.Core;
using Curling.Physics;
using Curling.Rules;

namespace Curling.AI
{
    public class HeuristicAI : IShotDecider
    {
        public CpuDifficulty Difficulty;
        public int Seed;

        const float SearchDt = 0.02f;
        const int SearchMaxSteps = 6000;
        const float MinSearchSpeed = 1.5f;
        const float AimMinDeg = -45f;
        const float AimMaxDeg = 45f;
        const float RotationMagnitude = 1.57f;
        const int TopCandidateCount = 8;
        const int MaxSearchTargets = 12;

        readonly Random _rng;

        public HeuristicAI(CpuDifficulty diff, int seed = 0)
        {
            Difficulty = diff;
            Seed = seed == 0 ? Environment.TickCount : seed;
            _rng = new Random(Seed);
        }

        public Task<ShotInput> DecideAsync(MatchState state, Team self, CancellationToken ct)
        {
            return Task.Run(() => Decide(state, self, ct), ct);
        }

        ShotInput Decide(MatchState state, Team self, CancellationToken ct)
        {
            var end = state.current_end;
            switch (Difficulty)
            {
                case CpuDifficulty.Easy: return DecideEasy(end, self);
                case CpuDifficulty.Normal: return DecideNormal(end, self);
                case CpuDifficulty.Hard: return DecideHard(state, self, ct);
                default: return DecideNormal(end, self);
            }
        }

        ShotInput DecideEasy(EndState end, Team self)
        {
            var plans = new List<ShotPlan>();
            foreach (var p in ShotCatalog.Generate(end, self))
                if (p.kind == ShotKind.Draw || p.kind == ShotKind.Guard) plans.Add(p);
            var plan = plans[_rng.Next(plans.Count)];
            return ApplyNoise(ShotCatalog.ToShotInput(plan, ShotCatalog.DefaultStartPos()), 0.05f, 0.02f);
        }

        ShotInput DecideNormal(EndState end, Team self)
        {
            Team opp = self.Opponent();
            int selfInHouse = 0;
            StoneState bestOpp = null;
            float bestOppDist = float.MaxValue;

            foreach (var s in end.LiveStones())
            {
                if (!s.IsInHouse()) continue;
                if (s.team == self) selfInHouse++;
                else
                {
                    float d = s.DistanceToTee();
                    if (d < bestOppDist) { bestOppDist = d; bestOpp = s; }
                }
            }

            bool lastShot = end.shot_num >= Constants.ShotsPerEndStandard - 2;
            bool hammerSelf = end.hammer == self;

            ShotPlan plan;
            if (lastShot && hammerSelf)
                plan = ShotCatalog.Draw(new Vec2(0f, Constants.HouseCenterY), Rotation.CCW);
            else if (selfInHouse == 0)
                plan = ShotCatalog.Draw(new Vec2(0f, Constants.HouseCenterY), Rotation.CCW);
            else if (bestOpp != null && bestOpp.DistanceToTee() < 1.0f)
                plan = ShotCatalog.Takeout(bestOpp.position, Rotation.CCW);
            else
                plan = ShotCatalog.Guard(new Vec2(0f, Constants.HogLineY + 1.5f), Rotation.CCW);

            return ApplyNoise(ShotCatalog.ToShotInput(plan, ShotCatalog.DefaultStartPos()), 0.02f, 0.008f);
        }

        ShotInput DecideHard(MatchState state, Team self, CancellationToken ct)
        {
            var end = state.current_end;
            int slot = NextEmptyStoneSlot(end, self);
            if (slot < 0)
                return ShotCatalog.ToShotInput(
                    ShotCatalog.Draw(new Vec2(0f, Constants.HouseCenterY), Rotation.CCW),
                    ShotCatalog.DefaultStartPos());

            var rules = new RuleEngine(state.settings);
            var top = new List<ScoredShot>(TopCandidateCount);
            ScoredShot best = ScoredShot.Invalid;

            SearchGrid(end, self, rules, ct, AimMinDeg, AimMaxDeg, 7.5f, MinSearchSpeed, Constants.MaxSpeed, 0.25f, top, ref best);
            SearchTargetedShots(end, self, rules, ct, top, ref best);
            RefineBestShots(end, self, rules, ct, top, ref best);

            if (!best.valid)
                return BuildShot(2.7f, 0f, -RotationMagnitude);

            return best.shot;
        }

        int NextEmptyStoneSlot(EndState end, Team self)
        {
            for (int i = 0; i < end.stones.Count; i++)
            {
                var s = end.stones[i];
                if (s.team == self && !s.in_play) return i;
            }
            return -1;
        }

        void SearchGrid(
            EndState end,
            Team self,
            RuleEngine rules,
            CancellationToken ct,
            float angleMin,
            float angleMax,
            float angleStep,
            float speedMin,
            float speedMax,
            float speedStep,
            List<ScoredShot> top,
            ref ScoredShot best)
        {
            for (float aim = angleMin; aim <= angleMax + 0.001f; aim += angleStep)
            {
                for (float speed = speedMin; speed <= speedMax + 0.001f; speed += speedStep)
                {
                    if (ct.IsCancellationRequested) return;
                    EvaluateAndKeep(end, self, rules, BuildShot(speed, aim, -RotationMagnitude), aim, top, ref best);
                    EvaluateAndKeep(end, self, rules, BuildShot(speed, aim, RotationMagnitude), aim, top, ref best);
                }
            }
        }

        void SearchTargetedShots(
            EndState end,
            Team self,
            RuleEngine rules,
            CancellationToken ct,
            List<ScoredShot> top,
            ref ScoredShot best)
        {
            var targets = BuildSearchTargets(end, self);
            foreach (var target in targets)
            {
                if (ct.IsCancellationRequested) return;

                float baseAim = AimOffsetForTarget(target);
                float min = Clamp(baseAim - 7.5f, AimMinDeg, AimMaxDeg);
                float max = Clamp(baseAim + 7.5f, AimMinDeg, AimMaxDeg);
                SearchGrid(end, self, rules, ct, min, max, 1.5f, MinSearchSpeed, Constants.MaxSpeed, 0.25f, top, ref best);
            }
        }

        List<Vec2> BuildSearchTargets(EndState end, Team self)
        {
            var targets = new List<Vec2>
            {
                new Vec2(0f, Constants.HouseCenterY),
                new Vec2(-0.45f, Constants.HouseCenterY),
                new Vec2(0.45f, Constants.HouseCenterY),
                new Vec2(0f, Constants.HogLineY + 1.2f),
                new Vec2(-0.55f, Constants.HogLineY + 1.6f),
                new Vec2(0.55f, Constants.HogLineY + 1.6f)
            };

            foreach (var s in end.LiveStones())
            {
                if (targets.Count >= MaxSearchTargets) break;
                if (s.team == self) continue;
                if (s.IsInHouse() || IsGuardZoneStone(s))
                    AddUniqueTarget(targets, s.position);
            }

            return targets;
        }

        static void AddUniqueTarget(List<Vec2> targets, Vec2 target)
        {
            foreach (var existing in targets)
            {
                if (Vec2.SqrDistance(existing, target) < 0.05f * 0.05f) return;
            }
            targets.Add(target);
        }

        void RefineBestShots(
            EndState end,
            Team self,
            RuleEngine rules,
            CancellationToken ct,
            List<ScoredShot> top,
            ref ScoredShot best)
        {
            var seeds = new List<ScoredShot>(top);
            foreach (var seed in seeds)
            {
                for (float angleDelta = -2f; angleDelta <= 2f + 0.001f; angleDelta += 0.5f)
                {
                    for (float speedDelta = -0.12f; speedDelta <= 0.12f + 0.001f; speedDelta += 0.04f)
                    {
                        if (ct.IsCancellationRequested) return;
                        float speed = Clamp(seed.shot.Speed + speedDelta, MinSearchSpeed, Constants.MaxSpeed);
                        float aim = Clamp(seed.aimOffsetDeg + angleDelta, AimMinDeg, AimMaxDeg);
                        EvaluateAndKeep(end, self, rules, BuildShot(speed, aim, seed.shot.angular_velocity), aim, top, ref best);
                    }
                }
            }
        }

        void EvaluateAndKeep(
            EndState before,
            Team self,
            RuleEngine rules,
            ShotInput shot,
            float aimOffsetDeg,
            List<ScoredShot> top,
            ref ScoredShot best)
        {
            var candidate = new ScoredShot
            {
                shot = shot,
                aimOffsetDeg = aimOffsetDeg,
                score = EvaluateShot(before, self, rules, shot),
                valid = true
            };

            if (!best.valid || candidate.score > best.score)
                best = candidate;

            InsertTopCandidate(top, candidate);
        }

        float EvaluateShot(EndState before, Team self, RuleEngine rules, ShotInput shot)
        {
            var afterPhysics = before.DeepClone();
            int idx = NextEmptyStoneSlot(afterPhysics, self);
            if (idx < 0) return float.MinValue;

            var stone = afterPhysics.stones[idx];
            stone.team = self;
            stone.stone_index = idx;
            stone.position = ShotCatalog.DefaultStartPos();
            stone.linear_velocity = shot.velocity;
            stone.angular_velocity = shot.angular_velocity;
            stone.in_play = true;

            var sim = new IceSimulator { Dt = SearchDt, MaxSteps = SearchMaxSteps };
            sim.SimulateToRest(afterPhysics.stones);
            sim.RemoveBeforeHogLine(afterPhysics.stones);

            var result = rules.ApplyShot(before, afterPhysics, self);
            float score = HeuristicEvaluator.Evaluate(result.resultingEnd, self);
            if (result.fgzViolation) score -= 12f;
            score -= shot.Speed * 0.003f;
            return score;
        }

        static void InsertTopCandidate(List<ScoredShot> top, ScoredShot candidate)
        {
            int insertAt = top.Count;
            for (int i = 0; i < top.Count; i++)
            {
                if (candidate.score > top[i].score)
                {
                    insertAt = i;
                    break;
                }
            }

            if (insertAt >= TopCandidateCount) return;
            top.Insert(insertAt, candidate);
            if (top.Count > TopCandidateCount)
                top.RemoveAt(top.Count - 1);
        }

        static ShotInput BuildShot(float speed, float aimOffsetDeg, float angularVelocity)
        {
            float angle = (float)(Math.PI * 0.5 + aimOffsetDeg * Math.PI / 180.0);
            var v = new Vec2((float)Math.Cos(angle), (float)Math.Sin(angle)) * speed;
            return new ShotInput(v, angularVelocity);
        }

        static float AimOffsetForTarget(Vec2 target)
        {
            Vec2 start = ShotCatalog.DefaultStartPos();
            Vec2 delta = target - start;
            float angle = (float)Math.Atan2(delta.y, delta.x);
            float offset = (float)((angle - Math.PI * 0.5) * 180.0 / Math.PI);
            return Clamp(offset, AimMinDeg, AimMaxDeg);
        }

        static bool IsGuardZoneStone(StoneState s)
        {
            if (!s.in_play) return false;
            if (s.position.y < Constants.HogLineY) return false;
            if (s.position.y > Constants.TeeLineY) return false;
            return !s.IsInHouse();
        }

        static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        ShotInput ApplyNoise(ShotInput baseShot, float stddevSpeed, float stddevAngle)
        {
            float speed = baseShot.Speed * (1f + (float)Gaussian() * stddevSpeed);
            float angle = baseShot.ShotAngle + (float)Gaussian() * stddevAngle;
            var v = new Vec2((float)Math.Cos(angle), (float)Math.Sin(angle)) * speed;
            return new ShotInput(v, baseShot.angular_velocity);
        }

        double Gaussian()
        {
            double u1 = 1.0 - _rng.NextDouble();
            double u2 = 1.0 - _rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        struct ScoredShot
        {
            public ShotInput shot;
            public float score;
            public float aimOffsetDeg;
            public bool valid;

            public static ScoredShot Invalid => new ScoredShot { valid = false, score = float.MinValue };
        }
    }
}
