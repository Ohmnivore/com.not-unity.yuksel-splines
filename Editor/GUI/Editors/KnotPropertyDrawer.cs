using UnityEngine;
using UnityEngine.YukselSplines;

namespace UnityEditor.YukselSplines
{
    /// <summary>
    /// Specialized UI for drawing a knot property drawer with additional data (ex, TangentMode from Spline.MetaData).
    /// Additionally supports inline fields a little longer than the regular inspector wide mode would allow.
    /// </summary>
    static class KnotPropertyDrawerUI
    {
        static readonly GUIContent k_Position = EditorGUIUtility.TrTextContent("Position");
        static readonly GUIContent k_TwistAngle = EditorGUIUtility.TrTextContent("Twist Angle");

        const float k_IndentPad = 13f; // kIndentPerLevel - margin (probably)
        const int k_MinWideModeWidth = 230;
        const int k_WideModeInputFieldWidth = 212;

        static bool CanForceWideMode() => EditorGUIUtility.currentViewWidth > k_MinWideModeWidth;

        public static float GetPropertyHeight(SerializedProperty knot, SerializedProperty meta, GUIContent _)
        {
            // title
            float height = SplineGUIUtility.lineHeight;
            // position, rotation
            height += SplineGUIUtility.lineHeight * (CanForceWideMode() ? 2 : 4);

            return knot.isExpanded ? height : SplineGUIUtility.lineHeight;
        }

        public static bool OnGUI(Rect rect, SerializedProperty knot, SerializedProperty meta, GUIContent label)
        {
            bool wideMode = EditorGUIUtility.wideMode;

            if (!wideMode && CanForceWideMode())
            {
                EditorGUIUtility.wideMode = true;
                EditorGUIUtility.labelWidth = EditorGUIUtility.currentViewWidth - k_WideModeInputFieldWidth;
            }
            else
                EditorGUIUtility.labelWidth = 0;

            var titleRect = SplineGUIUtility.ReserveSpace(SplineGUIUtility.lineHeight, ref rect);
            titleRect.width = EditorGUIUtility.labelWidth;
            knot.isExpanded = EditorGUI.Foldout(titleRect, knot.isExpanded, label);
            var position = knot.FindPropertyRelative("Position");

            EditorGUI.BeginChangeCheck();
            if (knot.isExpanded)
            {
                var twistAngle = knot.FindPropertyRelative("TwistAngle");

                EditorGUI.PropertyField(SplineGUIUtility.ReserveSpaceForLine(ref rect), position, k_Position);
                EditorGUI.PropertyField(SplineGUIUtility.ReserveSpaceForLine(ref rect), twistAngle, k_TwistAngle);
            }
            // When in wide mode, show the position field inline with the knot title if not expanded
            else if (EditorGUIUtility.wideMode)
            {
                var inlinePositionRect = titleRect;
                inlinePositionRect.x += titleRect.width - k_IndentPad * EditorGUI.indentLevel;
                inlinePositionRect.width = rect.width - (titleRect.width - k_IndentPad * EditorGUI.indentLevel);
                EditorGUI.PropertyField(inlinePositionRect, position, GUIContent.none);
            }

            EditorGUIUtility.wideMode = wideMode;
            EditorGUIUtility.labelWidth = 0;
            return EditorGUI.EndChangeCheck();
        }
    }
}
