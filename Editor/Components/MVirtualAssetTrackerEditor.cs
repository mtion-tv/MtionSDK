using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using mtion.room.sdk.compiled;
using UnityEditor;

namespace mtion.room.sdk
{
    [CustomEditor(typeof(MVirtualAssetTracker))]
    public class MVirtualAssetTrackerEditor : Editor
    {
        private MVirtualAssetTracker instance_ = null;
        private bool mExtraConfiguration = false;

        private object sceneOverlayWindow;
        private MethodInfo showSceneViewOverlay;

        void OnEnable()
        {
            if (instance_ == null)
            {
                instance_ = target as MVirtualAssetTracker;
            }

            var unityEditor = Assembly.GetAssembly(typeof(UnityEditor.SceneView));
            var overlayWindowType = unityEditor.GetType("UnityEditor.OverlayWindow");
            var sceneViewOverlayType = unityEditor.GetType("UnityEditor.SceneViewOverlay");

            var windowFuncType = sceneViewOverlayType.GetNestedType("WindowFunction");
            var windowFunc = Delegate.CreateDelegate(windowFuncType, this.GetType().GetMethod(nameof(DoOverlayUI), BindingFlags.Static | BindingFlags.NonPublic));
            var windowDisplayOptionType = sceneViewOverlayType.GetNestedType("WindowDisplayOption");

            sceneOverlayWindow = Activator.CreateInstance(
                overlayWindowType,
                EditorGUIUtility.TrTextContent("3D Asset", (string)null, (Texture)null), // Title
                windowFunc, // Draw function of the window
                int.MaxValue, // Priority of the window
                (UnityEngine.Object)instance_, // Unity Obect that will be passed to the drawing function
                Enum.Parse(windowDisplayOptionType, "OneWindowPerTarget") //SceneViewOverlay.WindowDisplayOption.OneWindowPerTarget
            );

            showSceneViewOverlay = sceneViewOverlayType.GetMethod("ShowWindow", BindingFlags.Static | BindingFlags.Public);

        }

        private static void DoOverlayUI(UnityEngine.Object target, SceneView sceneView)
        {
            MVirtualAssetTracker asset = (MVirtualAssetTracker)target;
            if (GUILayout.Button("Asset Type: " + asset.GetAssetType().ToString()))
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
            GUI.enabled = !Application.isPlaying;

            instance_.AssetParams.VirtualObjectType = (VirtualObjectComponentType)EditorGUILayout.EnumPopup("Asset Type", instance_.AssetParams.VirtualObjectType);

            GUI.enabled = false;

            instance_.InternalID = EditorGUILayout.TextField("Asset GUID", instance_.InternalID);

            instance_.ObjectType = (MTIONObjectType)EditorGUILayout.EnumPopup("Object Type", instance_.ObjectType);

            GUI.enabled = true;
        }
    }
}
