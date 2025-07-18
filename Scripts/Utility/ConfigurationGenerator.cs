using System;
using System.Collections.Generic;
using mtion.room.sdk.action;
using mtion.room.sdk.compiled;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
#endif

namespace mtion.room.sdk
{
    public class ConfigurationGenerator
    {
#if UNITY_EDITOR
        [Serializable]
        private class MyPackageManifest
        {
            public string version;
        }
#endif
        


        public static string ConvertSDKSceneToJsonString(
            MTIONSDKDescriptorSceneBase descriptor,
            MVirtualCameraEventTracker[] cameras,
            MVirtualDisplayTracker[] displays,
            MVirtualLightingTracker[] lights,
            MVirtualAssetTracker[] assetTrackers,
            MVirtualAvatarTracker[] avatarTrackers,
            string thumbnailMediaId)
        {
            SceneConfigurationFile model = new SceneConfigurationFile();

            string version = "0.0.0";

#if UNITY_EDITOR
            var assembly = typeof(ConfigurationGenerator).Assembly;
            var packageInfo = PackageInfo.FindForAssembly(assembly);
            if (packageInfo != null)
            {
                version = packageInfo.version;
            }
            else
            {
                TextAsset packageInfoFile = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/LocalPackages/MTIONStudioSDK/package.json");
                MyPackageManifest manifest = JsonConvert.DeserializeObject<MyPackageManifest>(packageInfoFile.text);
                version = manifest.version;
            }
#endif
            
            model.SceneType = descriptor.ObjectType;
            model.Name = descriptor.Name;
            model.GUID = descriptor.InternalID;
            model.Version = descriptor.Version;
            model.InternalVersion = version;
            var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            model.UpdateTimeMS = unixTimestamp;
            model.CreateTimeMS = descriptor.CreateTimeMS;
            if (descriptor.ObjectType == MTIONObjectType.MTIONSDK_ROOM)
            {
                var roomDescriptor = (MTIONSDKRoom)descriptor;
                if (!string.IsNullOrEmpty(roomDescriptor.EnvironmentInternalID))
                {
                    model.Metadata = JsonConvert.SerializeObject(new RoomMetadata
                        {
                            EnvGuid = roomDescriptor.EnvironmentInternalID
                        },
                        Formatting.None,
                        new JsonSerializerSettings()
                        {
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                        }
                    );
                }

                var camerasOrdered = cameras.OrderBy(x => x.OrderPrecedence).ToList();
                foreach (var camera in camerasOrdered)
                {
                    model.Cameras.Add(ConvertCameraToConfigData(camera));
                }

                model.NumCameras = cameras.Length;

                foreach (var display in displays)
                {
                    model.Displays.Add(ConvertDisplayToConfigData(display));
                }

                model.NumDisplays = displays.Length;


                foreach (var light in lights)
                {
                    model.Lights.Add(ConvertLightToConfigData(light));
                }

                model.NumLights = lights.Length;

                foreach (var assets in assetTrackers)
                {
                    model.Assets.Add(ConvertAssetToConfigData(assets));
                }

                model.NumAssets = assetTrackers.Length;

                foreach (var avatar in avatarTrackers)
                {
                    model.Avatars.Add(ConvertAvatarToConfigData(avatar));
                }

                model.NumAvatars = avatarTrackers.Length;
            }
            else
            {
                model.NumCameras = 0;
                model.NumDisplays = 0;
                model.NumLights = 0;
                model.NumAssets = 0;
            }

            return JsonConvert.SerializeObject(model, Formatting.None,
                new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });
        }

        public static bool HasSceneLogicNode(GameObject gameObject)
        {
            if (gameObject == null)
                return false;

            MonoBehaviour[] components = gameObject.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour component in components)
            {
                if (component is ISceneLogcNode)
                    return true;
            }

            return false;
        }

        public static string ConvertSDKAssetToJsonString(MTIONSDKAssetBase descriptor)
        {
            string version = "0.0.0";

#if UNITY_EDITOR
            var assembly = typeof(ConfigurationGenerator).Assembly;
            var packageInfo = PackageInfo.FindForAssembly(assembly);
            if (packageInfo != null)
            {
                version = packageInfo.version;
            }
            else
            {
                TextAsset packageInfoFile = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/LocalPackages/MTIONStudioSDK/package.json");
                MyPackageManifest manifest = JsonConvert.DeserializeObject<MyPackageManifest>(packageInfoFile.text);
                version = manifest.version;
            }
#endif

            AssetConfigurationFile model = new AssetConfigurationFile();
            
            model.SceneType = descriptor.ObjectType;
            model.Name = descriptor.Name;
            model.GUID = descriptor.InternalID;
            model.Version = descriptor.Version;
            model.InternalVersion = version;
            var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            model.UpdateTimeMS = unixTimestamp;
            model.CreateTimeMS = descriptor.CreateTimeMS;
            model.Metadata = null;

            var updatedGO = GameObject.FindObjectsOfType<MTIONSDKAssetBase>()
                .First(x => x.InternalID == descriptor.InternalID);

            if (descriptor.ObjectType == MTIONObjectType.MTIONSDK_ASSET)
            {
                var rb = updatedGO.ObjectReference.GetComponentInChildren<Rigidbody>();
                model.HasPhysics = rb != null
                    ? AssetConfigurationFile.HasSetting.TRUE
                    : AssetConfigurationFile.HasSetting.FALSE;

                model.IsSceneLogicNode = HasSceneLogicNode(updatedGO.ObjectReference) 
                    ? AssetConfigurationFile.HasSetting.TRUE
                    : AssetConfigurationFile.HasSetting.FALSE;

            }

            var actionDataGroup = new List<ActionData>();
            var actionBehaviourGroup = descriptor.ObjectReferenceProp.GetComponentInChildren<MActionBehaviourGroup>(true);
            if (actionBehaviourGroup != null)
            {
                foreach (var actionBehaviour in actionBehaviourGroup.MActionMap)
                {
                    var actionData = new ActionData();
                    actionData.ActionName = actionBehaviour.ActionName;
                    actionData.ActionDescription = actionBehaviour.ActionDescription;
                    actionDataGroup.Add(actionData);
                }
            }
            model.ActionDataGroup = actionDataGroup;

            return JsonConvert.SerializeObject(model, Formatting.None,
                new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });
        }

        public static string ConvertSDKAvatarToJsonString(MTIONSDKAssetBase descriptor)
        {
            string version = "0.0.0";

#if UNITY_EDITOR
            var assembly = typeof(ConfigurationGenerator).Assembly;
            var packageInfo = PackageInfo.FindForAssembly(assembly);
            if (packageInfo != null)
            {
                version = packageInfo.version;
            }
            else
            {
                TextAsset packageInfoFile = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/LocalPackages/MTIONStudioSDK/package.json");
                MyPackageManifest manifest = JsonConvert.DeserializeObject<MyPackageManifest>(packageInfoFile.text);
                version = manifest.version;
            }
#endif

            AvatarConfigurationFile model = new AvatarConfigurationFile();

            model.SceneType = descriptor.ObjectType;
            model.Name = descriptor.Name;
            model.GUID = descriptor.InternalID;
            model.Version = descriptor.Version;
            model.InternalVersion = version;
            var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            model.UpdateTimeMS = unixTimestamp;
            model.CreateTimeMS = descriptor.CreateTimeMS;
            model.Metadata = null;

            var updatedGO = GameObject.FindObjectsOfType<MTIONSDKAssetBase>()
                .First(x => x.InternalID == descriptor.InternalID);

            var avatarRagdoll = updatedGO.ObjectReference.GetComponentInChildren<MTIONAvatarRagdoll>();
            model.HasRagdoll = avatarRagdoll != null && avatarRagdoll.HasRequiredBones();

            return JsonConvert.SerializeObject(model, Formatting.None,
                new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });
        }

        public static CameraParameters ConvertCameraToConfigData(MVirtualCameraEventTracker camera)
        {
            CameraParameters cameraModel = new CameraParameters();

            cameraModel.GUID = camera.GUID;

            cameraModel.Position = camera.transform.position;
            cameraModel.Rotation = camera.transform.rotation;
            cameraModel.Scale = camera.transform.localScale;

            cameraModel.AspectRatio = camera.CameraParams.AspectRatio;
            cameraModel.NearPlane = camera.CameraParams.NearPlane;
            cameraModel.FarPlane = camera.CameraParams.FarPlane;
            cameraModel.VerticalFoV = camera.CameraParams.VerticalFoV;
            cameraModel.ClearFlags = camera.CameraParams.ClearFlags;
            cameraModel.KeyCodeList = camera.CameraParams.KeyCodeList;

            return cameraModel;
        }

        public static DisplayParameters ConvertDisplayToConfigData(MVirtualDisplayTracker display)
        {
            DisplayParameters displayModel = new DisplayParameters();

            displayModel.GUID = display.GUID;

            displayModel.Position = display.transform.position;
            displayModel.Rotation = display.transform.rotation;
            displayModel.Scale = display.transform.localScale;

            displayModel.LocalHeight = display.GetVisualizationQuad().transform.localScale.y;
            displayModel.LocalWidth = display.GetVisualizationQuad().transform.localScale.x;
            displayModel.AspectRatio = displayModel.LocalWidth / displayModel.LocalHeight;
            displayModel.KeyCodeList = display.DisplayParams.KeyCodeList;
            displayModel.DisplayType = display.DisplayParams.DisplayType;

            return displayModel;
        }


        public static LightingParameters ConvertLightToConfigData(MVirtualLightingTracker light)
        {
            LightingParameters lightModel = new LightingParameters();

            lightModel.GUID = light.GUID;

            lightModel.Position = light.transform.position;
            lightModel.Rotation = light.transform.rotation;
            lightModel.Scale = light.transform.localScale;


            lightModel.LightColor = light.LightParams.LightColor;
            lightModel.LightIntensity = light.LightParams.LightIntensity;
            lightModel.LightType = light.LightParams.LightType;

            return lightModel;
        }

        public static AssetParameters ConvertAssetToConfigData(MVirtualAssetTracker asset)
        {
            AssetParameters assetModel = new AssetParameters();

            assetModel.GUID = asset.InternalID;

            assetModel.Position = asset.transform.position;
            assetModel.Rotation = asset.transform.rotation;
            assetModel.Scale = asset.transform.localScale;

            assetModel.VirtualObjectType = asset.AssetParams.VirtualObjectType;

            return assetModel;
        }

        public static AvatarParameters ConvertAvatarToConfigData(MVirtualAvatarTracker avatar)
        {
            AvatarParameters avatarModel = new AvatarParameters();
            
            avatarModel.GUID = avatar.InternalID;

            avatarModel.Position = avatar.transform.position;
            avatarModel.Rotation = avatar.transform.rotation;
            avatarModel.Scale = avatar.transform.localScale;

            return avatarModel;
        }

        public static string ConvertConfigurationSettingsToJson(SceneConfigurationFile configFile)
        {
            return JsonConvert.SerializeObject(configFile, Formatting.None,
                new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });
        }

        public static string ConvertAssetConfigurationSettingsToJson(AssetConfigurationFile assetConfigFile) 
        {
            return JsonConvert.SerializeObject(assetConfigFile, Formatting.None,
                new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });
        }

        public static string ConvertAvatarConfigurationSettingsToJson(AvatarConfigurationFile avatarConfigFile)
        {
            return JsonConvert.SerializeObject(avatarConfigFile, Formatting.None,
                new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });
        }


        public static SceneConfigurationFile ConvertJsonToConfigurationSettings(string jsonData)
        {
            SceneConfigurationFile sceneConfiguration =
                JsonConvert.DeserializeObject<SceneConfigurationFile>(jsonData);
            return sceneConfiguration;
        }

        public static AssetConfigurationFile ConvertJsonToAssetConfigurationSettings(string jsonData)
        {
            AssetConfigurationFile assetConfiguration =
                JsonConvert.DeserializeObject<AssetConfigurationFile>(jsonData);
            return assetConfiguration;
        }

        public static AvatarConfigurationFile ConvertJsonToAvatarConfigurationSettings(string jsonData)
        {
            AvatarConfigurationFile avatarConfiguration =
                JsonConvert.DeserializeObject<AvatarConfigurationFile>(jsonData);
            return avatarConfiguration;
        }
    }
}
