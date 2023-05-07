using System;
using Unity.Mathematics;
using UnityEngine.YukselSplines;

namespace UnityEditor.YukselSplines
{
    interface ISplineElement : IEquatable<ISplineElement>
    {
        SplineInfo SplineInfo { get; }
        int KnotIndex { get; }
        float3 LocalPosition { get; set; }
        float3 Position { get; set; }
        bool IsValid();
    }

    struct SelectableKnot : ISplineElement, IEquatable<SelectableKnot>
    {
        public SplineInfo SplineInfo { get; }
        public int KnotIndex { get; }

        public float4x4 LocalToWorld => SplineInfo.LocalToWorld;

        public float3 Position
        {
            get => math.transform(LocalToWorld, LocalPosition);
            set => LocalPosition = math.transform(math.inverse(LocalToWorld), value);
        }

        public float3 LocalPosition
        {
            get => SplineInfo.Spline[KnotIndex].Position;
            set
            {
                var knot = SplineInfo.Spline[KnotIndex];
                knot.Position = value;
                SplineInfo.Spline[KnotIndex] = knot;
            }
        }

        public bool IsValid()
        {
            return SplineInfo.Spline != null && KnotIndex >= 0 && KnotIndex < SplineInfo.Spline.Count;
        }

        public quaternion Rotation
        {
            get => math.mul(new quaternion(LocalToWorld), LocalRotation);
            set => LocalRotation = math.mul(math.inverse(new quaternion(LocalToWorld)), value);
        }

        static readonly float LinearCutoff = 1.0f - math.acos(1f);

        public quaternion LocalRotation
        {
            get
            {
                var knot = SplineInfo.Spline[KnotIndex];
                var curve = SplineInfo.Spline.GetCurve(KnotIndex);

                var tangent = curve.EvaluateTangent(0f);
                var acceleration = curve.EvaluateAcceleration(0f);

                var cosTheta = math.dot(tangent, acceleration) / (math.length(tangent) * math.length(acceleration));
                if (math.abs(cosTheta) >= LinearCutoff)
                    acceleration = math.up();

                var up = knot.GetUpVector(tangent, acceleration);

                return quaternion.LookRotationSafe(tangent, up);
            }

            set
            {
                // TODO
                //var knot = SplineInfo.Spline[KnotIndex];
                //knot.Rotation = math.normalize(value);
                //SplineInfo.Spline[KnotIndex] = knot;
            }
        }

        public float TwistAngle
        {
            get => SplineInfo.Spline[KnotIndex].TwistAngle;
            set
            {
                var knot = SplineInfo.Spline[KnotIndex];
                knot.TwistAngle = value;
                SplineInfo.Spline[KnotIndex] = knot;
            }
        }

        public SelectableKnot(SplineInfo info, int index)
        {
            this.SplineInfo = info;
            this.KnotIndex = index;
        }

        public BezierKnot GetBezierKnot(bool worldSpace)
        {
            return worldSpace ? SplineInfo.Spline[KnotIndex].Transform(LocalToWorld) : SplineInfo.Spline[KnotIndex];
        }

        public bool Equals(ISplineElement other)
        {
            if (other is SelectableKnot knot)
                return Equals(knot);
            return false;
        }

        public bool Equals(SelectableKnot other)
        {
            return Equals(SplineInfo.Spline, other.SplineInfo.Spline) && KnotIndex == other.KnotIndex;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is SelectableKnot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SplineInfo.Spline, KnotIndex);
        }
    }
}
