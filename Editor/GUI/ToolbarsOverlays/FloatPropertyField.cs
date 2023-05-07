using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine.UIElements;
#if !UNITY_2022_1_OR_NEWER
using UnityEditor.UIElements;
#endif

namespace UnityEditor.YukselSplines
{
    class FloatPropertyField<T> : FloatField
        where T : ISplineElement
    {
        static readonly List<float> s_FloatBuffer = new List<float>();
        static readonly SplineGUIUtility.EqualityComparer<float> s_Comparer = (a, b) => a.Equals(b);

        readonly Func<T, float> m_Get;
        readonly Action<T, float> m_Set;

        IReadOnlyList<T> m_Elements = new List<T>(0);

        public event Action changed;

        public FloatPropertyField(string label, Func<T, float> get, Action<T, float> set) : base(label)
        {
            m_Get = get;
            m_Set = set;

            this.RegisterValueChangedCallback(Apply);
        }

        public void Update(IReadOnlyList<T> elements)
        {
            m_Elements = elements;

            s_FloatBuffer.Clear();
            for (int i = 0; i < elements.Count; ++i)
                s_FloatBuffer.Add(m_Get.Invoke(elements[i]));

            var value = s_FloatBuffer.Count > 0 ? s_FloatBuffer[0] : 0;
            this.showMixedValue = SplineGUIUtility.HasMultipleValues(s_FloatBuffer, s_Comparer);
            if (!this.showMixedValue)
                this.SetValueWithoutNotify(value);
        }

        void Apply(ChangeEvent<float> evt)
        {
            EditorSplineUtility.RecordObjects(m_Elements, SplineInspectorOverlay.SplineChangeUndoMessage);

            ElementInspector.ignoreKnotCallbacks = true;
            for (int i = 0; i < m_Elements.Count; ++i)
            {
                value = evt.newValue;
                m_Set.Invoke(m_Elements[i], value);
            }

            this.showMixedValue = false;
            this.SetValueWithoutNotify(evt.newValue);
            changed?.Invoke();
            ElementInspector.ignoreKnotCallbacks = false;
        }
    }
}
