using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace UnityEditor.YukselSplines
{
    [Serializable]
    struct SelectableSplineElement : IEquatable<SelectableSplineElement>
    {
        public Object target;
        public int targetIndex;
        public int knotIndex;

        public SelectableSplineElement(ISplineElement element)
        {
            target = element.SplineInfo.Object;
            targetIndex = element.SplineInfo.Index;
            knotIndex = element.KnotIndex;
        }

        public bool Equals(SelectableSplineElement other)
        {
            return target == other.target && targetIndex == other.targetIndex && knotIndex == other.knotIndex;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is SelectableSplineElement other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(target, targetIndex, knotIndex);
        }
    }

    sealed class SelectionContext : ScriptableSingleton<SelectionContext>
    {
        public List<SelectableSplineElement> selection = new List<SelectableSplineElement>();
        public int version;
    }
}
