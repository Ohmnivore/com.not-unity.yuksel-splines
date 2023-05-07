using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.YukselSplines;
using Object = UnityEngine.Object;

namespace UnityEditor.YukselSplines
{
    static class TransformOperation
    {
        [Flags]
        public enum PivotFreeze
        {
            None = 0,
            Position = 1,
            Rotation = 2,
            All = Position | Rotation
        }

        struct TransformData
        {
            internal float3 position;

            internal static TransformData GetData(ISplineElement element)
            {
                var tData = new TransformData();
                tData.position = new float3(element.Position);

                return tData;
            }
        }

        static readonly List<ISplineElement> s_ElementSelection = new List<ISplineElement>(32);

        public static IReadOnlyList<ISplineElement> elementSelection => s_ElementSelection;

        static int s_ElementSelectionCount = 0;

        public static bool canManipulate => s_ElementSelectionCount > 0;

        public static ISplineElement currentElementSelected
            => canManipulate ? s_ElementSelection[0] : null;

        static Vector3 s_PivotPosition;
        public static Vector3 pivotPosition => s_PivotPosition;

        static quaternion s_HandleRotation;
        public static quaternion handleRotation => s_HandleRotation;

        //Caching rotation inverse for rotate and scale operations
        static quaternion s_HandleRotationInv;

        public static PivotFreeze pivotFreeze { get; set; }

        static TransformData[] s_MouseDownData;

        // Used to prevent same knot being rotated multiple times during a transform operation in Rotation Sync mode.
        static HashSet<SelectableKnot> s_RotatedKnotCache = new HashSet<SelectableKnot>();

        // Used to prevent the translation of the same knot multiple times if a linked knot was moved
        static HashSet<SelectableKnot> s_LinkedKnotCache = new HashSet<SelectableKnot>();

        static readonly List<SelectableKnot> s_KnotBuffer = new List<SelectableKnot>();

        internal static void UpdateSelection(IEnumerable<Object> selection)
        {
            SplineSelection.GetElements(EditorSplineUtility.GetSplinesFromTargetsInternal(selection), s_ElementSelection);
            s_ElementSelectionCount = s_ElementSelection.Count;
            if (s_ElementSelectionCount > 0)
            {
                UpdatePivotPosition();
                UpdateHandleRotation();
            }
        }

        internal static void UpdatePivotPosition(bool useKnotPositionForTangents = false)
        {
            if ((pivotFreeze & PivotFreeze.Position) != 0)
                return;

            switch (Tools.pivotMode)
            {
                case PivotMode.Center:
                    s_PivotPosition = EditorSplineUtility.GetElementBounds(s_ElementSelection, useKnotPositionForTangents).center;
                    break;

                case PivotMode.Pivot:
                    if (s_ElementSelectionCount == 0)
                        goto default;

                    var element = s_ElementSelection[0];
                    s_PivotPosition = element.Position;
                    break;

                default:
                    s_PivotPosition = Vector3.positiveInfinity;
                    break;
            }
        }

        // A way to set pivot position for situations, when by design, pivot position does
        // not necessarily match the pivot of selected elements.
        internal static void ForcePivotPosition(float3 position)
        {
            s_PivotPosition = position;
        }

        internal static void UpdateHandleRotation()
        {
            if ((pivotFreeze & PivotFreeze.Rotation) != 0)
                return;

            var handleRotation = Tools.handleRotation;
            if (canManipulate && (SplineTool.handleOrientation == HandleOrientation.Element || SplineTool.handleOrientation == HandleOrientation.Parent))
            {
                var curElement = TransformOperation.currentElementSelected;

                if (SplineTool.handleOrientation == HandleOrientation.Element)
                    handleRotation = EditorSplineUtility.GetElementRotation(curElement);
            }

            s_HandleRotation = handleRotation;
            s_HandleRotationInv = math.inverse(s_HandleRotation);
        }

        public static void ApplyTranslation(float3 delta)
        {
            s_RotatedKnotCache.Clear();
            s_LinkedKnotCache.Clear();

            foreach (var element in s_ElementSelection)
            {
                if (element is SelectableKnot knot)
                {
                    if (!s_LinkedKnotCache.Contains(knot))
                    {
                        knot.Position = ApplySmartRounding(knot.Position + delta);

                        EditorSplineUtility.GetKnotLinks(knot, s_KnotBuffer);
                        foreach (var k in s_KnotBuffer)
                            s_LinkedKnotCache.Add(k);
                    }
                }
            }
        }
        
        public static void ApplyRotation(Quaternion deltaRotation, float3 rotationCenter)
        {
            s_RotatedKnotCache.Clear();

            foreach (var element in s_ElementSelection)
            {
                if (element is SelectableKnot knot)
                {
                    var knotRotation = knot.Rotation;
                    RotateKnot(knot, deltaRotation, rotationCenter);
                }
            }
        }

        static void RotateKnot(SelectableKnot knot, quaternion deltaRotation, float3 rotationCenter, bool allowTranslation = true)
        {
            if (allowTranslation && Tools.pivotMode == PivotMode.Center)
            {
                var dir = knot.Position - rotationCenter;

                if (SplineTool.handleOrientation == HandleOrientation.Element || SplineTool.handleOrientation == HandleOrientation.Parent)
                    knot.Position = math.rotate(deltaRotation, dir) + rotationCenter;
                else
                    knot.Position = math.rotate(s_HandleRotation, math.rotate(deltaRotation, math.rotate(s_HandleRotationInv, dir))) + rotationCenter;
            }

            if (SplineTool.handleOrientation == HandleOrientation.Element || SplineTool.handleOrientation == HandleOrientation.Parent)
            {
                if (Tools.pivotMode == PivotMode.Center)
                    knot.Rotation = math.mul(deltaRotation, knot.Rotation);
                else
                {
                    var handlePivotModeRot = math.mul(GetCurrentSelectionKnot().Rotation, math.inverse(knot.Rotation));
                    knot.Rotation = math.mul(math.inverse(handlePivotModeRot), math.mul(deltaRotation, math.mul(handlePivotModeRot, knot.Rotation)));
                }
            }
            else
                knot.Rotation = math.mul(s_HandleRotation, math.mul(deltaRotation, math.mul(s_HandleRotationInv, knot.Rotation)));

            if (SplineTool.handleOrientation == HandleOrientation.Element)
            {
                RotateTwistAngle(knot, deltaRotation);
            }

            s_RotatedKnotCache.Add(knot);
        }

        static void RotateTwistAngle(SelectableKnot knot, quaternion deltaRotation)
        {
            var finalRotation = math.mul(deltaRotation, knot.Rotation);

            var axis = math.mul(knot.Rotation, math.forward());
            var v1 = math.mul(knot.Rotation, math.up());
            var v2 = math.mul(finalRotation, math.up());

            var deltaZ = Vector3.SignedAngle(v1, v2, axis);
            knot.TwistAngle += deltaZ;
        }

        static SelectableKnot GetCurrentSelectionKnot()
        {
            if (currentElementSelected == null)
                return default;

            if (currentElementSelected is SelectableKnot knot)
                return knot;

            return default;
        }

        public static void RecordMouseDownState()
        {
            s_MouseDownData = new TransformData[s_ElementSelectionCount];
            for (int i = 0; i < s_ElementSelectionCount; i++)
            {
                s_MouseDownData[i] = TransformData.GetData(s_ElementSelection[i]);
            }
        }

        public static void ClearMouseDownState()
        {
            s_MouseDownData = null;
        }

        public static Bounds GetSelectionBounds(bool useKnotPositionForTangents = false)
        {
            return EditorSplineUtility.GetElementBounds(s_ElementSelection, useKnotPositionForTangents);
        }

        public static float3 ApplySmartRounding(float3 position)
        {
            //If we are snapping, disable the smart rounding. If not the case, the transform will have the wrong snap value based on distance to screen.
#if UNITY_2022_2_OR_NEWER
            if (EditorSnapSettings.incrementalSnapActive || EditorSnapSettings.gridSnapActive)
                return position;
#endif

            float3 minDifference = SplineHandleUtility.GetMinDifference(position);
            for (int i = 0; i < 3; ++i)
                position[i] = Mathf.Approximately(position[i], 0f) ? position[i] : SplineHandleUtility.RoundBasedOnMinimumDifference(position[i], minDifference[i]);

            return position;
        }
    }
}