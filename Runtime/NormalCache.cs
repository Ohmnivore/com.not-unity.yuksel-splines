using Unity.Collections;
using Unity.Mathematics;

namespace UnityEngine.YukselSplines
{
    public struct NormalCache
    {
        struct FrenetFrame
        {
            public float3 origin;
            public float3 tangent;
            public float3 normal;
            public float3 binormal;
        }

        public const float DefaultStepSize = 0.01f;

        NativeArray<float3> Cache;

        public float3 Evaluate(float splineT)
        {
            if (Cache.Length < 2)
            {
                return math.up();
            }

            splineT *= Cache.Length - 1;

            var aT = (int)splineT;
            var bT = math.min(aT + 1, Cache.Length - 1);
            var interp = splineT - aT;

            var a = Cache[aT];
            var b = Cache[bT];

            return math.lerp(a, b, interp);
        }

        public void Populate(ISpline spline, float stepSize)
        {
            if (spline.Count < 2)
            {
                return;
            }

            var length = spline.GetLength();
            var num = (int)(length / stepSize);
            var stepSizeT = 1f / num;

            Cache = new NativeArray<float3>(num, Allocator.Persistent);

            // Construct initial frenet frame
            var firstKnot = spline[0];
            var firstCurve = spline.GetCurve(0);
            FrenetFrame frame;
            frame.origin = firstCurve.EvaluatePosition(0f);
            frame.tangent = math.normalize(firstCurve.EvaluateTangent(0f));
            frame.normal = Vector3.up;
            frame.binormal = math.normalize(math.cross(frame.tangent, frame.normal));
            Cache[0] = frame.normal;

            // Continue building remaining rotation minimizing frames
            var currentT = stepSizeT;
            FrenetFrame prevFrame;
            for (int i = 1; i < num; ++i)
            {
                prevFrame = frame;
                frame = GetNextRotationMinimizingFrame(spline, prevFrame, currentT);

                var twistAngle = spline.EvaluateTwistAngle(currentT);
                var twistedNormal = GetTwistedNormal(frame, twistAngle);

                Cache[i] = twistedNormal;

                currentT += stepSizeT;
            }
        }

        static FrenetFrame GetNextRotationMinimizingFrame(ISpline spline, FrenetFrame previousRMFrame, float nextRMFrameT)
        {
            FrenetFrame nextRMFrame;

            // Evaluate position and tangent for next RM frame
            nextRMFrame.origin = spline.EvaluatePosition(nextRMFrameT);
            nextRMFrame.tangent = math.normalize(spline.EvaluateTangent(nextRMFrameT));

            // Mirror the rotational axis and tangent
            float3 toCurrentFrame = nextRMFrame.origin - previousRMFrame.origin;
            float c1 = math.dot(toCurrentFrame, toCurrentFrame);
            float3 riL = previousRMFrame.binormal - toCurrentFrame * 2f / c1 * math.dot(toCurrentFrame, previousRMFrame.binormal);
            float3 tiL = previousRMFrame.tangent - toCurrentFrame * 2f / c1 * math.dot(toCurrentFrame, previousRMFrame.tangent);

            // Compute a more stable binormal
            float3 v2 = nextRMFrame.tangent - tiL;
            float c2 = math.dot(v2, v2);

            // Fix binormal's axis
            nextRMFrame.binormal = math.normalize(riL - v2 * 2f / c2 * math.dot(v2, riL));
            nextRMFrame.normal = math.normalize(math.cross(nextRMFrame.binormal, nextRMFrame.tangent));

            return nextRMFrame;
        }

        static float3 GetTwistedNormal(FrenetFrame frame, float TwistAngle)
        {
            var rot = quaternion.AxisAngle(frame.tangent, math.radians(TwistAngle));

            return math.rotate(rot, frame.normal);
        }
    }
}
