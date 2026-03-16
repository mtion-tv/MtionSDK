using System;
using System.Collections.Generic;
using mtion.room.sdk.compiled;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;



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

        public static string GetSceneDescriptorValidationError()
        {
            return BuildManager.Instance.GetSceneDescriptorValidationErrorInternal();
        }

        public static SDKExportReport GetLastExportReport()
        {
            return BuildManager.Instance.lastExportReport;
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


        public bool SceneContainsValidSDK { get => GetSceneDescriptorCandidatesInternal().Count > 0; }
        public GameObject SceneDescriptorObject { get => GetPreferredSceneDescriptorInternal(); }
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
        private SDKExportReport lastExportReport;
        private Scene exportStartingScene;


        private void BuildApplicationAssetBundle()
        {
            AssetExporter.EnsureProjectReadyForJsonCatalogExport();

            var builder = AddressableAssetSettingsDefaultObject.Settings.ActivePlayerDataBuilder;
            AddressableAssetSettings.CleanPlayerContent(builder);
            AddressableAssetSettings.BuildPlayerContent();
        }

        private void BuildAndExportSdkScene()
        {
            string validationError = GetSceneDescriptorValidationErrorInternal();
            if (!string.IsNullOrEmpty(validationError))
            {
                EditorUtility.DisplayDialog("Error", validationError, "ok");
                return;
            }

            GameObject sdkDescriptor = GetPreferredSceneDescriptorInternal();
            if (sdkDescriptor == null)
            {
                return;
            }

            var sceneObjectDescriptor = sdkDescriptor.GetComponent<MTIONSDKDescriptorSceneBase>();
            if (sceneObjectDescriptor == null || sceneObjectDescriptor.ObjectReference == null)
            {
                EditorUtility.DisplayDialog("Error", "Scene descriptor is missing an object reference.", "ok");
                return;
            }

            var roomObject = sceneObjectDescriptor.ObjectReference;

            if (roomObject.transform.position == Vector3.zero &&
                roomObject.transform.rotation == Quaternion.identity &&
                roomObject.transform.localScale == Vector3.one)
            {
                try
                {
                    AssetExporter.EnsureProjectReadyForJsonCatalogExport();
                }
                catch (AssetExporter.ExportRequiresRecompileException ex)
                {
                    EditorUtility.DisplayDialog("SDK Export Recompile Required", ex.Message, "Close");
                    return;
                }

                exportStartingScene = EditorSceneManager.GetActiveScene();
                exportTaskRunning = true;
                sceneExporter = new SceneExporter(sceneObjectDescriptor);
                sceneExporter.ExportFinished += OnExportFinished;

                try
                {
                    sceneExporter.ExportSDKScene();
                }
                catch (Exception ex)
                {
                    if (sceneExporter != null && !sceneExporter.IsCompleted)
                    {
                        sceneExporter.FailReport(ex);

                        if (ex is AssetExporter.ExportRequiresRecompileException)
                        {
                            EditorUtility.DisplayDialog("SDK Export Recompile Required", ex.Message, "Close");
                        }
                        else
                        {
                            Debug.LogException(ex);
                            EditorUtility.DisplayDialog("SDK Export Failed", ex.Message, "Close");
                        }
                    }
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Error", $"Ensure that the root transform of {roomObject.name} is zero.", "ok");
            }
        }

        private void OnExportFinished(SceneExporter exporter, Exception ex)
        {
            lastExportReport = exporter != null ? exporter.ExportReport : null;

            if (ex != null)
            {
                if (ex is AssetExporter.ExportRequiresRecompileException)
                {
                    EditorUtility.DisplayDialog("SDK Export Recompile Required", ex.Message, "Close");
                }
                else
                {
                    Debug.LogException(ex);
                    EditorUtility.DisplayDialog("SDK Export Failed", ex.Message, "Close");
                }
            }

            if (exportStartingScene.IsValid() && exportStartingScene.isLoaded)
            {
                EditorSceneManager.SetActiveScene(exportStartingScene);
            }

            try
            {
                exporter?.PersistReport();
            }
            catch (Exception persistException)
            {
                Debug.LogException(persistException);
            }
            finally
            {
                EditorUtility.ClearProgressBar();

                if (sceneExporter != null)
                {
                    sceneExporter.ExportFinished -= OnExportFinished;
                }

                sceneExporter = null;
                exportTaskRunning = false;
                MTIONSDKToolsBuildTab.Invalidate();
            }
        }

        private string GetSceneDescriptorValidationErrorInternal()
        {
            List<GameObject> descriptors = GetSceneDescriptorCandidatesInternal();
            if (descriptors.Count == 0)
            {
                return null;
            }

            List<MTIONSDKDescriptorSceneBase> descriptorComponents = new List<MTIONSDKDescriptorSceneBase>();
            foreach (GameObject descriptorObject in descriptors)
            {
                descriptorComponents.Add(descriptorObject.GetComponent<MTIONSDKDescriptorSceneBase>());
            }

            int blueprintCount = descriptorComponents.FindAll(descriptor => descriptor.ObjectType == MTIONObjectType.MTIONSDK_BLUEPRINT).Count;
            if (blueprintCount > 1)
            {
                return "Multiple blueprint descriptors were found in the active scene. Keep only one blueprint descriptor root.";
            }

            if (blueprintCount == 1)
            {
                List<string> invalidBlueprintSceneDescriptors = new List<string>();
                foreach (MTIONSDKDescriptorSceneBase descriptor in descriptorComponents)
                {
                    if (descriptor.ObjectType == MTIONObjectType.MTIONSDK_BLUEPRINT ||
                        descriptor.ObjectType == MTIONObjectType.MTIONSDK_ROOM)
                    {
                        continue;
                    }

                    invalidBlueprintSceneDescriptors.Add(descriptor.ObjectType.ToString());
                }

                if (invalidBlueprintSceneDescriptors.Count > 0)
                {
                    return $"Blueprint scenes may only contain a blueprint descriptor and its room descriptor. Found: {string.Join(", ", invalidBlueprintSceneDescriptors)}.";
                }

                return null;
            }

            if (descriptorComponents.Count > 1)
            {
                List<string> descriptorTypes = new List<string>();
                foreach (MTIONSDKDescriptorSceneBase descriptor in descriptorComponents)
                {
                    descriptorTypes.Add(descriptor.ObjectType.ToString());
                }

                return $"Multiple SDK descriptors were found in the active scene. Keep only one descriptor root. Found: {string.Join(", ", descriptorTypes)}.";
            }

            return null;
        }

        private GameObject GetPreferredSceneDescriptorInternal()
        {
            List<GameObject> descriptors = GetSceneDescriptorCandidatesInternal();
            GameObject fallback = null;

            foreach (GameObject descriptorObject in descriptors)
            {
                MTIONSDKDescriptorSceneBase descriptor = descriptorObject.GetComponent<MTIONSDKDescriptorSceneBase>();
                if (descriptor == null)
                {
                    continue;
                }

                if (descriptor.ObjectType == MTIONObjectType.MTIONSDK_BLUEPRINT)
                {
                    return descriptorObject;
                }

                if (fallback == null)
                {
                    fallback = descriptorObject;
                }
            }

            return fallback;
        }

        private List<GameObject> GetSceneDescriptorCandidatesInternal()
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

                descriptors.Add(go);
            }

            return descriptors;
        }
    }
}
