using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Curling.Core;
using Curling.Physics;

namespace Curling.AI
{
    public class HeuristicAI : IShotDecider
    {
        public CpuDifficulty Difficulty;
        public int Seed;

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
            ShotPlan bestPlan = null;
            float bestScore = float.MinValue;

            foreach (var plan in ShotCatalog.Generate(end, self))
            {
                if (ct.IsCancellationRequested) break;
                var sim = new IceSimulator();
                var trialEnd = end.DeepClone();
                int idx = NextEmptyStoneSlot(trialEnd, self);
                if (idx < 0) continue;
                var stone = trialEnd.stones[idx];
                stone.team = self;
                stone.position = ShotCatalog.DefaultStartPos();
                stone.linear_velocity = (plan.target - stone.position).Normalized * plan.speed;
                stone.angular_velocity = plan.angular_velocity;
                stone.in_play = true;

                sim.SimulateToRest(trialEnd.stones);
                sim.RemoveBeforeHogLine(trialEnd.stones);

                float score = HeuristicEvaluator.Evaluate(trialEnd, self);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestPlan = plan;
                }
            }

            if (bestPlan == null)
                bestPlan = ShotCatalog.Draw(new Vec2(0f, Constants.HouseCenterY), Rotation.CCW);

            return ApplyNoise(ShotCatalog.ToShotInput(bestPlan, ShotCatalog.DefaultStartPos()), 0.01f, 0.004f);
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
    }
}
