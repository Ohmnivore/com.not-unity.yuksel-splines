using System;
using Unity.Mathematics;

namespace UnityEngine.YukselSplines
{
    struct QuadraticBezierInterpolator : IInterpolator, IEquatable<QuadraticBezierInterpolator>
    {
        static readonly float3x3 QuadraticBezierMatrixForm = math.transpose(new float3x3(
            new float3(1f, 0f, 0f),
            new float3(-2f, 2f, 0f),
            new float3(1f, -2f, 1f)));

        float3x3 ControlMatrix;

        float t;

        public float T => t;

        public void Initialize(float3 a, float3 b, float3 c)
        {
            var controlPoint = GetBezierControlPoint(a, b, c, out t);
            ControlMatrix = GetBezierControlMatrix(a, controlPoint, c);
        }

        public float3 Evaluate(float t)
        {
            var parameterVector = new float3(1f, t, t * t);

            return math.mul(parameterVector, ControlMatrix);
        }

        public float3 EvaluateFirstDerivative(float t)
        {
            var parameterVector = new float3(0f, 1f, 2f * t);

            return math.mul(parameterVector, ControlMatrix);
        }

        public float3 EvaluateSecondDerivative(float t)
        {
            var parameterVector = new float3(0f, 0f, 2f);

            return math.mul(parameterVector, ControlMatrix);
        }

        static float3x3 GetBezierControlMatrix(float3 a, float3 b, float3 c)
        {
            var controlMatrix = new float3x3(a, b, c);
            var transposed = math.transpose(controlMatrix);

            return math.mul(QuadraticBezierMatrixForm, transposed);
        }

        /// <summary>
        /// Returns the control point for a quadratic Bezier curve that passes through the 3 specified points.
        /// This method places the local maximum of curvature at <paramref name="b"/>.
        /// </summary>
        /// <param name="a">The first point that the curve should pass through.</param>
        /// <param name="b">The second point that the curve should pass through.</param>
        /// <param name="c">The third point that the curve should pass through.</param>
        /// <param name="t">Stores the parameter where the local maximum occurs.</param>
        /// <returns>The control point of the curve.</returns>
        static float3 GetBezierControlPoint(float3 a, float3 b, float3 c, out float t)
        {
            var aToC = c - a;
            var aToCLength = math.length(aToC);

            var bToA = a - b;
            var bToALength = math.length(bToA);

            if (aToCLength < 0.001f || bToALength < 0.001f) // Overlapping points
                t = 0.5f;
            else if (math.abs(math.dot(aToC, -bToA) / (aToCLength * bToALength)) <= 0.001f) // Linear curve
                t = 0.5f;
            else
            {
                var v0 = a - b;
                var v2 = c - b;
                var vDot = math.dot(v0, v2);
                t = NewtonCubicRoot(-math.lengthsq(v0), -vDot / 3, vDot / 3, math.lengthsq(v2));
            }

            var oneMinusTi = 1f - t;

            var num = b - oneMinusTi * oneMinusTi * a - t * t * c;
            var denom = 2f * oneMinusTi * t;
            var controlPoint = num / denom;

            return controlPoint;
        }

        static float NewtonCubicRoot(float d, float c, float b, float a)
        {
            var value = (d + 3f * c + 3f * b + a) / 8f;
            if (value >= 0.000001f)
                return NewtonCubicRoot(d, (d + c) / 2f, (d + 2f * c + b) / 4f, value) / 2f;
            if (value <= -0.000001f)
                return 0.5f + NewtonCubicRoot(value, (c + 2f * b + a) / 4f, (b + a) / 2f, a) / 2f;
            return 0.5f;
        }

        public bool Equals(QuadraticBezierInterpolator other)
        {
            return ControlMatrix.Equals(other.ControlMatrix) && t.Equals(other.t);
        }

        public override bool Equals(object obj)
        {
            return obj is QuadraticBezierInterpolator other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = ControlMatrix.GetHashCode();
                hashCode = (hashCode * 397) ^ t.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(QuadraticBezierInterpolator left, QuadraticBezierInterpolator right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(QuadraticBezierInterpolator left, QuadraticBezierInterpolator right)
        {
            return !left.Equals(right);
        }
    }
}
