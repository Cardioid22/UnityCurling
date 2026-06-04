using System;

namespace Curling.Core
{
    [Serializable]
    public struct Vec2 : IEquatable<Vec2>
    {
        public float x;
        public float y;

        public Vec2(float x, float y) { this.x = x; this.y = y; }

        public static Vec2 Zero => new Vec2(0f, 0f);

        public float SqrMagnitude => x * x + y * y;
        public float Magnitude => (float)Math.Sqrt(SqrMagnitude);

        public Vec2 Normalized
        {
            get
            {
                float m = Magnitude;
                return m > 1e-12f ? new Vec2(x / m, y / m) : Zero;
            }
        }

        public Vec2 Perpendicular() => new Vec2(-y, x);

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.x + b.x, a.y + b.y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.x - b.x, a.y - b.y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.x * s, a.y * s);
        public static Vec2 operator *(float s, Vec2 a) => new Vec2(a.x * s, a.y * s);
        public static Vec2 operator /(Vec2 a, float s) => new Vec2(a.x / s, a.y / s);
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.x, -a.y);

        public static float Dot(Vec2 a, Vec2 b) => a.x * b.x + a.y * b.y;
        public static float Distance(Vec2 a, Vec2 b) => (a - b).Magnitude;
        public static float SqrDistance(Vec2 a, Vec2 b) => (a - b).SqrMagnitude;

        public bool Equals(Vec2 other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is Vec2 v && Equals(v);
        public override int GetHashCode() => (x, y).GetHashCode();
        public override string ToString() => $"({x:F3}, {y:F3})";
    }
}
