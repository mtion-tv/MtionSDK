using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets;
using System;
using mtion.room.sdk.compiled;



namespace mtion.room.sdk
{
    public class BuildManager
    {

        public static void BuildApplicationAssetBundlesCLI()
        {
            BuildManager.Instance.BuildApplicationAssetBundle();
        }
        public static void BuildScene()
        {
            BuildManager.Instance.BuildAndExportSdkScene();
        }


        public static bool IsSceneValid()
        {
            return BuildManager.Instance.SceneContainsValidSDK;
        }

        public static bool IsExportTaskRunning()
        {
            return BuildManager.Instance.ExportTaskRunning;
        }

        public static float GetExportTaskPrecentageComplete()
        {
            return BuildManager.Instance.ExportPercentComplete;
        }

        public static void SetExportTaskState(bool state)
        {
            BuildManager.Instance.ExportTaskRunning = state;
        }

        public static GameObject GetSceneDescriptor()
        {
            return BuildManager.Instance.SceneDescriptorObject;
        }



        public static bool SERVER_HTTPS = true;
        public static string SERVER_KEY = null;
        public static string SERVER_ADDRESS = null;
        public static int SERVER_PORT = 0;
        

        private static readonly Lazy<BuildManager> lazy = new Lazy<BuildManager>(() => new BuildManager());
        public static BuildManager Instance
        {
            get
            {
                return lazy.Value;
            }
        }


        public bool SceneContainsValidSDK { get => GetMTIONSDKDescriptor() != null; }
        public GameObject SceneDescriptorObject { get => GetMTIONSDKDescriptor(); }
        public bool ExportTaskRunning { get => exportTaskRunning; set => exportTaskRunning = value; }
        public float ExportPercentComplete 
        { 
            get
            {
                if (sceneExporter == null)
                {
                    return 0f;
                }

                return sceneExporter.ExportPercentComplete;
            } 
        }


        private SceneExporter sceneExporter;
        private bool exportTaskRunning = false;


        private void BuildApplicationAssetBundle()
        {
            var builder = AddressableAssetSettingsDefaultObject.Settings.ActivePlayerDataBuilder;
            AddressableAssetSettings.CleanPlayerContent(builder);
            AddressableAssetSettings.BuildPlayerContent();
        }

        private void BuildAndExportSdkScene()
        {
            var sdkDescriptor = GetMTIONSDKDescriptor();
            if (sdkDescriptor == null)
            {
                return;
            }

            var sceneObjectDescriptor = sdkDescriptor.GetComponent<MTIONSDKDescriptorSceneBase>();
            var roomObject = sceneObjectDescriptor.ObjectReferenceProp;

            if (roomObject.transform.position == Vector3.zero &&
                roomObject.transform.rotation == Quaternion.identity &&
                roomObject.transform.localScale == Vector3.one)
            {
                exportTaskRunning = true;
                sceneExporter = new SceneExporter(sceneObjectDescriptor);
                sceneExporter.ExportSDKScene();
                sceneExporter = null;
                exportTaskRunning = false;
            }
            else
            {
                EditorUtility.DisplayDialog("Error", $"Ensure that the root transform of {roomObject.name} is zero.", "ok");
            }
        }

        private GameObject GetMTIONSDKDescriptor()
        {
            var currentScene = EditorSceneManager.GetActiveScene();
            List<GameObject> rootObjects = new List<GameObject>();
            currentScene.GetRootGameObjects(rootObjects);

            var descriptors = new List<GameObject>();
            for (int i = 0; i < rootObjects.Count; ++i)
            {
                var go = rootObjects[i];
                var descriptor = go.GetComponent<MTIONSDKDescriptorSceneBase>();

                if (descriptor == null)
                {
                    continue;
                }

                if (descriptor.ObjectType == MTIONObjectType.MTIONSDK_BLUEPRINT)
                {
                    return go;
                }

                descriptors.Add(go);
            }

            return descriptors.Count == 0 ? null : descriptors[0];
        }
    }
}
