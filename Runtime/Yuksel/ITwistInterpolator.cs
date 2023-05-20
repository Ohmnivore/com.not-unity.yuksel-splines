namespace UnityEngine.YukselSplines
{
    public interface ITwistInterpolator
    {
        float T { get; }

        void Initialize(float V0, float V1, float V2);

        float Evaluate(float t);
    }
}
