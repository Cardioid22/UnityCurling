using System;
using System.Collections.Generic;
using Curling.Core;

namespace Curling.Physics
{
    public class IceSimulator
    {
        public float Mu = Constants.IceFrictionCoefficient;
        public float KCurl = Constants.CurlCoefficient;
        public float CurlExponent = 1f;
        public float KOmega = Constants.AngularDecel;
        public float Gravity = Constants.Gravity;
        public float Restitution = 1.0f;
        public float Dt = 0.001f;
        public int MaxSteps = 60000;

        public bool SimulateToRest(List<StoneState> stones)
        {
            for (int step = 0; step < MaxSteps; step++)
            {
                if (!Step(stones)) return true;
            }
            return false;
        }

        public bool Step(List<StoneState> stones)
        {
            for (int i = 0; i < stones.Count; i++)
            {
                var s = stones[i];
                if (!s.in_play) continue;

                Vec2 v = s.linear_velocity;
                if (v.Magnitude > Constants.StopLinearEps || Math.Abs(s.angular_velocity) > Constants.StopAngularEps)
                {
                    Vec2 fr = CurlForce.FrictionAccel(v, Mu, Gravity);
                    Vec2 fc = CurlForce.Compute(v, s.angular_velocity, KCurl, CurlExponent);
                    v += (fr + fc) * Dt;
                    if (v.Magnitude < Constants.StopLinearEps) v = Vec2.Zero;
                    s.linear_velocity = v;
                    s.position += v * Dt;
                    s.angular_velocity += CurlForce.AngularDecel(s.angular_velocity, KOmega) * Dt;
                    if (Math.Abs(s.angular_velocity) < Constants.StopAngularEps) s.angular_velocity = 0f;
                }
                else
                {
                    s.linear_velocity = Vec2.Zero;
                    s.angular_velocity = 0f;
                }
            }

            ResolveCollisions(stones);
            ResolveOutOfPlay(stones);
            return HasMovingStone(stones);
        }

        static bool HasMovingStone(List<StoneState> stones)
        {
            foreach (var s in stones)
            {
                if (s.in_play && !s.IsStill) return true;
            }
            return false;
        }

        void ResolveCollisions(List<StoneState> stones)
        {
            float r2 = Constants.StoneRadius * 2f;
            float r2sq = r2 * r2;
            for (int i = 0; i < stones.Count; i++)
            {
                var a = stones[i];
                if (!a.in_play) continue;
                for (int j = i + 1; j < stones.Count; j++)
                {
                    var b = stones[j];
                    if (!b.in_play) continue;
                    Vec2 d = b.position - a.position;
                    float dsq = d.SqrMagnitude;
                    if (dsq < r2sq && dsq > 1e-12f)
                    {
                        float dist = (float)Math.Sqrt(dsq);
                        Vec2 n = d / dist;
                        float overlap = r2 - dist;
                        a.position -= n * (overlap * 0.5f);
                        b.position += n * (overlap * 0.5f);

                        Vec2 rv = b.linear_velocity - a.linear_velocity;
                        float vn = Vec2.Dot(rv, n);
                        if (vn < 0f)
                        {
                            float j_imp = -(1f + Restitution) * vn * 0.5f;
                            Vec2 imp = n * j_imp;
                            a.linear_velocity -= imp;
                            b.linear_velocity += imp;
                        }
                    }
                }
            }
        }

        void ResolveOutOfPlay(List<StoneState> stones)
        {
            float halfW = Constants.SheetHalfWidth;
            float backY = Constants.BackLineY;
            foreach (var s in stones)
            {
                if (!s.in_play) continue;
                if (s.position.y > backY + Constants.StoneRadius) s.in_play = false;
                if (Math.Abs(s.position.x) > halfW + Constants.StoneRadius) s.in_play = false;
                if (!s.in_play)
                {
                    s.linear_velocity = Vec2.Zero;
                    s.angular_velocity = 0f;
                }
            }
        }

        public void RemoveBeforeHogLine(List<StoneState> stones)
        {
            foreach (var s in stones)
            {
                if (!s.in_play) continue;
                if (s.position.y < Constants.HogLineY)
                {
                    s.in_play = false;
                    s.linear_velocity = Vec2.Zero;
                    s.angular_velocity = 0f;
                }
            }
        }
    }
}
