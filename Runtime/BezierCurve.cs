using System;
using Unity.Mathematics;

namespace UnityEngine.YukselSplines
{
    public interface IInterpolator
    {
        float T { get; }

        void Initialize(float3 P0, float3 P1, float3 P2);

        float3 Evaluate(float t);

        float3 EvaluateFirstDerivative(float t);

        float3 EvaluateSecondDerivative(float t);
    }

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
            float3 controlPoint = GetBezierControlPoint(a, b, c, out t);
            ControlMatrix = GetBezierControlMatrix(a, controlPoint, c);
        }

        public float3 Evaluate(float t)
        {
            var parameterVector = new float3(1f, t, t * t);

            return math.mul(parameterVector, ControlMatrix);
        }

        public float3 EvaluateFirstDerivative(float t)
        {
            var parameterVector = new float3(0f, 1f, t);

            return math.mul(parameterVector, ControlMatrix);
        }

        public float3 EvaluateSecondDerivative(float t)
        {
            var parameterVector = new float3(0f, 0f, 1f);

            return math.mul(parameterVector, ControlMatrix);
        }

        static float3x3 GetBezierControlMatrix(float3 a, float3 b, float3 c)
        {
            var controlMatrix = new float3x3(a, b, c);
            controlMatrix = math.transpose(controlMatrix);

            return math.mul(QuadraticBezierMatrixForm, controlMatrix);
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

    /// <summary>
    /// Control points for a cubic B-Spline section comprising 4 control points.
    /// The section may not pass through every control point (B-Splines are not interpolating).
    /// </summary>
    public struct BezierCurve : IEquatable<BezierCurve>
    {
        QuadraticBezierInterpolator InterpolatorA;
        QuadraticBezierInterpolator InterpolatorB;

        /// <summary>
        /// Construct a cubic B-Spline section from a series of control points.
        /// </summary>
        /// <param name="p0">The first control point.</param>
        /// <param name="p1">The second control point.</param>
        /// <param name="p2">The third control point.</param>
        /// <param name="p3">The fourth control point.</param>
        public BezierCurve(float3 p0, float3 p1, float3 p2, float3 p3)
        {
            InterpolatorA = default;
            InterpolatorB = default;

            InterpolatorA.Initialize(p0, p1, p2);
            InterpolatorB.Initialize(p1, p2, p3);
        }

        public BezierCurve(BezierKnot c0, BezierKnot c1, BezierKnot c2, BezierKnot c3) :
            this(c0.Position, c1.Position, c2.Position, c3.Position)
        {

        }

        public BezierCurve(float3 p0, float3 p1, float3 p2, float3 p3, float4x4 matrix) :
            this(math.transform(matrix, p0), math.transform(matrix, p1), math.transform(matrix, p2), math.transform(matrix, p3))
        {

        }

        public BezierCurve(BezierKnot c0, BezierKnot c1, BezierKnot c2, BezierKnot c3, float4x4 matrix) :
            this(c0.Transform(matrix), c1.Transform(matrix), c2.Transform(matrix), c3.Transform(matrix))
        {

        }

        /// <summary>
        /// Return an interpolated position at ratio t.
        /// </summary>
        /// <param name="t">A value between 0 and 1 representing the ratio along the curve.</param>
        /// <returns>A position on the curve.</returns>
        public float3 EvaluatePosition(float t)
        {
            var trigonometricParameteter = GetTrigonometricParameter(t);
            var localParameterA = GetLocalParameterA(t);
            var localParameterB = GetLocalParameterB(t);

            var interpolatedA = InterpolatorA.Evaluate(localParameterA);
            var interpolatedB = InterpolatorB.Evaluate(localParameterB);

            var blended = TrigonometricBlend(trigonometricParameteter, interpolatedA, interpolatedB);
            return blended;
        }

        /// <summary>
        /// Return an interpolated tangent at ratio t.
        /// </summary>
        /// <param name="t">A value between 0 and 1 representing the ratio along the curve.</param>
        /// <returns>A tangent on the curve.</returns>
        public float3 EvaluateTangent(float t)
        {
            var trigonometricParameteter = GetTrigonometricParameter(t);
            var localParameterA = GetLocalParameterA(t);
            var localParameterB = GetLocalParameterB(t);

            var interpolatedA = InterpolatorA.Evaluate(localParameterA);
            var interpolatedB = InterpolatorB.Evaluate(localParameterB);
            var interpolatedDerivativeA = InterpolatorA.EvaluateFirstDerivative(localParameterA);
            var interpolatedDerivativeB = InterpolatorB.EvaluateFirstDerivative(localParameterB);

            var blended = TrigonometricBlendFirstDerivative(trigonometricParameteter, interpolatedA, interpolatedB, interpolatedDerivativeA, interpolatedDerivativeB);
            return blended;
        }

        /// <summary>
        /// Return an interpolated acceleration at ratio t.
        /// </summary>
        /// <param name="t">A value between 0 and 1 representing the ratio along the curve.</param>
        /// <returns>An acceleration vector on the curve.</returns>
        public float3 EvaluateAcceleration(float t)
        {
            var trigonometricParameteter = GetTrigonometricParameter(t);
            var localParameterA = GetLocalParameterA(t);
            var localParameterB = GetLocalParameterB(t);

            var interpolatedA = InterpolatorA.Evaluate(localParameterA);
            var interpolatedB = InterpolatorB.Evaluate(localParameterB);
            var interpolatedFirstDerivativeA = InterpolatorA.EvaluateFirstDerivative(localParameterA);
            var interpolatedFirstDerivativeB = InterpolatorB.EvaluateFirstDerivative(localParameterB);
            var interpolatedSecondDerivativeA = InterpolatorA.EvaluateSecondDerivative(localParameterA);
            var interpolatedSecondDerivativeB = InterpolatorB.EvaluateSecondDerivative(localParameterB);

            var blended = TrigonometricBlendSecondDerivative(trigonometricParameteter, interpolatedA, interpolatedB, interpolatedFirstDerivativeA, interpolatedFirstDerivativeB, interpolatedSecondDerivativeA, interpolatedSecondDerivativeB);
            return blended;
        }

        float GetLocalParameterA(float t)
        {
            return InterpolatorA.T + (1f - InterpolatorA.T) * t;
        }

        float GetLocalParameterB(float t)
        {
            return InterpolatorB.T * t;
        }

        float GetTrigonometricParameter(float t)
        {
            return math.PI / 2.0f * t;
        }

        private static float3 TrigonometricBlend(float t, float3 f1, float3 f2)
        {
            var cos = math.cos(t);
            var cosSquared = cos * cos;
            var sin = math.sin(t);
            var sinSquared = sin * sin;

            return cosSquared * f1 + sinSquared * f2;
        }

        private static float3 TrigonometricBlendFirstDerivative(float t, float3 f1, float3 f2, float3 f1Derivative, float3 f2Derivative)
        {
            var cos = math.cos(t);
            var cosSquared = cos * cos;
            var sin = math.sin(t);
            var sinSquared = sin * sin;

            return 2f * cos * sin * (f2 - f1) + cosSquared * f1Derivative + sinSquared * f2Derivative;
        }

        private static float3 TrigonometricBlendSecondDerivative(float t, float3 f1, float3 f2, float3 f1FirstDerivative, float3 f2FirstDerivative, float3 f1SecondDerivative, float3 f2SecondDerivative)
        {
            var cos = math.cos(t);
            var cosSquared = cos * cos;
            var sin = math.sin(t);
            var sinSquared = sin * sin;

            var term1 = 2 * (cosSquared * sinSquared) * (f2 - f1);
            var term2 = 4f * cos * sin * (f2FirstDerivative - f1FirstDerivative);
            var term3 = cosSquared * f1SecondDerivative + sinSquared * f2SecondDerivative;

            return term1 + term2 + term3;
        }

        /// <summary>
        /// Calculate the approximate length of a <see cref="BezierCurve"/>. This is less accurate than
        /// <seealso cref="CurveUtility.CalculateLength"/>, but can be significantly faster. Use this when accuracy is
        /// not paramount and the curve control points are changing frequently.
        /// </summary>
        /// <returns>An estimate of the length of a curve.</returns>
        public float ApproximateLength()
        {
            var P0 = InterpolatorA.Evaluate(0f);
            var P1 = InterpolatorB.Evaluate(0f);
            var P2 = InterpolatorA.Evaluate(1f);
            var P3 = InterpolatorB.Evaluate(1f);

            float chord = math.length(P3 - P0);
            float net = math.length(P0 - P1) + math.length(P2 - P1) + math.length(P3 - P2);
            return (net + chord) / 2;
        }

        /// <summary>
        /// Compare two curves for equality.
        /// </summary>
        /// <param name="other">The curve to compare against.</param>
        /// <returns>Returns true when the control points of each curve are identical.</returns>
        public bool Equals(BezierCurve other)
        {
            return InterpolatorA.Equals(other.InterpolatorA) && InterpolatorB.Equals(other.InterpolatorB);
        }

        /// <summary>
        /// Compare against an object for equality.
        /// </summary>
        /// <param name="obj">The object to compare against.</param>
        /// <returns>
        /// Returns true when <paramref name="obj"/> is a <see cref="BSplineCurve"/> and the control points of each
        /// curve are identical.
        /// </returns>
        public override bool Equals(object obj)
        {
            return obj is BezierCurve other && Equals(other);
        }

        /// <summary>
        /// Calculate a hash code for this curve.
        /// </summary>
        /// <returns>
        /// A hash code for the curve.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = InterpolatorA.GetHashCode();
                hashCode = (hashCode * 397) ^ InterpolatorB.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// Compare two curves for equality.
        /// </summary>
        /// <param name="left">The first curve.</param>
        /// <param name="right">The second curve.</param>
        /// <returns>Returns true when the control points of each curve are identical.</returns>
        public static bool operator ==(BezierCurve left, BezierCurve right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compare two curves for inequality.
        /// </summary>
        /// <param name="left">The first curve.</param>
        /// <param name="right">The second curve.</param>
        /// <returns>Returns false when the control points of each curve are identical.</returns>
        public static bool operator !=(BezierCurve left, BezierCurve right)
        {
            return !left.Equals(right);
        }
    }
}
