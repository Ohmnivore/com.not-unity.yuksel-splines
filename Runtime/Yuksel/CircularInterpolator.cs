using System;
using Unity.Mathematics;

namespace UnityEngine.YukselSplines
{
    struct CircularInterpolator : IInterpolator, IEquatable<CircularInterpolator>
    {
        float3 DirA;
        float3 DirB;
        float3 DirC;

        float3 Center;
        float Radius;
        float AngleRange;
        quaternion CircleRotation;

        bool IsLinear;

        float t;

        public float T => t;

        public void Initialize(float3 a, float3 b, float3 c)
        {
            if (a.Equals(b))
            {
                DirA = a;
                DirC = c;
                t = 0f;
                IsLinear = true;
            }
            else if (b.Equals(c))
            {
                DirA = a;
                DirC = c;
                t = 1f;
                IsLinear = true;
            }
            else
            {
                IsLinear = false;

                Center = CircleBariCenter3D(a, b, c);
                Radius = math.length(a - Center);

                DirA = math.normalize(a - Center);
                DirB = math.normalize(b - Center);
                DirC = math.normalize(c - Center);

                var axis = math.cross(DirA, DirC);
                AngleRange = Vector3.SignedAngle(DirA, DirC, axis);

                var bAngle = Vector3.SignedAngle(DirA, DirB, axis);
                t = bAngle / AngleRange;

                CircleRotation = quaternion.LookRotation(DirA, axis);

                Debug.DrawLine(Center, Center + DirA * Radius);
                Debug.DrawLine(Center, Center + DirB * Radius);
                Debug.DrawLine(Center, Center + DirC * Radius);
                Debug.DrawLine(Center, Center + axis * Radius * 0.2f);
            }
        }

        public float3 Evaluate(float t)
        {
            if (IsLinear)
                return math.lerp(DirA, DirC, t);
            else
            {
                var angle = -math.radians(AngleRange * t - 90f);
                var on2DCircle = new float3(math.cos(angle), 0f, math.sin(angle)) * Radius;
                var on3DCircle = math.rotate(CircleRotation, on2DCircle);

                return on3DCircle + Center;
            }
        }

        public float3 EvaluateFirstDerivative(float t)
        {
            if (IsLinear)
                return DirC - DirA;
            else
            {
                var angle = -math.radians(AngleRange * t + 90f);
                var on2DCircle = new float3(-AngleRange * math.sin(angle), 0f, AngleRange * math.cos(angle)) * Radius;
                var on3DCircle = math.rotate(CircleRotation, on2DCircle);

                return on3DCircle + Center;
            }
        }

        public float3 EvaluateSecondDerivative(float t)
        {
            if (IsLinear)
                return 0f;
            else
            {
                var angle = -math.radians(AngleRange * t + 90f);
                var on2DCircle = new float3(-AngleRange * AngleRange * math.sin(angle), 0f, -AngleRange * AngleRange * math.cos(angle)) * Radius;
                var on3DCircle = math.rotate(CircleRotation, on2DCircle);

                return on3DCircle + Center;
            }
        }

        // https://stackoverflow.com/a/67062238
        static float3 CircleBariCenter3D(float3 p1, float3 p2, float3 p3)
        {
            Vector3 a = p3 - p2;
            Vector3 b = p1 - p3;
            Vector3 c = p2 - p1;

            float u = math.dot(a, a) * math.dot(c, b);
            float v = math.dot(b, b) * math.dot(c, a);
            float w = math.dot(c, c) * math.dot(b, a);

            return BarycentricToWorld3D(p1, p2, p3, u, v, w);
        }

        static float3 BarycentricToWorld3D(float3 p1, float3 p2, float3 p3, float u, float v, float w)
        {
            return (u * p1 + v * p2 + w * p3) / (u + v + w);
        }

        public bool Equals(CircularInterpolator other)
        {
            return
                DirA.Equals(other.DirA) &&
                DirB.Equals(other.DirB) &&
                DirC.Equals(other.DirC) &&
                Center.Equals(other.Center) &&
                Radius.Equals(other.Radius);
        }

        public override bool Equals(object obj)
        {
            return obj is CircularInterpolator other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = DirA.GetHashCode();
                hashCode = (hashCode * 397) ^ DirB.GetHashCode();
                hashCode = (hashCode * 397) ^ DirC.GetHashCode();
                hashCode = (hashCode * 397) ^ Center.GetHashCode();
                hashCode = (hashCode * 397) ^ Radius.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(CircularInterpolator left, CircularInterpolator right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CircularInterpolator left, CircularInterpolator right)
        {
            return !left.Equals(right);
        }
    }
}
