using System;
using Unity.Mathematics;

namespace UnityEngine.YukselSplines
{
    /// <summary>
    /// Control points for a cubic B-Spline section comprising 4 control points.
    /// The section may not pass through every control point (B-Splines are not interpolating).
    /// </summary>
    public struct BezierCurve : IEquatable<BezierCurve>
    {
        CircularInterpolator InterpolatorA;
        CircularInterpolator InterpolatorB;

        ConstantTwistInterpolator TwistInterpolatorA;
        ConstantTwistInterpolator TwistInterpolatorB;

        public BezierCurve(BezierKnot c0, BezierKnot c1, BezierKnot c2, BezierKnot c3)
        {
            InterpolatorA = default;
            InterpolatorB = default;

            TwistInterpolatorA = default;
            TwistInterpolatorB = default;

            InterpolatorA.Initialize(c0.Position, c1.Position, c2.Position);
            InterpolatorB.Initialize(c1.Position, c2.Position, c3.Position);

            TwistInterpolatorA.Initialize(c0.TwistAngle, c1.TwistAngle, c2.TwistAngle);
            TwistInterpolatorB.Initialize(c1.TwistAngle, c2.TwistAngle, c3.TwistAngle);
        }

        public BezierCurve(BezierCurve other)
        {
            InterpolatorA = other.InterpolatorA;
            InterpolatorB = other.InterpolatorB;

            TwistInterpolatorA = other.TwistInterpolatorA;
            TwistInterpolatorB = other.TwistInterpolatorB;
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
        /// Return an interpolated twist angle at ratio t.
        /// </summary>
        /// <param name="t">A value between 0 and 1 representing the ratio along the curve.</param>
        /// <returns>A twist angle around the curve tangent at t.</returns>
        public float EvaluateTwistAngle(float t)
        {
            var trigonometricParameteter = GetTrigonometricParameter(t);
            var localParameterA = TwistInterpolatorA.T + (1f - TwistInterpolatorA.T) * t;
            var localParameterB = TwistInterpolatorB.T * t;

            var interpolatedA = TwistInterpolatorA.Evaluate(localParameterA);
            var interpolatedB = TwistInterpolatorB.Evaluate(localParameterB);

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

        private static float TrigonometricBlend(float t, float f1, float f2)
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

            var term1 = 2f * (cosSquared * sinSquared) * (f2 - f1);
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
