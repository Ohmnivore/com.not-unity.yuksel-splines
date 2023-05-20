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
}
