using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace UnityEngine.YukselSplines
{
    /// <summary>
    /// A collection of methods for extracting information about <see cref="Spline"/> types.
    /// </summary>
    /// <remarks>
    /// `SplineUtility` methods do not consider Transform values except where explicitly requested. To perform operations in world space, you can use the <see cref="SplineContainer"/> evaluate methods or build a <see cref="NativeSpline"/> with a constructor that accepts a matrix and evaluate that spline.
    /// </remarks>
    public static class SplineUtility
    {
        const int k_SubdivisionCountMin = 6;
        const int k_SubdivisionCountMax = 1024;

        /// <summary>
        /// The default tension value used for <see cref="TangentMode.AutoSmooth"/> knots.
        /// Use with <see cref="Spline.SetTangentMode(UnityEngine.YukselSplines.TangentMode)"/> and
        /// <see cref="Spline.SetAutoSmoothTension(int,float)"/> to control the curvature of the spline at control
        /// points.
        /// </summary>
        public const float DefaultTension = 1 / 3f;

        /// <summary>
        /// The tension value for a Catmull-Rom type spline.
        /// Use with <see cref="Spline.SetTangentMode(UnityEngine.YukselSplines.TangentMode)"/> and
        /// <see cref="Spline.SetAutoSmoothTension(int,float)"/> to control the curvature of the spline at control
        /// points.
        /// </summary>
        //todo Deprecate in 3.0.
        public const float CatmullRomTension = 1 / 2f;

        /// <summary>
        /// The minimum resolution allowable when unrolling a curve to hit test while picking (selecting a spline with a cursor).
        ///
        /// Pick resolution is used when determining how many segments are required to unroll a curve. Unrolling is the
        /// process of calculating a series of line segments to approximate a curve. Some functions in SplineUtility
        /// allow you to specify a resolution. Lower resolution means fewer segments, while higher resolutions result
        /// in more segments. Use lower resolutions where performance is critical and accuracy is not paramount. Use
        /// higher resolution where a fine degree of accuracy is necessary and performance is less important.
        /// </summary>
        public const int PickResolutionMin = 2;

        /// <summary>
        /// The default resolution used when unrolling a curve to hit test while picking (selecting a spline with a cursor).
        ///
        /// Pick resolution is used when determining how many segments are required to unroll a curve. Unrolling is the
        /// process of calculating a series of line segments to approximate a curve. Some functions in SplineUtility
        /// allow you to specify a resolution. Lower resolution means fewer segments, while higher resolutions result
        /// in more segments. Use lower resolutions where performance is critical and accuracy is not paramount. Use
        /// higher resolution where a fine degree of accuracy is necessary and performance is less important.
        /// </summary>
        public const int PickResolutionDefault = 4;

        /// <summary>
        /// The maximum resolution allowed when unrolling a curve to hit test while picking (selecting a spline with a cursor).
        ///
        /// Pick resolution is used when determining how many segments are required to unroll a curve. Unrolling is the
        /// process of calculating a series of line segments to approximate a curve. Some functions in SplineUtility
        /// allow you to specify a resolution. Lower resolution means fewer segments, while higher resolutions result
        /// in more segments. Use lower resolutions where performance is critical and accuracy is not paramount. Use
        /// higher resolution where a fine degree of accuracy is necessary and performance is less important.
        /// </summary>
        public const int PickResolutionMax = 64;

        /// <summary>
        /// The default resolution used when unrolling a curve to draw a preview in the Scene View.
        ///
        /// Pick resolution is used when determining how many segments are required to unroll a curve. Unrolling is the
        /// process of calculating a series of line segments to approximate a curve. Some functions in SplineUtility
        /// allow you to specify a resolution. Lower resolution means fewer segments, while higher resolutions result
        /// in more segments. Use lower resolutions where performance is critical and accuracy is not paramount. Use
        /// higher resolution where a fine degree of accuracy is necessary and performance is less important.
        /// </summary>
        public const int DrawResolutionDefault = 10;

        /// <summary>
        /// Compute interpolated position, direction and upDirection at ratio t. Calling this method to get the
        /// 3 vectors is faster than calling independently EvaluatePosition, EvaluateDirection and EvaluateUpVector
        /// for the same time t as it reduces some redundant computation.
        /// </summary>
        /// <param name="spline">The spline to interpolate.</param>
        /// <param name="t">A value between 0 and 1 representing the ratio along the curve.</param>
        /// <param name="position">Output variable for the float3 position at t.</param>
        /// <param name="tangent">Output variable for the float3 tangent at t.</param>
        /// <param name="upVector">Output variable for the float3 up direction at t.</param>
        /// <typeparam name="T">A type implementing ISpline.</typeparam>
        /// <returns>True if successful.</returns>
        public static bool Evaluate<T>(this T spline,
            float t,
            out float3 position,
            out float3 tangent,
            out float3 upVector
        ) where T : ISpline
        {
            if (spline.Count < 1)
            {
                position = float3.zero;
                tangent = new float3(0, 0, 1);
                upVector = new float3(0, 1, 0);
                return false;
            }

            var curveIndex = SplineToCurveT(spline, t, out var curveT);
            var curve = spline.GetCurve(curveIndex);

            position = CurveUtility.EvaluatePosition(curve, curveT);
            tangent = CurveUtility.EvaluateTangent(curve, curveT);
            upVector = spline.GetUpVector(curveT);

            return true;
        }

        /// <summary>
        /// Computes the interpolated position for NURBS defined by order, controlPoints, and knotVector at ratio t.
        /// </summary>
        /// <param name="t">The value between knotVector[0] and knotVector[-1] that represents the ratio along the curve.</param>
        /// <param name="controlPoints">The control points for the NURBS.</param>
        /// <param name="knotVector">The knot vector for the NURBS. There must be at least order + controlPoints.Length - 1 knots.</param>
        /// <param name="order">The order of the curve. For example, 4 for a cubic curve or 3 for quadratic.</param>
        /// <param name="position">The output variable for the float3 position at t.</param>
        /// <returns>True if successful.</returns>
        public static bool EvaluateNurbs(
            float t,
            List<float3> controlPoints,
            List<double> knotVector,
            int order,
            out float3 position
        )
        {
            position = float3.zero;
            if (knotVector.Count < controlPoints.Count + order - 1 || controlPoints.Count < order || t < 0 || 1 < t)
            {
                return false;
            }

            knotVector = new List<double>(knotVector);

            var originalFirstKnot = knotVector[0];
            var fullKnotSpan = knotVector[knotVector.Count - 1] - knotVector[0];

            //normalize knots
            if (knotVector[0] != 0 || knotVector[knotVector.Count - 1] != 1)
            {
                for (int i = 0; i < knotVector.Count; ++i)
                {
                    knotVector[i] = (knotVector[i] - originalFirstKnot) / fullKnotSpan;
                }
            }

            var span = order;
            while (span < controlPoints.Count && knotVector[span] <= t)
            {
                span++;
            }
            span--;

            var basis = SplineUtility.GetNurbsBasisFunctions(order, t, knotVector, span);

            for (int i = 0; i < order; ++i)
            {
                position += basis[i] * controlPoints[span - order + 1 + i];
            }

            return true;
        }

        static float[] GetNurbsBasisFunctions(int degree, float t, List<double> knotVector, int span)
        {
            //Constructs the Basis functions at t for the nurbs curve.
            //The nurbs basis function form can be found at this link under the section
            //"Construction of the basis functions": https://en.wikipedia.org/wiki/Non-uniform_rational_B-spline
            //This is an iterative way of computing the same thing.
            var left = new float[degree];
            var right = new float[degree];
            var N = new float[degree];

            for (int j = 0; j < degree; ++j)
            {
                left[j] = 0f;
                right[j] = 0f;
                N[j] = 1f;
            }

            for (int j = 1; j < degree; ++j)
            {

                left[j] = (float)(t - knotVector[span + 1 - j]);
                right[j] = (float)(knotVector[span + j] - t);
                var saved = 0f;
                for (int k = 0; k < j; k++)
                {
                    float temp = N[k] / (right[k + 1] + left[j - k]);
                    N[k] = saved + right[k + 1] * temp;
                    saved = left[j - k] * temp;
                }

                N[j] = saved;

            }

            return N;
        }

        /// <summary>
        /// Return an interpolated position at ratio t.
        /// </summary>
        /// <param name="spline">The spline to interpolate.</param>
        /// <param name="t">A value between 0 and 1 representing the ratio along the curve.</param>
        /// <typeparam name="T">A type implementing ISpline.</typeparam>
        /// <returns>A position on the spline.</returns>
        public static float3 EvaluatePosition<T>(this T spline, float t) where T : ISpline
        {
            if (spline.Count < 1)
                return float.PositiveInfinity;
            var curve = spline.GetCurve(SplineToCurveT(spline, t, out var curveT));
            return CurveUtility.EvaluatePosition(curve, curveT);
        }

        /// <summary>
        /// Return an interpolated twist angle at ratio t.
        /// </summary>
        /// <param name="spline">The spline to interpolate.</param>
        /// <param name="t">A value between 0 and 1 representing the ratio along the curve.</param>
        /// <typeparam name="T">A type implementing ISpline.</typeparam>
        /// <returns>A twist angle around the tangent at t on the spline.</returns>
        public static float EvaluateTwistAngle<T>(this T spline, float t) where T : ISpline
        {
            if (spline.Count < 1)
                return float.PositiveInfinity;
            var curve = spline.GetCurve(SplineToCurveT(spline, t, out var curveT));
            return CurveUtility.EvaluateTwistAngle(curve, curveT);
        }

        /// <summary>
        /// Return an interpolated direction at ratio t.
        /// </summary>
        /// <param name="spline">The spline to interpolate.</param>
        /// <param name="t">A value between 0 and 1 representing the ratio along the curve.</param>
        /// <typeparam name="T">A type implementing ISpline.</typeparam>
        /// <returns>A direction on the spline.</returns>
        public static float3 EvaluateTangent<T>(this T spline, float t) where T : ISpline
        {
            if (spline.Count < 1)
                return float.PositiveInfinity;
            var curve = spline.GetCurve(SplineToCurveT(spline, t, out var curveT));
            return CurveUtility.EvaluateTangent(curve, curveT);
        }

        /// <summary>
        /// Return an interpolated acceleration at ratio t.
        /// </summary>
        /// <param name="spline">The spline to interpolate.</param>
        /// <typeparam name="T">A type implementing ISpline.</typeparam>
        /// <param name="t">A value between 0 and 1 representing the ratio along the curve.</param>
        /// <returns>An acceleration on the spline.</returns>
        public static float3 EvaluateAcceleration<T>(this T spline, float t) where T : ISpline
        {
            if (spline.Count < 1)
                return float3.zero;
            var curve = spline.GetCurve(SplineToCurveT(spline, t, out var curveT));
            return CurveUtility.EvaluateAcceleration(curve, curveT);
        }

        /// <summary>
        /// Return an interpolated curvature at ratio t.
        /// </summary>
        /// <param name="spline">The spline to interpolate.</param>
        /// <typeparam name="T">A type implementing ISpline.</typeparam>
        /// <param name="t">A value between 0 and 1 representing the ratio along the curve.</param>
        /// <returns>A curvature on the spline.</returns>
        public static float EvaluateCurvature<T>(this T spline, float t) where T : ISpline
        {
            if (spline.Count < 1)
                return 0f;

            var curveIndex = SplineToCurveT(spline, t, out var curveT);
            var curve = spline.GetCurve(curveIndex);

            return CurveUtility.EvaluateCurvature(curve, curveT);
        }

        /// <summary>
        /// Return the curvature center at ratio t. The curvature center represents the center of the circle
        /// that is tangent to the curve at t. This circle is in the plane defined by the curve velocity (tangent)
        /// and the curve acceleration at that point.
        /// </summary>
        /// <param name="spline">The spline to interpolate.</param>
        /// <typeparam name="T">A type implementing ISpline.</typeparam>
        /// <param name="t">A value between 0 and 1 representing the ratio along the curve.</param>
        /// <returns>A point representing the curvature center associated to the position at t on the spline.</returns>
        public static float3 EvaluateCurvatureCenter<T>(this T spline, float t) where T : ISpline
        {
            if (spline.Count < 1)
                return 0f;

            var curveIndex = SplineToCurveT(spline, t, out var curveT);
            var curve = spline.GetCurve(curveIndex);

            var curvature = CurveUtility.EvaluateCurvature(curve, curveT);

            if (curvature != 0)
            {
                var radius = 1f / curvature;

                var position = CurveUtility.EvaluatePosition(curve, curveT);
                var velocity = CurveUtility.EvaluateTangent(curve, curveT);
                var acceleration = CurveUtility.EvaluateAcceleration(curve, curveT);
                var curvatureUp = math.normalize(math.cross(acceleration, velocity));
                var curvatureRight = math.normalize(math.cross(velocity, curvatureUp));

                return position + radius * curvatureRight;
            }

            return float3.zero;
        }

        /// <summary>
        /// Given a normalized interpolation (t) for a spline, calculate the curve index and curve-relative
        /// normalized interpolation.
        /// </summary>
        /// <param name="spline">The target spline.</param>
        /// <param name="splineT">A normalized spline interpolation value to be converted into curve space.</param>
        /// <param name="curveT">A normalized curve interpolation value.</param>
        /// <typeparam name="T">A type implementing ISpline.</typeparam>
        /// <returns>The curve index.</returns>
        public static int SplineToCurveT<T>(this T spline, float splineT, out float curveT) where T : ISpline
        {
            return SplineToCurveT(spline, splineT, out curveT, true);
        }

        static int SplineToCurveT<T>(this T spline, float splineT, out float curveT, bool useLUT) where T : ISpline
        {
            var knotCount = spline.Count;
            if (knotCount <= 1)
            {
                curveT = 0f;
                return 0;
            }

            splineT = math.clamp(splineT, 0, 1);
            var tLength = splineT * spline.GetLength();

            var start = 0f;
            var closed = spline.Closed;
            for (int i = 0, c = closed ? knotCount : knotCount - 1; i < c; i++)
            {
                var index = i % knotCount;
                var curveLength = spline.GetCurveLength(index);

                if (tLength <= (start + curveLength))
                {
                    curveT = useLUT ?
                        spline.GetCurveInterpolation(index, tLength - start) :
                        (tLength - start) / curveLength;
                    return index;
                }

                start += curveLength;
            }

            curveT = 1f;
            return closed ? knotCount - 1 : knotCount - 2;
        }

        /// <summary>
        /// Given an interpolation value for a curve, calculate the relative normalized spline interpolation.
        /// </summary>
        /// <param name="spline">The target spline.</param>
        /// <param name="curve">A curve index and normalized interpolation. The curve index is represented by the
        /// integer part of the float, and interpolation is the fractional part. This is the format used by
        /// <seealso cref="PathIndexUnit.Knot"/>.
        /// </param>
        /// <typeparam name="T">A type implementing ISpline.</typeparam>
        /// <returns>An interpolation value relative to normalized Spline length (0 to 1).</returns>
        /// <seealso cref="SplineToCurveT{T}"/>
        public static float CurveToSplineT<T>(this T spline, float curve) where T : ISpline
        {
            // Clamp negative curve index to 0
            if (spline.Count <= 1 || curve < 0f)
                return 0f;

            // Clamp postive curve index beyond last knot to 1
            if (curve >= (spline.Closed ? spline.Count : spline.Count - 1))
                return 1f;

            var curveIndex = (int)math.floor(curve);

            float t = 0f;

            for (int i = 0; i < curveIndex; i++)
                t += spline.GetCurveLength(i);

            t += spline.GetCurveLength(curveIndex) * math.frac(curve);

            return t / spline.GetLength();
        }

        /// <summary>
        /// Calculate the length of a spline when transformed by a matrix.
        /// </summary>
        /// <param name="spline"></param>
        /// <typeparam name="T">A type implementing ISpline.</typeparam>
        /// <param name="transform"></param>
        /// <returns></returns>
        public static float CalculateLength<T>(this T spline, float4x4 transform) where T : ISpline
        {
            using var nativeSpline = new NativeSpline(spline, transform);
            return nativeSpline.GetLength();
        }

        /// <summary>
        /// Calculates the number of curves in a spline.
        /// </summary>
        /// <typeparam name="T">A type implementing ISpline.</typeparam>
        /// <param name="spline"></param>
        /// <returns>The number of curves in a spline.</returns>
        public static int GetCurveCount<T>(this T spline) where T : ISpline
        {
            return math.max(0, spline.Count - (spline.Closed ? 0 : 1));
        }

        /// <summary>
        /// Calculate the bounding box of a Spline.
        /// </summary>
        /// <param name="spline">The spline for which to calculate bounds.</param>
        /// <typeparam name="T">A type implementing ISpline.</typeparam>
        /// <returns>The bounds of a spline.</returns>
        public static Bounds GetBounds<T>(this T spline) where T : ISpline
        {
            return GetBounds(spline, float4x4.identity);
        }

        /// <summary>
        /// Creates a bounding box for a spline.
        /// </summary>
        /// <param name="spline">The spline to calculate bounds for.</param>
        /// <param name="transform">The matrix to transform the spline's elements with.</param>
        /// <typeparam name="T">A type implementing ISpline.</typeparam>
        /// <returns>The bounds of a spline.</returns>
        public static Bounds GetBounds<T>(this T spline, float4x4 transform) where T : ISpline
        {
            if (spline.Count < 1)
                return default;

            var knot = spline[0];
            Bounds bounds = new Bounds(math.transform(transform, knot.Position), Vector3.zero);

            for (int i = 1, c = spline.Count; i < c; ++i)
            {
                knot = spline[i];
                bounds.Encapsulate(math.transform(transform, knot.Position));
            }

            return bounds;
        }

        /// <summary>
        /// Gets the number of segments for a specified spline length and resolution.
        /// </summary>
        /// <param name="length">The length of the spline to consider.</param>
        /// <param name="resolution">The value used to calculate the number of segments for a length. This is calculated
        /// as max(MIN_SEGMENTS, min(MAX_SEGMENTS, sqrt(length) * resolution)).
        /// </param>
        /// <returns>
        /// The number of segments for a length and resolution.
        /// </returns>
        [Obsolete("Use " + nameof(GetSubdivisionCount) + " instead.", false)]
        public static int GetSegmentCount(float length, int resolution) => GetSubdivisionCount(length, resolution);

        /// <summary>
        /// Gets the number of subdivisions for a spline length and resolution.
        /// </summary>
        /// <param name="length">The length of the spline to consider.</param>
        /// <param name="resolution">The resolution to consider. Higher resolutions result in more
        /// precise representations. However, higher resolutions have higher performance requirements.
        /// </param>
        /// <returns>
        /// The number of subdivisions as calculated for given length and resolution.
        /// </returns>
        public static int GetSubdivisionCount(float length, int resolution)
        {
            return (int)math.max(k_SubdivisionCountMin, math.min(k_SubdivisionCountMax, math.sqrt(length) * resolution));
        }

        struct Segment
        {
            public float start, length;

            public Segment(float start, float length)
            {
                this.start = start;
                this.length = length;
            }
        }

        static Segment GetNearestPoint<T>(T spline,
            float3 ro, float3 rd,
            Segment range,
            out float distance, out float3 nearest, out float time,
            int segments) where T : ISpline
        {
            distance = float.PositiveInfinity;
            nearest = float.PositiveInfinity;
            time = float.PositiveInfinity;
            Segment segment = new Segment(-1f, 0f);

            float t0 = range.start;
            float3 a = EvaluatePosition(spline, t0);

            for (int i = 1; i < segments; i++)
            {
                float t1 = range.start + (range.length * (i / (segments - 1f)));
                float3 b = EvaluatePosition(spline, t1);
                var (rayPoint, linePoint) = SplineMath.RayLineNearestPoint(ro, rd, a, b, out _, out var lineParam);
                float dsqr = math.lengthsq(linePoint - rayPoint);

                if (dsqr < distance)
                {
                    segment.start = t0;
                    segment.length = t1 - t0;
                    time = segment.start + segment.length * lineParam;
                    distance = dsqr;
                    nearest = linePoint;
                }

                t0 = t1;
                a = b;
            }

            distance = math.sqrt(distance);
            return segment;
        }

        static Segment GetNearestPoint<T>(T spline,
            float3 point,
            Segment range,
            out float distance, out float3 nearest, out float time,
            int segments) where T : ISpline
        {
            distance = float.PositiveInfinity;
            nearest = float.PositiveInfinity;
            time = float.PositiveInfinity;
            Segment segment = new Segment(-1f, 0f);

            float t0 = range.start;
            float3 a = EvaluatePosition(spline, t0);


            for (int i = 1; i < segments; i++)
            {
                float t1 = range.start + (range.length * (i / (segments - 1f)));
                float3 b = EvaluatePosition(spline, t1);
                var p = SplineMath.PointLineNearestPoint(point, a, b, out var lineParam);
                float dsqr = math.distancesq(p, point);

                if (dsqr < distance)
                {
                    segment.start = t0;
                    segment.length = t1 - t0;
                    time = segment.start + segment.length * lineParam;
                    distance = dsqr;

                    nearest = p;
                }

                t0 = t1;
                a = b;
            }

            distance = math.sqrt(distance);
            return segment;
        }

        /// <summary>
        /// Calculate the point on a spline nearest to a ray.
        /// </summary>
        /// <param name="spline">The input spline to search for nearest point.</param>
        /// <param name="ray">The input ray to search against.</param>
        /// <param name="nearest">The point on a spline nearest to the input ray. The accuracy of this value is
        /// affected by the <paramref name="resolution"/>.</param>
        /// <typeparam name="T">A type implementing ISpline.</typeparam>
        /// <param name="t">The normalized time value to the nearest point.</param>
        /// <param name="resolution">Affects how many segments to split a spline into when calculating the nearest point.
        /// Higher values mean smaller and more segments, which increases accuracy at the cost of processing time.
        /// The minimum resolution is defined by <seealso cref="PickResolutionMin"/>, and the maximum is defined by
        /// <seealso cref="PickResolutionMax"/>.
        /// In most cases, the default resolution is appropriate. Use with <paramref name="iterations"/> to fine tune
        /// point accuracy.
        /// </param>
        /// <param name="iterations">
        /// The nearest point is calculated by finding the nearest point on the entire length
        /// of the spline using <paramref name="resolution"/> to divide into equally spaced line segments. Successive
        /// iterations will then subdivide further the nearest segment, producing more accurate results. In most cases,
        /// the default value is sufficient, but if extreme accuracy is required this value can be increased to a
        /// maximum of <see cref="PickResolutionMax"/>.
        /// </param>
        /// <returns>The distance from ray to nearest point.</returns>
        public static float GetNearestPoint<T>(T spline,
            Ray ray,
            out float3 nearest,
            out float t,
            int resolution = PickResolutionDefault,
            int iterations = 2) where T : ISpline
        {
            float distance = float.PositiveInfinity;
            nearest = float.PositiveInfinity;
            float3 ro = ray.origin, rd = ray.direction;
            Segment segment = new Segment(0f, 1f);
            t = 0f;
            int res = math.min(math.max(PickResolutionMin, resolution), PickResolutionMax);

            for (int i = 0, c = math.min(10, iterations); i < c; i++)
            {
                int segments = GetSubdivisionCount(spline.GetLength() * segment.length, res);
                segment = GetNearestPoint(spline, ro, rd, segment, out distance, out nearest, out t, segments);
            }

            return distance;
        }

        /// <summary>
        /// Calculate the point on a spline nearest to a point.
        /// </summary>
        /// <param name="spline">The input spline to search for nearest point.</param>
        /// <param name="point">The input point to compare.</param>
        /// <param name="nearest">The point on a spline nearest to the input point. The accuracy of this value is
        /// affected by the <paramref name="resolution"/>.</param>
        /// <param name="t">The normalized interpolation ratio corresponding to the nearest point.</param>
        /// <param name="resolution">Affects how many segments to split a spline into when calculating the nearest point.
        /// Higher values mean smaller and more segments, which increases accuracy at the cost of processing time.
        /// The minimum resolution is defined by <seealso cref="PickResolutionMin"/>, and the maximum is defined by
        /// <seealso cref="PickResolutionMax"/>.
        /// In most cases, the default resolution is appropriate. Use with <paramref name="iterations"/> to fine tune
        /// point accuracy.
        /// </param>
        /// <param name="iterations">
        /// The nearest point is calculated by finding the nearest point on the entire length
        /// of the spline using <paramref name="resolution"/> to divide into equally spaced line segments. Successive
        /// iterations will then subdivide further the nearest segment, producing more accurate results. In most cases,
        /// the default value is sufficient, but if extreme accuracy is required this value can be increased to a
        /// maximum of <see cref="PickResolutionMax"/>.
        /// </param>
        /// <typeparam name="T">A type implementing ISpline.</typeparam>
        /// <returns>The distance from input point to nearest point on spline.</returns>
        public static float GetNearestPoint<T>(T spline,
            float3 point,
            out float3 nearest,
            out float t,
            int resolution = PickResolutionDefault,
            int iterations = 2) where T : ISpline
        {
            float distance = float.PositiveInfinity;
            nearest = float.PositiveInfinity;
            Segment segment = new Segment(0f, 1f);
            t = 0f;
            int res = math.min(math.max(PickResolutionMin, resolution), PickResolutionMax);

            for (int i = 0, c = math.min(10, iterations); i < c; i++)
            {
                int segments = GetSubdivisionCount(spline.GetLength() * segment.length, res);
                segment = GetNearestPoint(spline, point, segment, out distance, out nearest, out t, segments);
            }

            return distance;
        }

        /// <summary>
        /// Given a Spline and interpolation ratio, calculate the 3d point at a linear distance from point at spline.EvaluatePosition(t).
        /// Returns the corresponding time associated to this 3d position on the Spline.
        /// </summary>
        /// <param name="spline">The Spline on which to compute the point.</param>
        /// <typeparam name="T">A type implementing ISpline.</typeparam>
        /// <param name="fromT">The Spline interpolation ratio 't' (normalized) from which the next position need to be computed.</param>
        /// <param name="relativeDistance">
        /// The relative distance at which the new point should be placed. A negative value will compute a point at a
        /// 'resultPointTime' previous to 'fromT' (backward search).
        /// </param>
        /// <param name="resultPointT">The normalized interpolation ratio of the resulting point.</param>
        /// <returns>The 3d point from the spline located at a linear distance from the point at t.</returns>
        public static float3 GetPointAtLinearDistance<T>(this T spline,
            float fromT,
            float relativeDistance,
            out float resultPointT) where T : ISpline
        {
            const float epsilon = 0.001f;
            if (fromT < 0)
            {
                resultPointT = 0f;
                return spline.EvaluatePosition(0f);
            }

            var length = spline.GetLength();
            var lengthAtT = fromT * length;
            float currentLength = lengthAtT;
            if (currentLength + relativeDistance >= length) //relativeDistance >= 0 -> Forward search
            {
                resultPointT = 1f;
                return spline.EvaluatePosition(1f);
            }
            else if (currentLength + relativeDistance <= 0) //relativeDistance < 0 -> Forward search
            {
                resultPointT = 0f;
                return spline.EvaluatePosition(0f);
            }

            var currentPos = spline.EvaluatePosition(fromT);
            resultPointT = fromT;

            var forwardSearch = relativeDistance >= 0;
            var residual = math.abs(relativeDistance);
            float linearDistance = 0;
            float3 point = spline.EvaluatePosition(fromT);
            while (residual > epsilon && (forwardSearch ? resultPointT < 1f : resultPointT > 0))
            {
                currentLength += forwardSearch ? residual : -residual;
                resultPointT = currentLength / length;

                if (resultPointT > 1f) //forward search
                {
                    resultPointT = 1f;
                    point = spline.EvaluatePosition(1f);
                }
                else if (resultPointT < 0f) //backward search
                {
                    resultPointT = 0f;
                    point = spline.EvaluatePosition(0f);
                }

                point = spline.EvaluatePosition(resultPointT);
                linearDistance = math.distance(currentPos, point);
                residual = math.abs(relativeDistance) - linearDistance;
            }

            return point;
        }

        /// <summary>
        /// Given a normalized interpolation ratio, calculate the associated interpolation value in another targetPathUnit regarding a specific spline.
        /// </summary>
        /// <param name="spline">The Spline to use for the conversion, this is necessary to compute Normalized and Distance PathIndexUnits.</param>
        /// <param name="t">Normalized interpolation ratio (0 to 1).</param>
        /// <param name="targetPathUnit">The PathIndexUnit to which 't' should be converted.</param>
        /// <typeparam name="T">A type implementing ISpline.</typeparam>
        /// <returns>The interpolation value converted to targetPathUnit.</returns>
        public static float ConvertIndexUnit<T>(this T spline, float t, PathIndexUnit targetPathUnit)
            where T : ISpline
        {
            if (targetPathUnit == PathIndexUnit.Normalized)
                return WrapInterpolation(t, spline.Closed);

            return ConvertNormalizedIndexUnit(spline, t, targetPathUnit);
        }

        /// <summary>
        /// Given an interpolation value using a certain PathIndexUnit type, calculate the associated interpolation value in another targetPathUnit regarding a specific spline.
        /// </summary>
        /// <param name="spline">The Spline to use for the conversion, this is necessary to compute Normalized and Distance PathIndexUnits.</param>
        /// <param name="t">Interpolation in the original PathIndexUnit.</param>
        /// <param name="fromPathUnit">The PathIndexUnit for the original interpolation value.</param>
        /// <param name="targetPathUnit">The PathIndexUnit to which 't' should be converted.</param>
        /// <typeparam name="T">A type implementing ISpline.</typeparam>
        /// <returns>The interpolation value converted to targetPathUnit.</returns>
        public static float ConvertIndexUnit<T>(this T spline, float t, PathIndexUnit fromPathUnit, PathIndexUnit targetPathUnit)
            where T : ISpline
        {
            if (fromPathUnit == targetPathUnit)
            {
                if (targetPathUnit == PathIndexUnit.Normalized)
                    t = WrapInterpolation(t, spline.Closed);

                return t;
            }

            return ConvertNormalizedIndexUnit(spline, GetNormalizedInterpolation(spline, t, fromPathUnit), targetPathUnit);
        }

        static float ConvertNormalizedIndexUnit<T>(T spline, float t, PathIndexUnit targetPathUnit) where T : ISpline
        {
            switch (targetPathUnit)
            {
                case PathIndexUnit.Knot:
                    //LUT SHOULD NOT be used here as PathIndexUnit.KnotIndex is linear regarding the distance
                    //(and thus not be interpreted using the LUT and the interpolated T)
                    int splineIndex = spline.SplineToCurveT(t, out float curveTime, false);
                    return splineIndex + curveTime;
                case PathIndexUnit.Distance:
                    return t * spline.GetLength();
                default:
                    return t;
            }
        }

        static float WrapInterpolation(float t, bool closed)
        {
            if (!closed)
                return math.clamp(t, 0f, 1f);

            return t % 1f == 0f ? math.clamp(t, 0f, 1f) : t - math.floor(t);
        }

        /// <summary>
        /// Given an interpolation value in any PathIndexUnit type, calculate the normalized interpolation ratio value
        /// relative to a <see cref="Spline"/>.
        /// </summary>
        /// <param name="spline">The Spline to use for the conversion, this is necessary to compute Normalized and Distance PathIndexUnits.</param>
        /// <param name="t">The 't' value to normalize in the original PathIndexUnit.</param>
        /// <param name="originalPathUnit">The PathIndexUnit from the original 't'.</param>
        /// <typeparam name="T">A type implementing ISpline.</typeparam>
        /// <returns>The normalized interpolation ratio (0 to 1).</returns>
        public static float GetNormalizedInterpolation<T>(T spline, float t, PathIndexUnit originalPathUnit) where T : ISpline
        {
            switch (originalPathUnit)
            {
                case PathIndexUnit.Knot:
                    return WrapInterpolation(CurveToSplineT(spline, t), spline.Closed);
                case PathIndexUnit.Distance:
                    var length = spline.GetLength();
                    return WrapInterpolation(length > 0 ? t / length : 0f, spline.Closed);
                default:
                    return WrapInterpolation(t, spline.Closed);
            }
        }

        /// <summary>
        /// Gets the index of a knot that precedes a spline index. This method uses the <see cref="Spline.Count"/>
        /// and <see cref="Spline.Closed"/> properties to ensure that it returns the correct index of the knot.
        /// </summary>
        /// <param name="spline">The spline to consider.</param>
        /// <param name="index">The current index to consider.</param>
        /// <typeparam name="T">A type that implements ISpline.</typeparam>
        /// <returns>Returns a knot index that precedes the `index` on the considered spline.</returns>
        public static int PreviousIndex<T>(this T spline, int index) where T : ISpline
            => PreviousIndex(index, spline.Count, spline.Closed);

        /// <summary>
        /// Gets the index of a knot that follows a spline index. This method uses the <see cref="Spline.Count"/> and
        /// <see cref="Spline.Closed"/> properties to ensure that it returns the correct index of the knot.
        /// </summary>
        /// <param name="spline">The spline to consider.</param>
        /// <param name="index">The current index to consider.</param>
        /// <typeparam name="T">A type that implements ISpline.</typeparam>
        /// <returns>The knot index after `index` on the considered spline.</returns>
        public static int NextIndex<T>(this T spline, int index) where T : ISpline
            => NextIndex(index, spline.Count, spline.Closed);

        /// <summary>
        /// Gets the <see cref="BezierKnot"/> before spline[index]. This method uses the <see cref="Spline.Count"/>
        /// and <see cref="Spline.Closed"/> properties to ensure that it returns the correct knot.
        /// </summary>
        /// <param name="spline">The spline to consider.</param>
        /// <param name="index">The current index to consider.</param>
        /// <typeparam name="T">A type that implements ISpline.</typeparam>
        /// <returns>The knot before the knot at spline[index].</returns>
        public static BezierKnot Previous<T>(this T spline, int index) where T : ISpline
            => spline[PreviousIndex(spline, index)];

        /// <summary>
        /// Gets the <see cref="BezierKnot"/> after spline[index]. This method uses the <see cref="Spline.Count"/>
        /// and <see cref="Spline.Closed"/> properties to ensure that it returns the correct knot.
        /// </summary>
        /// <param name="spline">The spline to consider.</param>
        /// <param name="index">The current index to consider.</param>
        /// <typeparam name="T">A type that implements ISpline.</typeparam>
        /// <returns>The knot after the knot at spline[index].</returns>
        public static BezierKnot Next<T>(this T spline, int index) where T : ISpline
            => spline[NextIndex(spline, index)];

        internal static int PreviousIndex(int index, int count, bool wrap)
        {
            return wrap ? (index + (count - 1)) % count : math.max(index - 1, 0);
        }

        internal static int NextIndex(int index, int count, bool wrap)
        {
            return wrap ? (index + 1) % count : math.min(index + 1, count - 1);
        }

        internal static quaternion GetKnotRotation(float3 tangent, float3 normal)
        {
            if (math.lengthsq(tangent) == 0f)
                tangent = math.rotate(Quaternion.FromToRotation(math.up(), normal), math.forward());

            float3 up = Mathf.Approximately(math.abs(math.dot(tangent, normal)), 1f)
                ? math.cross(tangent, math.right())
                : Vector3.ProjectOnPlane(normal, tangent).normalized;

            return quaternion.LookRotationSafe(tangent, up);
        }

        /// <summary>
        /// Reset a transform position to a position while keeping knot positions in the same place. This modifies both
        /// knot positions and transform position.
        /// </summary>
        /// <param name="container">The target spline.</param>
        /// <param name="position">The point in world space to move the pivot to.</param>
        public static void SetPivot(SplineContainer container, Vector3 position)
        {
            var transform = container.transform;
            var delta = position - transform.position;
            transform.position = position;
            var spline = container.Spline;
            for (int i = 0, c = spline.Count; i < c; i++)
                spline[i] = spline[i] - delta;
        }

        /// <summary>
        /// Creates a new spline and adds it to the <see cref="ISplineContainer"/>.
        /// </summary>
        /// <param name="container">The target container.</param>
        /// <typeparam name="T">A type that implements <see cref="ISplineContainer"/>.</typeparam>
        /// <returns>Returns the spline that was created and added to the container.</returns>
        public static Spline AddSpline<T>(this T container) where T : ISplineContainer
        {
            var spline = new Spline();
            AddSpline(container, spline);
            return spline;
        }

        /// <summary>
        /// Add a new <see cref="Spline"/> to the <see cref="ISplineContainer"/>.
        /// </summary>
        /// <param name="container">The target container.</param>
        /// <param name="spline">The spline to append to this container.</param>
        /// <typeparam name="T">A type that implements <see cref="ISplineContainer"/>.</typeparam>
        public static void AddSpline<T>(this T container, Spline spline) where T : ISplineContainer
        {
            var splines = new List<Spline>(container.Splines);
            splines.Add(spline);
            container.Splines = splines;
        }

        /// <summary>
        /// Removes a spline from a <see cref="ISplineContainer"/>.
        /// </summary>
        /// <param name="container">The target container.</param>
        /// <param name="splineIndex">The index of the spline to remove from the SplineContainer.</param>
        /// <typeparam name="T">A type that implements <see cref="ISplineContainer"/>.</typeparam>
        /// <returns>Returns true if the spline was removed from the container.</returns>
        public static bool RemoveSplineAt<T>(this T container, int splineIndex) where T : ISplineContainer
        {
            if (splineIndex < 0 || splineIndex >= container.Splines.Count)
                return false;

            var splines = new List<Spline>(container.Splines);
            splines.RemoveAt(splineIndex);
            container.KnotLinkCollection.SplineRemoved(splineIndex);
            container.Splines = splines;

            return true;
        }

        /// <summary>
        /// Removes a spline from a <see cref="ISplineContainer"/>.
        /// </summary>
        /// <param name="container">The target SplineContainer.</param>
        /// <param name="spline">The spline to remove from the SplineContainer.</param>
        /// <typeparam name="T">A type that implements <see cref="ISplineContainer"/>.</typeparam>
        /// <returns>Returns true if the spline was removed from the container.</returns>
        public static bool RemoveSpline<T>(this T container, Spline spline) where T : ISplineContainer
        {
            var splines = new List<Spline>(container.Splines);
            var index = splines.IndexOf(spline);
            if (index < 0)
                return false;

            splines.RemoveAt(index);
            container.KnotLinkCollection.SplineRemoved(index);
            container.Splines = splines;

            return true;
        }

        /// <summary>
        /// Reorders a spline in a <see cref="ISplineContainer"/>.
        /// </summary>
        /// <param name="container">The target SplineContainer.</param>
        /// <param name="previousSplineIndex">The previous index of the spline to reorder in the SplineContainer.</param>
        /// <param name="newSplineIndex">The new index of the spline to reorder in the SplineContainer.</param>
        /// <typeparam name="T">A type that implements <see cref="ISplineContainer"/>.</typeparam>
        /// <returns>Returns true if the spline was reordered in the container.</returns>
        public static bool ReorderSpline<T>(this T container, int previousSplineIndex, int newSplineIndex) where T : ISplineContainer
        {
            if (previousSplineIndex < 0 || previousSplineIndex >= container.Splines.Count ||
                newSplineIndex < 0 || newSplineIndex >= container.Splines.Count)
                return false;

            var splines = new List<Spline>(container.Splines);
            var spline = splines[previousSplineIndex];
            splines.RemoveAt(previousSplineIndex);
            splines.Insert(newSplineIndex, spline);

            container.KnotLinkCollection.SplineIndexChanged(previousSplineIndex, newSplineIndex);
            container.Splines = splines;

            return true;
        }

        internal static bool IsIndexValid<T>(T container, SplineKnotIndex index) where T : ISplineContainer
        {
            return index.Knot >= 0 && index.Knot < container.Splines[index.Spline].Count &&
                index.Spline < container.Splines.Count && index.Knot < container.Splines[index.Spline].Count;
        }

        /// <summary>
        /// Sets the position of all knots linked to the knot at `index` in an <see cref="ISplineContainer"/> to the same position.
        /// </summary>
        /// <param name="container">The target container.</param>
        /// <param name="index">The `SplineKnotIndex` of the knot to use to synchronize the positions.</param>
        /// <typeparam name="T">A type that implements <see cref="ISplineContainer"/>.</typeparam>
        public static void SetLinkedKnotPosition<T>(this T container, SplineKnotIndex index) where T : ISplineContainer
        {
            if (!container.KnotLinkCollection.TryGetKnotLinks(index, out var knots))
                return;

            var splines = container.Splines;
            var position = splines[index.Spline][index.Knot].Position;

            foreach (var i in knots)
            {
                if (!IsIndexValid(container, i))
                    return;

                var knot = splines[i.Spline][i.Knot];
                knot.Position = position;
                splines[i.Spline].SetKnotNoNotify(i.Knot, knot);
            }
        }

        /// <summary>
        /// Links two knots in an <see cref="ISplineContainer"/>. The two knots can be on different splines, but both must be in the referenced SplineContainer.
        /// If these knots are linked to other knots, all existing links are kept and updated.
        /// </summary>
        /// <param name="container">The target SplineContainer.</param>
        /// <param name="knotA">The first knot to link.</param>
        /// <typeparam name="T">A type that implements <see cref="ISplineContainer"/>.</typeparam>
        /// <param name="knotB">The second knot to link.</param>
        public static void LinkKnots<T>(this T container, SplineKnotIndex knotA, SplineKnotIndex knotB) where T : ISplineContainer
        {
            container.KnotLinkCollection.Link(knotA, knotB);
        }

        /// <summary>
        /// Unlinks several knots from an <see cref="ISplineContainer"/>. A knot in `knots` disconnects from other knots it was linked to.
        /// </summary>
        /// <param name="container">The target SplineContainer.</param>
        /// <param name="knots">The knot to unlink.</param>
        /// <typeparam name="T">A type implementing <see cref="ISplineContainer"/>.</typeparam>
        public static void UnlinkKnots<T>(this T container, IReadOnlyList<SplineKnotIndex> knots) where T : ISplineContainer
        {
            foreach (var knot in knots)
                container.KnotLinkCollection.Unlink(knot);
        }

        /// <summary>
        /// Copies knot links between two splines of the same <see cref="ISplineContainer"/>.
        /// </summary>
        /// <param name="container">The target SplineContainer.</param>
        /// <param name="srcSplineIndex">The index of the source spline to copy from.</param>
        /// <param name="destSplineIndex">The index of the destination spline to copy to.</param>
        /// <typeparam name="T">A type implementing <see cref="ISplineContainer"/>.</typeparam>
        /// <remarks>
        /// The knot links will only be copied if both of the spline indices are valid and both splines have the same amount of knots.
        /// </remarks>
        public static void CopyKnotLinks<T>(this T container, int srcSplineIndex, int destSplineIndex) where T : ISplineContainer
        {
            if ((srcSplineIndex < 0 || srcSplineIndex >= container.Splines.Count) ||
                (destSplineIndex < 0 || destSplineIndex >= container.Splines.Count))
                return;

            var srcSpline = container.Splines[srcSplineIndex];
            var dstSpline = container.Splines[destSplineIndex];

            if (srcSpline.Count == 0 || srcSpline.Count != dstSpline.Count)
                return;

            for (int i = 0, c = srcSpline.Count; i < c; ++i)
            {
                if (container.KnotLinkCollection.TryGetKnotLinks(new SplineKnotIndex(srcSplineIndex, i), out _))
                    container.KnotLinkCollection.Link(new SplineKnotIndex(srcSplineIndex, i), new SplineKnotIndex(destSplineIndex, i));
            }
        }

        /// <summary>
        /// Removes redundant points in a poly line to form a similar shape with fewer points.
        /// </summary>
        /// <param name="line">The poly line to act on.</param>
        /// <param name="epsilon">The maximum distance from the reduced poly line shape for a point to be discarded.</param>
        /// <typeparam name="T">The collection type. Usually this will be list or array of float3.</typeparam>
        /// <returns>Returns a new list with a poly line matching the shape of the original line with fewer points.</returns>
        public static List<float3> ReducePoints<T>(T line, float epsilon = .15f) where T : IList<float3>
        {
            var ret = new List<float3>();
            var rdp = new RamerDouglasPeucker<T>(line);
            rdp.Reduce(ret, epsilon);
            return ret;
        }

        /// <summary>
        /// Removes redundant points in a poly line to form a similar shape with fewer points.
        /// </summary>
        /// <param name="line">The poly line to act on.</param>
        /// <param name="results">A pre-allocated list to be filled with the new reduced line points.</param>
        /// <param name="epsilon">The maximum distance from the reduced poly line shape for a point to be discarded.</param>
        /// <typeparam name="T">The collection type. Usually this will be list or array of float3.</typeparam>
        public static void ReducePoints<T>(T line, List<float3> results, float epsilon = .15f) where T : IList<float3>
        {
            var rdp = new RamerDouglasPeucker<T>(line);
            rdp.Reduce(results, epsilon);
        }
    }
}
