using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets;
using System;
using mtion.room.sdk.compiled;

// NOTE: See https://github.dev/kuler90/build-unity/blob/master/src/build.js for details around setting up expanded build settings


namespace mtion.room.sdk
{
    public class BuildManager
    {
        ////////////////////////////////////////////////////////////////////////////////////////
        ///  PUBLIC CLI BUILDSYSTEM
        ////////////////////////////////////////////////////////////////////////////////////////

        public static void BuildApplicationAssetBundlesCLI()
        {
            BuildManager.Instance.BuildApplicationAssetBundle();
        }
        public static void BuildScene()
        {
            BuildManager.Instance.BuildAndExportSdkScene();
        }


        ////////////////////////////////////////////////////////////////////////////////////////
        ///  PUBLIC STATIC API
        ////////////////////////////////////////////////////////////////////////////////////////
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


        // SDK USER AUTH

        public static bool SERVER_HTTPS = true;
        public static string SERVER_KEY = null;
        public static string SERVER_ADDRESS = null;
        public static int SERVER_PORT = 0;

        public static UserSdkAuthentication GetUserAuthentication()
        {
            return BuildManager.Instance.UserAuthentication;
        }



        ////////////////////////////////////////////////////////////////////////////////////////
        /// SINGLETON ACCESS 
        ////////////////////////////////////////////////////////////////////////////////////////
        private static readonly Lazy<BuildManager> lazy = new Lazy<BuildManager>(() => new BuildManager());
        public static BuildManager Instance
        {
            get
            {
                return lazy.Value;
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////
        ///  PROPS
        ////////////////////////////////////////////////////////////////////////////////////////

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
        public UserSdkAuthentication UserAuthentication
        {
            get
            {
                if (userAuthentication == null)
                {
                    if (!string.IsNullOrEmpty(SERVER_KEY) && 
                        !string.IsNullOrEmpty(SERVER_ADDRESS) && 
                        SERVER_PORT > 0)
                    {
                        userAuthentication = new UserSdkAuthentication(SERVER_HTTPS, SERVER_KEY, SERVER_ADDRESS, SERVER_PORT);
                    }
                    else
                    {
                        userAuthentication = new UserSdkAuthentication();
                        userAuthentication.RefreshCredentials();
                    }
                }

                return userAuthentication;
            }

            set => userAuthentication = value;
        }


        ////////////////////////////////////////////////////////////////////////////////////////
        ///  VARS
        ////////////////////////////////////////////////////////////////////////////////////////

        private SceneExporter sceneExporter;
        private bool exportTaskRunning = false;

        ////////////////////////////////////////////////////////////////////
        /// USER AUTH
        /// TODO: https://docs.aws.amazon.com/AWSJavaScriptSDK/latest/AWS/CognitoIdentity.html#getId-property
        ////////////////////////////////////////////////////////////////////
        private UserSdkAuthentication userAuthentication = null;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// PRIVATE API
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private void BuildApplicationAssetBundle()
        {
            var builder = AddressableAssetSettingsDefaultObject.Settings.ActivePlayerDataBuilder;
            AddressableAssetSettings.CleanPlayerContent(builder);
            AddressableAssetSettings.BuildPlayerContent();
        }

        private void BuildAndExportSdkScene()
        {
            // Check for valid scene descriptor
            var sdkDescriptor = GetMTIONSDKDescriptor();
            if (sdkDescriptor == null)
            {
                return;
            }

            var sceneObjectDescriptor = sdkDescriptor.GetComponent<MTIONSDKDescriptorSceneBase>();
            var roomObject = sceneObjectDescriptor.ObjectReferenceProp;

            // Export room and generate Prefab and glb
            if (roomObject.transform.position == Vector3.zero &&
                roomObject.transform.rotation == Quaternion.identity &&
                roomObject.transform.localScale == Vector3.one)
            {
                exportTaskRunning = true;
                sceneExporter = new SceneExporter(sceneObjectDescriptor, UserAuthentication);
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

            for (int i = 0; i < rootObjects.Count; ++i)
            {
                var go = rootObjects[i];
                if (go.GetComponent<MTIONSDKDescriptorSceneBase>())
                {
                    return go;
                }
            }

            return null;
        }
    }
}
