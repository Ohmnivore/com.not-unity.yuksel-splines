using Unity.Mathematics;
using UnityEngine.YukselSplines;
using UnityEngine;
using UnityEditor.EditorTools;

namespace UnityEditor.YukselSplines
{
    static class SplineMenu
    {
        const string k_MenuPath = "GameObject/Yuksel Spline";

        internal static GameObject CreateSplineGameObject(MenuCommand menuCommand, Spline spline = null)
        {
            var name = GameObjectUtility.GetUniqueNameForSibling(null, "Spline");
            var gameObject = ObjectFactory.CreateGameObject(name, typeof(SplineContainer));

#if UNITY_2022_1_OR_NEWER
            ObjectFactory.PlaceGameObject(gameObject, menuCommand.context as GameObject);
#else
            if (menuCommand.context is GameObject go)
            {
                Undo.RecordObject(gameObject.transform, "Re-parenting");
                gameObject.transform.SetParent(go.transform);
            }
#endif

            if (spline != null)
            {
                var container = gameObject.GetComponent<SplineContainer>();
                container.Spline = spline;
            }
            
            Selection.activeGameObject = gameObject;
            return gameObject;
        }

        const int k_MenuPriority = 10;

        [MenuItem(k_MenuPath + "/Draw Splines Tool...", false, k_MenuPriority + 0)]
        static void CreateNewSpline(MenuCommand menuCommand)
        {
            var gameObject = CreateSplineGameObject(menuCommand);

            gameObject.transform.localPosition = Vector3.zero;
            gameObject.transform.localRotation = Quaternion.identity;

            Selection.activeObject = gameObject;
            ActiveEditorTracker.sharedTracker.RebuildIfNecessary();
            //Ensuring trackers are rebuilt before changing to SplineContext
            EditorApplication.delayCall += SetKnotPlacementTool;
        }

        static void SetKnotPlacementTool()
        {
            ToolManager.SetActiveContext<SplineToolContext>();
            ToolManager.SetActiveTool<KnotPlacementTool>();
        }
    }
}
