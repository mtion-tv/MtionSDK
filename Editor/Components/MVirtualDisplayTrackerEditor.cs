using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using mtion.room.sdk.compiled;

namespace mtion.room.sdk
{
    public class UniversalDisplayGUIOptions
    {
        public static bool FocusOnDisplay = false;
    }

    [CustomEditor(typeof(MVirtualDisplayTracker))]
    public class MVirtualDisplayTrackerEditor : Editor
    {
        public static List<string> displayGizmoOptions = new List<string>() {
            "Show All",
            "Horizontal 16:9",
            "Horizontal 4:3",
            "Virtical 16:9",
            "Virtical 4:3"
        };

        private MVirtualDisplayTracker instance_ = null;
        private bool mExtraConfiguration = false;

        private object sceneOverlayWindow;
        private MethodInfo showSceneViewOverlay;


        void OnEnable()
        {

            if (instance_ == null)
            {
                instance_ = target as MVirtualDisplayTracker;
            }


            var unityEditor = Assembly.GetAssembly(typeof(UnityEditor.SceneView));
            var overlayWindowType = unityEditor.GetType("UnityEditor.OverlayWindow");
            var sceneViewOverlayType = unityEditor.GetType("UnityEditor.SceneViewOverlay");

            var windowFuncType = sceneViewOverlayType.GetNestedType("WindowFunction");
            var windowFunc = Delegate.CreateDelegate(windowFuncType, this.GetType().GetMethod(nameof(DoOverlayUI), BindingFlags.Static | BindingFlags.NonPublic));
            var windowDisplayOptionType = sceneViewOverlayType.GetNestedType("WindowDisplayOption");

            sceneOverlayWindow = Activator.CreateInstance(
                overlayWindowType,
                EditorGUIUtility.TrTextContent("Display", (string)null, (Texture)null), // Title
                windowFunc, // Draw function of the window
                int.MaxValue, // Priority of the window
                (UnityEngine.Object)instance_, // Unity Obect that will be passed to the drawing function
                Enum.Parse(windowDisplayOptionType, "OneWindowPerTarget") //SceneViewOverlay.WindowDisplayOption.OneWindowPerTarget
            );

            showSceneViewOverlay = sceneViewOverlayType.GetMethod("ShowWindow", BindingFlags.Static | BindingFlags.Public);

        }

        private static void DoOverlayUI(UnityEngine.Object target, SceneView sceneView)
        {
            MVirtualDisplayTracker display = (MVirtualDisplayTracker)target;
            if (GUILayout.Button("Display Type: " + display.GetDisplayType().ToString()))
            {
                UniversalDisplayGUIOptions.FocusOnDisplay = true;
            }
        }

        public void OnSceneGUI()
        {
            if (instance_ == null) return;
            showSceneViewOverlay.Invoke(null, new object[] { sceneOverlayWindow });

            if (UniversalDisplayGUIOptions.FocusOnDisplay)
            {
                UniversalDisplayGUIOptions.FocusOnDisplay = false;
                SceneView.FrameLastActiveSceneView();
            }
        }

        public override void OnInspectorGUI()
        {

            GUI.enabled = false;

            GUI.enabled = !Application.isPlaying;

            instance_.DisplayParams.DisplayType = (DisplayComponentType)EditorGUILayout.EnumPopup("Display Type", instance_.DisplayParams.DisplayType);

            GUI.enabled = true;

            instance_.gizmoDisplaySelection = EditorGUILayout.Popup("Gizmo Display:", instance_.gizmoDisplaySelection, displayGizmoOptions.ToArray());
        }
    }
}
