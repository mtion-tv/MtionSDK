using mtion.room.sdk.compiled;
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace mtion.room.sdk
{
    public class SDKEditorUtil
    {
        public static Scene CreateBlueprintScene()
        {
            var roomScene = SDKEditorUtil.CreateRoomScene();

            var environmentScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            environmentScene.name = roomScene.name + "_Environment";
            SDKEditorUtil.CreateEnvironmentScene();
            var roomSceneFolderPath = Path.GetDirectoryName(roomScene.path);
            EditorSceneManager.SaveScene(environmentScene, $"{roomSceneFolderPath}/{environmentScene.name}.unity");

            EditorSceneManager.SetActiveScene(roomScene);
            GameObject blueprintDescriptorObject = new GameObject("MTIONBlueprintDescriptor");
            blueprintDescriptorObject.transform.position = Vector3.zero;
            blueprintDescriptorObject.transform.rotation = Quaternion.identity;
            var sdkBlueprint = blueprintDescriptorObject.AddComponent<MTIONSDKBlueprint>();

            SDKEditorUtil.InitAddressableAssetFields(sdkBlueprint, MTIONObjectType.MTIONSDK_BLUEPRINT, roomScene.name);
            sdkBlueprint.RoomSceneName = roomScene.name;
            sdkBlueprint.EnvironmentSceneName = environmentScene.name;

            var sdkRoom = sdkBlueprint.GetMTIONSDKRoom();
            var sdkEnvironment = sdkBlueprint.GetMTIONSDKEnvironment();

            sdkRoom.EnvironmentInternalID = sdkEnvironment.InternalID;
            return roomScene;
        }

        public static Scene CreateRoomScene()
        {
            GameObject RoomDescriptorObject = new GameObject("MTIONRoomDescriptor");
            RoomDescriptorObject.transform.position = Vector3.zero;
            RoomDescriptorObject.transform.rotation = Quaternion.identity;
            var sdkcomp = RoomDescriptorObject.AddComponent<MTIONSDKRoom>();

            var scene = EditorSceneManager.GetActiveScene();
            InitAddressableAssetFields(sdkcomp, MTIONObjectType.MTIONSDK_ROOM, scene.name);
            AssetDatabase.SaveAssets();
            GenerateMTIONScene(sdkcomp, scene);

            return scene;
        }

        public static Scene CreateEnvironmentScene()
        {
            GameObject RoomDescriptorObject = new GameObject("MTIONEnvironmentDescriptor");
            RoomDescriptorObject.transform.position = Vector3.zero;
            RoomDescriptorObject.transform.rotation = Quaternion.identity;
            var sdkcomp = RoomDescriptorObject.AddComponent<MTIONSDKEnvironment>();

            var scene = EditorSceneManager.GetActiveScene();
            InitAddressableAssetFields(sdkcomp, MTIONObjectType.MTIONSDK_ENVIRONMENT, scene.name);
            AssetDatabase.SaveAssets();
            GenerateMTIONScene(sdkcomp, scene);

            return scene;
        }

        public static void CreateAssetScene()
        {
            GameObject RoomDescriptorObject = new GameObject("MTIONAssetDescriptor");
            RoomDescriptorObject.transform.position = Vector3.zero;
            RoomDescriptorObject.transform.rotation = Quaternion.identity;
            var sdkcomp = RoomDescriptorObject.AddComponent<MTIONSDKAsset>();

            var scene = EditorSceneManager.GetActiveScene();
            InitAddressableAssetFields(sdkcomp, MTIONObjectType.MTIONSDK_ASSET, scene.name);
            AssetDatabase.SaveAssets();
            GenerateMTIONScene(sdkcomp, scene);
        }

        public static void CreateAvatarScene()
        {
            GameObject avatarDescriptorObject = new GameObject("MTIONAvatarDescriptor");
            avatarDescriptorObject.transform.position = Vector3.zero;
            avatarDescriptorObject.transform.rotation = Quaternion.identity;
            var sdkcomp = avatarDescriptorObject.AddComponent<MTIONSDKAvatar>();

            var scene = EditorSceneManager.GetActiveScene();
            InitAddressableAssetFields(sdkcomp, MTIONObjectType.MTIONSDK_AVATAR, scene.name);
            AssetDatabase.SaveAssets();
            GenerateMTIONScene(sdkcomp, scene);
        }

        private static void GenerateMTIONScene(MTIONSDKDescriptorSceneBase descriptor, Scene scene)
        {
            string descriptorObjectType = MTIONSDKAssetBase.ConvertObjectTypeToString(descriptor.ObjectType);
            string scenePath = scene.path;

            Camera mainCamera = null;
            foreach (var rootGO in scene.GetRootGameObjects())
            {
                if (rootGO.GetComponent<Camera>())
                {
                    mainCamera = rootGO.GetComponent<Camera>();
                }
            }
            if (mainCamera == null)
            {
                GameObject camGO = new GameObject("Main Camera");
                mainCamera = camGO.AddComponent<Camera>();
            }

            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 1.93f;
            mainCamera.transform.position = new Vector3(-3.31999993f, 2.3599999f, 2.93000007f);
            mainCamera.transform.rotation = new Quaternion(-0.0752960071f, -0.872711599f, 0.142591402f, -0.460839152f);
            mainCamera.transform.parent = descriptor.gameObject.transform;

            var forceObjectReferenceCration = descriptor.ObjectReferenceProp;

            if (descriptor.ObjectType == MTIONObjectType.MTIONSDK_ROOM)
            {
                var roomDescriptor = descriptor as MTIONSDKRoom;
                if (roomDescriptor.SDKRoot == null)
                {
                    roomDescriptor.SDKRoot = new GameObject("SDK PROPS");
                    roomDescriptor.SDKRoot.transform.localPosition = Vector3.zero;
                    roomDescriptor.SDKRoot.transform.localRotation = Quaternion.identity;
                    roomDescriptor.SDKRoot.transform.localScale = Vector3.one;
                }
            }
        }

        public static void InitAddressableAssetFields(MTIONSDKAssetBase assetBase, MTIONObjectType objectType, string name = "")
        {
            if (string.IsNullOrEmpty(assetBase.GUID))
            {
                assetBase.GenerateNewGUID(null);
                assetBase.CreateTimeMS = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
            else if (!Guid.TryParseExact(assetBase.GUID, "D", out var _))
            {
                assetBase.MigrateGUID();
                assetBase.CreateTimeMS = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            if (!string.IsNullOrEmpty(name) && string.IsNullOrEmpty(assetBase.Name))
            {
                assetBase.Name = name;
                assetBase.gameObject.name = name;
            }
            else if (string.IsNullOrEmpty(assetBase.Name))
            {
                assetBase.Name = assetBase.gameObject.name;
            }
            else
            {
                assetBase.gameObject.name = assetBase.Name;
            }

            assetBase.InternalID = SDKUtil.GetSDKInternalID(assetBase.GUID);
            assetBase.ObjectType = objectType;
            EditorUtility.SetDirty(assetBase);
        }

        public static void InitVirtualComponentFields(VirtualComponentTracker virtualComponent)
        {
            if (string.IsNullOrEmpty(virtualComponent.GUID))
            {
                virtualComponent.GenerateNewGUID();
            }
        }
    }
}
