using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using mtion.room.sdk.compiled;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace mtion.room.sdk
{
    [CustomEditor(typeof(MVirtualLightingTracker))]
    public class MVirtualLightingTrackerEditor : Editor
    {
      private MVirtualLightingTracker instance_ = null;
        private bool mExtraConfiguration = false;

        private object sceneOverlayWindow;
        private MethodInfo showSceneViewOverlay;

          void OnEnable()
        {

            if (instance_ == null)
            {
                instance_ = target as MVirtualLightingTracker;
            }


            var unityEditor = Assembly.GetAssembly(typeof(UnityEditor.SceneView));
            var overlayWindowType = unityEditor.GetType("UnityEditor.OverlayWindow");
            var sceneViewOverlayType = unityEditor.GetType("UnityEditor.SceneViewOverlay");

            var windowFuncType = sceneViewOverlayType.GetNestedType("WindowFunction");
            var windowFunc = Delegate.CreateDelegate(windowFuncType, this.GetType().GetMethod(nameof(DoOverlayUI), BindingFlags.Static | BindingFlags.NonPublic));
            var windowDisplayOptionType = sceneViewOverlayType.GetNestedType("WindowDisplayOption");

            sceneOverlayWindow = Activator.CreateInstance(
                overlayWindowType,
                EditorGUIUtility.TrTextContent("Light Component", (string)null, (Texture)null), // Title
                windowFunc, // Draw function of the window
                int.MaxValue, // Priority of the window
                (UnityEngine.Object)instance_, // Unity Obect that will be passed to the drawing function
                Enum.Parse(windowDisplayOptionType, "OneWindowPerTarget") //SceneViewOverlay.WindowDisplayOption.OneWindowPerTarget
            );

            showSceneViewOverlay = sceneViewOverlayType.GetMethod("ShowWindow", BindingFlags.Static | BindingFlags.Public);

        }

        private static void DoOverlayUI(UnityEngine.Object target, SceneView sceneView)
        {
            MVirtualLightingTracker asset = (MVirtualLightingTracker)target;
            if (GUILayout.Button("Light: " + asset.GetType().ToString()))
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

            instance_.LightParams.LightColor = (Vector4) EditorGUILayout.ColorField("Color", instance_.LightParams.LightColor);

            instance_.LightParams.LightIntensity = EditorGUILayout.FloatField("Intensity", instance_.LightParams.LightIntensity);
            instance_.LightParams.LightIntensity = Mathf.Clamp(instance_.LightParams.LightIntensity, 0, 100);

            instance_.LightParams.LightType = (LightingComponentType)EditorGUILayout.EnumPopup("Light Type", instance_.LightParams.LightType);

            GUI.enabled = true;

            if (instance_.gameObject.GetComponent<Light>() == null)
            {
                var tempLight = instance_.gameObject.AddComponent<Light>();
                tempLight.lightmapBakeType = LightmapBakeType.Realtime;
                tempLight.shadows = LightShadows.Soft;
                tempLight.hideFlags = HideFlags.DontSave;
            }

            var lightComponent = instance_.gameObject.GetComponent<Light>();
            
            LightType unityType = instance_.LightParams.LightType == LightingComponentType.POINT_LIGHT ? LightType.Point : LightType.Spot;
            if (lightComponent.type != unityType)
            {
                lightComponent.type = unityType;
            }

            if (lightComponent.intensity != instance_.LightParams.LightIntensity)
            {
                lightComponent.intensity = instance_.LightParams.LightIntensity;
            }

            if (((Vector4) lightComponent.color) != instance_.LightParams.LightColor)
            {
                lightComponent.color = instance_.LightParams.LightColor;
            }

            if (GUI.changed && !Application.isPlaying)
            {
                EditorUtility.SetDirty(instance_);
                EditorSceneManager.MarkSceneDirty(instance_.gameObject.scene);
            }
        }
    }
}
