using mtion.room.sdk.compiled;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace mtion.room.sdk
{
    public static class SDKMigrationHandler
    {
        public static void TryMigrate()
        {
            foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                var sdkRoom = go.GetComponentInChildren<MTIONSDKRoom>();
                if (sdkRoom != null)
                {
                    MigrateRoomToBlueprint(sdkRoom);
                    break;
                }
            }
        }

        private static void MigrateRoomToBlueprint(MTIONSDKRoom sdkRoom)
        {
            foreach (var go in sdkRoom.gameObject.scene.GetRootGameObjects())
            {
                var blueprint = go.GetComponentInChildren<MTIONSDKBlueprint>();
                if (blueprint != null)
                {
                    return;
                }
            }

            var roomScene = sdkRoom.gameObject.scene;
            var roomScenePath = roomScene.path;
            var roomSceneName = roomScene.name;
            var environmentSceneName = "";

            if (!string.IsNullOrEmpty(sdkRoom.EnvironmentInternalID))
            {
                var sceneAssets = AssetDatabase.FindAssets("t:Scene", new string[]
                {
                    Application.dataPath,
                });

                sceneAssets = AssetDatabase.FindAssets("t:Scene", new string[]
                {
                    "Assets",
                });
                foreach (var assetGuids in sceneAssets)
                {
                    var scenePath = AssetDatabase.GUIDToAssetPath(assetGuids);
                    var scene = EditorSceneManager.OpenScene(scenePath);
                    foreach (var go in scene.GetRootGameObjects())
                    {
                        var environment = go.GetComponentInChildren<MTIONSDKEnvironment>();
                        if (environment != null && environment.InternalID == sdkRoom.EnvironmentInternalID)
                        {
                            environmentSceneName = scene.name;
                            break;
                        }
                    }
                    EditorSceneManager.CloseScene(scene, true);

                    if (!string.IsNullOrEmpty(environmentSceneName))
                    {
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(environmentSceneName))
            {
                var environmentScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                environmentScene.name = roomSceneName + "_Environment";
                environmentSceneName = environmentScene.name;

                SDKEditorUtil.CreateEnvironmentScene();
                var roomSceneFolderPath = Path.GetDirectoryName(roomScenePath);
                EditorSceneManager.SaveScene(environmentScene, $"{roomSceneFolderPath}/{environmentScene.name}.unity");

                sdkRoom.EnvironmentInternalID = GameObject.FindFirstObjectByType<MTIONSDKEnvironment>()?.InternalID;
            }


            if (!roomScene.isLoaded)
            {
                roomScene = EditorSceneManager.OpenScene(roomScenePath, OpenSceneMode.Additive);
            }

            EditorSceneManager.SetActiveScene(roomScene);
            sdkRoom = GameObject.FindFirstObjectByType<MTIONSDKRoom>();

            GameObject blueprintDescriptorObject = new GameObject("MTIONBlueprintDescriptor");
            blueprintDescriptorObject.transform.position = Vector3.zero;
            blueprintDescriptorObject.transform.rotation = Quaternion.identity;
            var sdkBlueprint = blueprintDescriptorObject.AddComponent<MTIONSDKBlueprint>();

            SDKEditorUtil.InitAddressableAssetFields(sdkBlueprint, MTIONObjectType.MTIONSDK_BLUEPRINT, roomSceneName);
            sdkBlueprint.RoomSceneName = roomSceneName;
            sdkBlueprint.EnvironmentSceneName = environmentSceneName;

            sdkRoom = GameObject.FindFirstObjectByType<MTIONSDKRoom>();
            sdkBlueprint.SDKRoot = sdkRoom.SDKRoot;
            sdkBlueprint.ObjectReference = sdkRoom.ObjectReference;

            sdkBlueprint.SDKRoot.transform.parent = null;
        }
    }
}
