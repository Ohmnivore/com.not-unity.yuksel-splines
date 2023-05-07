using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.YukselSplines;
#if !UNITY_2022_1_OR_NEWER
using UnityEditor.UIElements;
#endif

namespace UnityEditor.YukselSplines
{
    sealed class BezierKnotDrawer : ElementDrawer<SelectableKnot>
    {
        static readonly string k_PositionTooltip = L10n.Tr("Knot Position");
        static readonly string k_TwistTooltip = L10n.Tr("Knot Twist Angle");

        readonly Float3PropertyField<SelectableKnot> m_Position;
        readonly FloatPropertyField<SelectableKnot> m_Twist;

        public BezierKnotDrawer()
        {
            VisualElement row;
            Add(row = new VisualElement(){name = "Vector3WithIcon"});
            row.tooltip = k_PositionTooltip;
            row.style.flexDirection = FlexDirection.Row;
            row.Add(new VisualElement(){name = "PositionIcon"});
            row.Add(m_Position = new Float3PropertyField<SelectableKnot>("",
                (knot) => knot.LocalPosition, 
                (knot, value) => knot.LocalPosition = value)
                { name = "Position" });

            m_Position.style.flexGrow = 1;

            Add(row = new VisualElement(){name = "Vector3WithIcon"});
            row.tooltip = k_TwistTooltip;
            row.style.flexDirection = FlexDirection.Row;
            row.Add(new VisualElement(){name = "RotationIcon"});
            row.Add(m_Twist = new FloatPropertyField<SelectableKnot>("Twist",
                (knot) => knot.TwistAngle,
                (knot, value) => knot.TwistAngle = value)
                { name = "Twist Angle" });

            m_Twist.style.flexGrow = 1;

            Add(new Separator());

            Add(new Separator());
        }

        public override string GetLabelForTargets()
        {
            if (targets.Count > 1)
                return $"<b>({targets.Count}) Knots</b> selected";
            
            return $"<b>Knot {target.KnotIndex}</b> (<b>Spline {target.SplineInfo.Index}</b>) selected";
        }

        public override void Update()
        {
            base.Update();
            
            m_Position.Update(targets);
            m_Twist.Update(targets);
        }
    }
}
