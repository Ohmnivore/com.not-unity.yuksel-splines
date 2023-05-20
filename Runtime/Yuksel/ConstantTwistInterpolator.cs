using System;
using Unity.Mathematics;

namespace UnityEngine.YukselSplines
{
    struct ConstantTwistInterpolator : ITwistInterpolator, IEquatable<ConstantTwistInterpolator>
    {
        float Constant;

        public float T => 0.5f;

        public void Initialize(float a, float b, float c)
        {
            Constant = b;
        }

        public float Evaluate(float t)
        {
            return Constant;
        }

        public bool Equals(ConstantTwistInterpolator other)
        {
            return Constant.Equals(other.Constant);
        }

        public override bool Equals(object obj)
        {
            return obj is ConstantTwistInterpolator other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Constant.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(ConstantTwistInterpolator left, ConstantTwistInterpolator right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ConstantTwistInterpolator left, ConstantTwistInterpolator right)
        {
            return !left.Equals(right);
        }
    }
}
