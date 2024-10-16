using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace mtion.room.sdk
{
    public class TrackerEditorCamera
    {
        static public GameObject cameraObject = null;
        static public Camera VizCamera = null;
        static public Camera sceneViewCamera = null;
        static public RenderTexture VizTexture = null;
        static public float defaultTextureHeight = 200.0f;

        static public bool TriggerSelection = false;
    }


    [CustomEditor(typeof(MVirtualCameraEventTracker))]
    class MVirtualCameraEventTrackerEditor : Editor
    {
        public MVirtualCameraEventTracker instance_ = null;
        private bool mExtraConfiguration = false;



        private object sceneOverlayWindow;
        private MethodInfo showSceneViewOverlay;

        void OnEnable()
        {

            if (instance_ == null)
            {
                instance_ = target as MVirtualCameraEventTracker;

            }

            if (TrackerEditorCamera.cameraObject == null)
            {
                TrackerEditorCamera.cameraObject = new GameObject("[VAPOR] DO NOT EDIT");
                TrackerEditorCamera.cameraObject.transform.parent = instance_.transform;
                TrackerEditorCamera.cameraObject.transform.localPosition = Vector3.zero;
                TrackerEditorCamera.cameraObject.transform.localRotation = Quaternion.identity;
                TrackerEditorCamera.cameraObject.hideFlags = HideFlags.HideAndDontSave;

                TrackerEditorCamera.VizCamera = TrackerEditorCamera.cameraObject.AddComponent<Camera>();

                TrackerEditorCamera.VizTexture = new RenderTexture((int)(TrackerEditorCamera.defaultTextureHeight * instance_.CameraParams.AspectRatio), (int)TrackerEditorCamera.defaultTextureHeight, 0);
                TrackerEditorCamera.VizTexture.name = "mtion.room.sdk.MVirtualCameraEventTrackerEditor.VizTexture";
                TrackerEditorCamera.VizCamera.targetTexture = TrackerEditorCamera.VizTexture;
            }

            if (TrackerEditorCamera.sceneViewCamera == null)
            {
                TrackerEditorCamera.sceneViewCamera = EditorWindow.GetWindow<SceneView>().camera;
            }


            var unityEditor = Assembly.GetAssembly(typeof(UnityEditor.SceneView));
            var overlayWindowType = unityEditor.GetType("UnityEditor.OverlayWindow");
            var sceneViewOverlayType = unityEditor.GetType("UnityEditor.SceneViewOverlay");

            var windowFuncType = sceneViewOverlayType.GetNestedType("WindowFunction");
            var windowFunc = Delegate.CreateDelegate(windowFuncType, this.GetType().GetMethod(nameof(DoOverlayUI), BindingFlags.Static | BindingFlags.NonPublic));
            var windowDisplayOptionType = sceneViewOverlayType.GetNestedType("WindowDisplayOption");

            sceneOverlayWindow = Activator.CreateInstance(
                overlayWindowType,
                EditorGUIUtility.TrTextContent("Camera", (string)null, (Texture)null), // Title
                windowFunc, // Draw function of the window
                int.MaxValue, // Priority of the window
                (UnityEngine.Object)instance_, // Unity Obect that will be passed to the drawing function
                Enum.Parse(windowDisplayOptionType, "OneWindowPerTarget") //SceneViewOverlay.WindowDisplayOption.OneWindowPerTarget
            );

            showSceneViewOverlay = sceneViewOverlayType.GetMethod("ShowWindow", BindingFlags.Static | BindingFlags.Public);

        }
        private static void DoOverlayUI(UnityEngine.Object target, SceneView sceneView)
        {
            MVirtualCameraEventTracker tracker = (MVirtualCameraEventTracker)target;

            if (TrackerEditorCamera.VizCamera && TrackerEditorCamera.sceneViewCamera)
            {
                TrackerEditorCamera.VizCamera.fieldOfView = tracker.CameraParams.VerticalFoV;
                TrackerEditorCamera.VizCamera.aspect = tracker.CameraParams.AspectRatio;
                TrackerEditorCamera.VizCamera.nearClipPlane = tracker.CameraParams.NearPlane;
                TrackerEditorCamera.VizCamera.farClipPlane = tracker.CameraParams.FarPlane;
                TrackerEditorCamera.VizCamera.clearFlags = tracker.CameraParams.ClearFlags;

                float aspect = TrackerEditorCamera.VizTexture.width / (float)TrackerEditorCamera.VizTexture.height;
                if (Math.Abs(aspect - tracker.CameraParams.AspectRatio) > 0.01f)
                {
                    TrackerEditorCamera.VizTexture.Release();
                    TrackerEditorCamera.VizTexture = new RenderTexture(
                        (int)(TrackerEditorCamera.defaultTextureHeight * tracker.CameraParams.AspectRatio),
                        (int)TrackerEditorCamera.defaultTextureHeight, 0);
                    TrackerEditorCamera.VizTexture.name = "mtion.room.sdk.MVirtualCameraEventTrackerEditor.VizTexture";
                    TrackerEditorCamera.VizCamera.targetTexture = TrackerEditorCamera.VizTexture;
                    TrackerEditorCamera.VizCamera.Render();
                }

                if (GUILayout.Button(TrackerEditorCamera.VizTexture))
                {
                    TrackerEditorCamera.TriggerSelection = true;
                }
            }
        }


        private void OnDisable()
        {
            if (TrackerEditorCamera.cameraObject != null)
            {
                DestroyImmediate(TrackerEditorCamera.cameraObject);
            }
        }




        public void OnSceneGUI()
        {
            showSceneViewOverlay.Invoke(null, new object[] { sceneOverlayWindow });

            if (TrackerEditorCamera.TriggerSelection)
            {
                TrackerEditorCamera.TriggerSelection = false;
                SceneView.FrameLastActiveSceneView();
            }
        }




        public override void OnInspectorGUI()
        {

            GUI.enabled = !Application.isPlaying;

            instance_.CameraParams.VerticalFoV = (int)EditorGUILayout.IntSlider(
                new GUIContent("Field of View", "The virtual cameras field of view"),
                (int)instance_.CameraParams.VerticalFoV,
                10,
                120);

            instance_.CameraParams.NearPlane = EditorGUILayout.FloatField("Near Plane", instance_.CameraParams.NearPlane);
            instance_.CameraParams.FarPlane = EditorGUILayout.FloatField("Far Plane", instance_.CameraParams.FarPlane);

            instance_.CameraParams.AspectRatio = EditorGUILayout.Slider(
                new GUIContent("Aspect Ratio", "The virtual cameras aspect ratio"),
                instance_.CameraParams.AspectRatio,
                0.5f,
                4.0f);


            instance_.CameraParams.ClearFlags = (CameraClearFlags)EditorGUILayout.EnumPopup("Clear Flags", instance_.CameraParams.ClearFlags);




            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);


            mExtraConfiguration = EditorGUILayout.Foldout(mExtraConfiguration, "Raw Configuration Details");
            EditorGUI.indentLevel++;
            using (var extraConfigGroup = new EditorGUILayout.FadeGroupScope(Convert.ToSingle(mExtraConfiguration)))
            {
                if (extraConfigGroup.visible)
                {
                    DrawDefaultInspector();
                }
            }
            EditorGUI.indentLevel--;


            if (GUI.changed && !Application.isPlaying)
            {
                EditorUtility.SetDirty(instance_);
                EditorSceneManager.MarkSceneDirty(instance_.gameObject.scene);
            }


        }

    }
}
