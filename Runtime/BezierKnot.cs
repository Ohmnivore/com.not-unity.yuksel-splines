using System;
using Unity.Mathematics;

namespace UnityEngine.YukselSplines
{
    /// <summary>
    /// This struct contains position and tangent data for a knot. The position is a scalar point and the tangents are vectors.
    /// The <see cref="Spline"/> class stores a collection of BezierKnot that form a series of connected
    /// <see cref="BezierCurve"/>. Each knot contains a Position, Tangent In, and Tangent Out. When a spline is not
    /// closed, the first and last knots will contain an extraneous tangent (in and out, respectively).
    /// </summary>
    [Serializable]
    public struct BezierKnot : IEquatable<BezierKnot>
    {
        /// <summary>
        /// The position of the knot. On a cubic Bezier curve, this is equivalent to <see cref="BezierCurve.P0"/> or
        /// <see cref="BezierCurve.P3"/>, depending on whether this knot is forming the first or second control point
        /// of the curve.
        /// </summary>
        public float3 Position;

        /// <summary>
        /// Rotation of the knot around its tangent in degrees.
        /// </summary>
        public float TwistAngle;

        /// <summary>
        /// Create a new BezierKnot struct.
        /// </summary>
        /// <param name="position">The position of the knot relative to the spline.</param>
        public BezierKnot(float3 position)
        {
            Position = position;
            TwistAngle = 0f;
        }

        /// <summary>
        /// Create a new BezierKnot struct.
        /// </summary>
        /// <param name="position">The position of the knot relative to the spline.</param>
        /// <param name="tangentIn">The leading tangent to this knot.</param>
        /// <param name="tangentOut">The following tangent to this knot.</param>
        /// <param name="rotation">The rotation of the knot relative to the spline.</param>
        public BezierKnot(float3 position, float twistAngle)
        {
            Position = position;
            TwistAngle = twistAngle;
        }

        /// <summary>
        /// Multiply the position and tangents by a matrix.
        /// </summary>
        /// <param name="matrix">The matrix to multiply.</param>
        /// <returns>A new BezierKnot multiplied by matrix.</returns>
        public BezierKnot Transform(float4x4 matrix)
        {
            return new BezierKnot(
                math.transform(matrix, Position),
                TwistAngle);
        }

        /// <summary>
        /// Knot position addition. This operation only applies to the position, tangents and rotation are unmodified.
        /// </summary>
        /// <param name="knot">The target knot.</param>
        /// <param name="rhs">The value to add.</param>
        /// <returns>A new BezierKnot where position is the sum of knot.position and rhs.</returns>
        public static BezierKnot operator +(BezierKnot knot, float3 rhs)
        {
            return new BezierKnot(knot.Position + rhs, knot.TwistAngle);
        }

        /// <summary>
        /// Knot position subtraction. This operation only applies to the position, tangents and rotation are unmodified.
        /// </summary>
        /// <param name="knot">The target knot.</param>
        /// <param name="rhs">The value to subtract.</param>
        /// <returns>A new BezierKnot where position is the sum of knot.position minus rhs.</returns>
        public static BezierKnot operator -(BezierKnot knot, float3 rhs)
        {
            return new BezierKnot(knot.Position - rhs, knot.TwistAngle);
        }

        /// <summary>
        /// Create a string with the values of this knot.
        /// </summary>
        /// <returns>A summary of the values contained by this knot.</returns>
        public override string ToString() => $"{{{Position}, {TwistAngle}}}";

        /// <summary>
        /// Compare two knots for equality.
        /// </summary>
        /// <param name="other">The knot to compare against.</param>
        /// <returns>Returns true when the position, tangents, and rotation of each knot are identical.</returns>
        public bool Equals(BezierKnot other)
        {
            return Position.Equals(other.Position)
                && TwistAngle.Equals(other.TwistAngle);
        }

        /// <summary>
        /// Compare against an object for equality.
        /// </summary>
        /// <param name="obj">The object to compare against.</param>
        /// <returns>
        /// Returns true when <paramref name="obj"/> is a <see cref="BezierKnot"/> and the values of each knot are
        /// identical.
        /// </returns>
        public override bool Equals(object obj)
        {
            return obj is BezierKnot other && Equals(other);
        }

        /// <summary>
        /// Calculate a hash code for this knot.
        /// </summary>
        /// <returns>
        /// A hash code for the knot.
        /// </returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Position, TwistAngle);
        }

        // https://en.wikipedia.org/wiki/Rodrigues%27_rotation_formula
        static float3 AngleAxisVector(float3 axis, float3 vec, float radians)
        {
            var cos = math.cos(radians);
            var sin = math.sin(radians);

            return cos * vec + sin * math.cross(axis, vec) + axis * math.dot(axis, vec) * (1f - cos);
        }
    }
}
