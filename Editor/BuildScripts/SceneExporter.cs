using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using mtion.room.sdk.compiled;
using mtion.service.api;
using mtion.utility;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace mtion.room.sdk
{
    public class SceneExporter
    {
        private MTIONSDKDescriptorSceneBase sceneObjectDescriptor;
        private float exportPercentComplete;

        private ExportLocationOptions LocationOptions => sceneObjectDescriptor.LocationOption;
        public float ExportPercentComplete => exportPercentComplete;

        public SceneExporter(MTIONSDKDescriptorSceneBase sceneObjectDescriptor)
        {
            this.sceneObjectDescriptor = sceneObjectDescriptor;
        }

        public void ExportSDKScene()
        {
            if (sceneObjectDescriptor == null)
            {
                throw new ArgumentNullException();
            }
            SDKServerManager.Init();

            if (string.IsNullOrEmpty(SDKServerManager.UserId))
            {
                Debug.LogError($"Error exporting SDK scene - not authenticated");
                return;
            }

            sceneObjectDescriptor.TemporaryHideGizmosForBuild();

            MTIONObjectType objectType = sceneObjectDescriptor.ObjectType;
            switch (objectType)
            {
                case MTIONObjectType.MTIONSDK_BLUEPRINT:
                    ExportBlueprintScene();
                    break;

                case MTIONObjectType.MTIONSDK_ASSET:
                    ExportAssetScene(MTIONObjectType.MTIONSDK_ASSET);
                    break;

                case MTIONObjectType.MTIONSDK_ROOM:
                    ExportRoomScene();
                    break;

                case MTIONObjectType.MTIONSDK_ENVIRONMENT:
                    ExportEnvironmentScene();
                    break;

                case MTIONObjectType.MTIONSDK_AVATAR:
                    ExportAssetScene(MTIONObjectType.MTIONSDK_AVATAR);
                    break;
            }
        }

        private void ExportAssetScene(MTIONObjectType objectType)
        {
            SDKServerManager.Init();

            if (objectType == MTIONObjectType.MTIONSDK_ASSET)
            {
                MTIONSDKAsset asset = sceneObjectDescriptor as MTIONSDKAsset;

                SDKServerManager.VerifyAssetGuid(asset);



            }
            else if (objectType == MTIONObjectType.MTIONSDK_AVATAR)
            {
                MTIONSDKAvatar asset = sceneObjectDescriptor as MTIONSDKAvatar;
                
                SDKServerManager.VerifyAssetGuid(asset);


            }

            SceneVerificationUtil.VerifySceneIntegrity(sceneObjectDescriptor);
            ExportVirtualAssetData(sceneObjectDescriptor);
        }

        private void ExportBlueprintScene()
        {
            MTIONSDKBlueprint blueprintDescriptor = sceneObjectDescriptor as MTIONSDKBlueprint;

            if (blueprintDescriptor == null)
            {
                throw new ArgumentNullException("Blueprint Decriptor is Null");
            }

            if (string.IsNullOrEmpty(blueprintDescriptor.GUID))
            {

                blueprintDescriptor.GenerateNewGUID(null);
            }

            {
                SDKServerManager.VerifyAssetGuid(blueprintDescriptor);
            }

            MarkSceneDirty(blueprintDescriptor);

            var roomDescriptor = blueprintDescriptor.GetMTIONSDKRoom();
            {
                SDKServerManager.VerifyAssetGuid(roomDescriptor);
            }

            var environmentDescriptor = blueprintDescriptor.GetMTIONSDKEnvironment();
            {
                var (resource, updated) = SDKServerManager.VerifyAssetGuid(roomDescriptor);

                if (updated)
                {
                    roomDescriptor.EnvironmentInternalID = environmentDescriptor.GUID;
                    EditorUtility.SetDirty(roomDescriptor);
                }
            }

            roomDescriptor.Name = $"{blueprintDescriptor.Name}_Room";
            roomDescriptor.LocationOption = blueprintDescriptor.LocationOption;
            roomDescriptor.EnvironmentInternalID = environmentDescriptor.InternalID;

            environmentDescriptor.Name = $"{blueprintDescriptor.Name}_Environment";
            environmentDescriptor.LocationOption = blueprintDescriptor.LocationOption;

            EditorSceneManager.SaveOpenScenes();

            var roomScene = EditorSceneManager.GetSceneByName(blueprintDescriptor.RoomSceneName);
            EditorSceneManager.SetActiveScene(roomScene);
            ExportRoomScene(roomDescriptor);

            blueprintDescriptor = sceneObjectDescriptor as MTIONSDKBlueprint;
            environmentDescriptor = blueprintDescriptor.GetMTIONSDKEnvironment();
            var environmentScene = EditorSceneManager.GetSceneByName(blueprintDescriptor.EnvironmentSceneName);
            EditorSceneManager.SetActiveScene(environmentScene);
            ExportEnvironmentScene(environmentDescriptor, () =>
            {
                blueprintDescriptor = sceneObjectDescriptor as MTIONSDKBlueprint;
                roomDescriptor = blueprintDescriptor.GetMTIONSDKRoom();
                environmentDescriptor = blueprintDescriptor.GetMTIONSDKEnvironment();

                ExportBlueprintSceneData(blueprintDescriptor, roomDescriptor, environmentDescriptor);

                roomScene = EditorSceneManager.GetSceneByName(blueprintDescriptor.RoomSceneName);
                EditorSceneManager.SetActiveScene(roomScene);
            });

        }

        private void MarkSceneDirty(MTIONSDKDescriptorSceneBase sceneBase)
        {
            var baseDirectory = Path.Combine(SDKUtil.GetSDKBlueprintDirectory(ExportLocationOptions.PersistentStorage), sceneBase.GUID);
            if (!Directory.Exists(baseDirectory))
            {
                Directory.CreateDirectory(baseDirectory);
            }

            var localMetaPath = Path.Combine(baseDirectory, "meta.json");

            var metaJson = new Dictionary<string, object>();
            if (SafeFileIO.Exists(localMetaPath))
            {
                var metaFileData = SafeFileIO.ReadAllText(localMetaPath);
                metaJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(metaFileData);
            }

            metaJson["is_dirty"] = true;
            SafeFileIO.WriteAllText(localMetaPath, JsonConvert.SerializeObject(metaJson));
        }

        private void ExportRoomScene(MTIONSDKRoom roomDescriptor = null)
        {
            if (roomDescriptor == null)
            {
                roomDescriptor = sceneObjectDescriptor as MTIONSDKRoom;
            }

            SDKServerManager.VerifyAssetGuid(roomDescriptor);

            if (string.IsNullOrEmpty(roomDescriptor.GUID))
            {
                roomDescriptor.GenerateNewGUID(null);
            }

            ComponentVerificationUtil.VerifyAllComponentsIntegrity(roomDescriptor, MTIONObjectType.MTIONSDK_CAMERA, false);
            ComponentVerificationUtil.VerifyAllComponentsIntegrity(roomDescriptor, MTIONObjectType.MTIONSDK_DISPLAY, false);
            ComponentVerificationUtil.VerifyAllComponentsIntegrity(roomDescriptor, MTIONObjectType.MTIONSDK_LIGHT, false);
            ComponentVerificationUtil.VerifyAllComponentsIntegrity(roomDescriptor, MTIONObjectType.MTIONSDK_ASSET, true);
            ComponentVerificationUtil.VerifyAllComponentsIntegrity(roomDescriptor, MTIONObjectType.MTIONSDK_AVATAR, true);

            MVirtualCameraEventTracker[] cameraTrackers = GameObject.FindObjectsOfType<MVirtualCameraEventTracker>();
            MVirtualDisplayTracker[] displayTrackers = GameObject.FindObjectsOfType<MVirtualDisplayTracker>();
            MVirtualAssetTracker[] assetTrackers = GameObject.FindObjectsOfType<MVirtualAssetTracker>();
            MVirtualLightingTracker[] lightTrackers = GameObject.FindObjectsOfType<MVirtualLightingTracker>();
            MVirtualAvatarTracker[] avatarTrackers = GameObject.FindObjectsOfType<MVirtualAvatarTracker>();

            VerifyAndSaveSDKComponents(cameraTrackers, displayTrackers, lightTrackers, assetTrackers, avatarTrackers);

            exportPercentComplete = 0.0f;

            ExportRoomSceneData(roomDescriptor);
            exportPercentComplete += 0.3f;


            var allAssets = GameObject.FindObjectsOfType<MVirtualAssetTracker>();
            var assetGuidsToExport = AssetComparisonUtil.FilterDuplicateAssets(allAssets, roomDescriptor.LocationOption)
                .Select(tracker => tracker.GUID).ToList();
            var exportDelta = 0.7f / assetGuidsToExport.Count;

            for (int i = 0; i < assetGuidsToExport.Count; i++)
            {
                var asset = GameObject.FindObjectsOfType<MVirtualAssetTracker>()
                    .Where(tracker => tracker.GUID == assetGuidsToExport[i]).First();

                ExportVirtualAssetData(asset);
                exportPercentComplete += exportDelta;
            }

            exportPercentComplete = 1.0f;
        }


        private void ExportEnvironmentScene(MTIONSDKEnvironment environmentDescriptor = null, Action onComplete = null)
        {
            if (environmentDescriptor == null)
            {
                environmentDescriptor = sceneObjectDescriptor as MTIONSDKEnvironment;
            }

            SDKServerManager.VerifyAssetGuid(environmentDescriptor);

            if (string.IsNullOrEmpty(environmentDescriptor?.GUID))
            {
                environmentDescriptor.GenerateNewGUID(null);
            }

            exportPercentComplete = 0.5f;

            ExportLightingData(() =>
            {
                ExportEnvironmentSceneData(environmentDescriptor);
                exportPercentComplete = 1.0f;
                onComplete?.Invoke();
            });


            void ExportLightingData(Action onComplete)
            {
                Lightmapper lightmapper = Lightmapper.UnityLightmapper;
#if BAKERY_INCLUDED
                lightmapper = Lightmapper.BakeryLightmapper;
#endif
                MLightmapBuildManager.StartStoringProcess(environmentDescriptor, lightmapper, onComplete);

            }
        }

        private void ExportBlueprintSceneData(MTIONSDKBlueprint blueprintDescriptor,
            MTIONSDKRoom roomDescriptor,
            MTIONSDKEnvironment environmentDescriptor)
        {
            var rootId = Guid.NewGuid().ToString();
            var rootChildrenIds = new List<string>();

            var blueprintData = new MTIONSDKBlueprint.ClubhouseData()
            {
                Id = blueprintDescriptor.GUID,
                RoomAssetId = roomDescriptor.GUID,
                EnvironmentAssetId = environmentDescriptor.GUID,
                RootElementId = rootId,
                Name = blueprintDescriptor.Name,
                Description = blueprintDescriptor.Description,
                FormatVersion = MTIONSDKBlueprint.CurrentFormatVersion,
                ElementMap = new()
                {
                    {
                        rootId,
                        new MTIONSDKBlueprint.ClubhouseElement<object>()
                        {
                            Id = rootId,
                            Name = "Root",
                            Position = Vector3.zero,
                            Rotation = Quaternion.identity,
                            Scale = Vector3.one,
                            ElementType = "empty",
                            ChildrenIds = rootChildrenIds,
                            TypeExtra = null,
                        }
                    },
                },
            };
            var blueprintLocalData = new MTIONSDKBlueprint.ClubhouseLocalData()
            {
                Id = blueprintDescriptor.GUID,
            };
            var blueprintThumbnailId = GetThumbnailId(blueprintDescriptor);

            var roomThumbnailId = GetThumbnailId(roomDescriptor);
            if (!string.IsNullOrEmpty(roomThumbnailId))
            {
                var roomThumbnailPath = Path.Combine(SDKUtil.GetSDKThumbnailDirectory(ExportLocationOptions.PersistentStorage), roomThumbnailId, $"{roomThumbnailId}.png");
                if (SafeFileIO.Exists(roomThumbnailPath))
                {
                    var bpThumbnailDir = Path.Combine(SDKUtil.GetSDKThumbnailDirectory(ExportLocationOptions.PersistentStorage), blueprintThumbnailId);
                    if (!Directory.Exists(bpThumbnailDir))
                    {
                        Directory.CreateDirectory(bpThumbnailDir);
                    }

                    var bpThumbnailPath = Path.Combine(bpThumbnailDir, $"{blueprintThumbnailId}.png");
                    SafeFileIO.Copy(roomThumbnailPath, bpThumbnailPath, true);
                }
            }

            MVirtualCameraEventTracker[] cameraTrackers = GameObject.FindObjectsOfType<MVirtualCameraEventTracker>();
            MVirtualDisplayTracker[] displayTrackers = GameObject.FindObjectsOfType<MVirtualDisplayTracker>();
            MVirtualLightingTracker[] lightTrackers = GameObject.FindObjectsOfType<MVirtualLightingTracker>();
            MVirtualAssetTracker[] assetTrackers = GameObject.FindObjectsOfType<MVirtualAssetTracker>();

            for (var i = 0; i < cameraTrackers.Length; ++i)
            {
                var id = Guid.NewGuid().ToString();
                rootChildrenIds.Add(id);

                blueprintData.ElementMap[id] = new MTIONSDKBlueprint.ClubhouseCamera()
                {
                    Id = id,
                    Name = $"Camera {i + 1}",
                    ElementType = "camera",
                    Position = cameraTrackers[i].transform.position,
                    Rotation = cameraTrackers[i].transform.rotation,
                    Scale = cameraTrackers[i].transform.localScale,
                    ParentId = rootId,
                    TypeExtra = new()
                    {
                        AspectRatio = cameraTrackers[i].CameraParams.AspectRatio,
                        FarPlane = cameraTrackers[i].CameraParams.FarPlane,
                        NearPlane = cameraTrackers[i].CameraParams.NearPlane,
                        Fov = cameraTrackers[i].CameraParams.VerticalFoV,
                        KeyCodeList = cameraTrackers[i].CameraParams.KeyCodeList,
                    },
                };
            }

            for (var i = 0; i < displayTrackers.Length; ++i)
            {
                var id = Guid.NewGuid().ToString();
                rootChildrenIds.Add(id);

                blueprintData.ElementMap[id] = new MTIONSDKBlueprint.ClubhouseDisplay()
                {
                    Id = id,
                    Name = $"Display {i + 1}",
                    ElementType = "display",
                    Position = displayTrackers[i].transform.position,
                    Rotation = displayTrackers[i].transform.rotation,
                    Scale = displayTrackers[i].transform.localScale,
                    ParentId = rootId,
                    TypeExtra = new()
                    {
                        DisplayType = displayTrackers[i].DisplayParams.DisplayType,
                        KeyCodeList = displayTrackers[i].DisplayParams.KeyCodeList,
                    },
                };
            }

            for (var i = 0; i < lightTrackers.Length; ++i)
            {
                var id = Guid.NewGuid().ToString();
                rootChildrenIds.Add(id);

                blueprintData.ElementMap[id] = new MTIONSDKBlueprint.ClubhouseLight()
                {
                    Id = id,
                    Name = $"Light {i + 1}",
                    ElementType = "light",
                    Position = lightTrackers[i].transform.position,
                    Rotation = lightTrackers[i].transform.rotation,
                    Scale = lightTrackers[i].transform.localScale,
                    ParentId = rootId,
                    TypeExtra = new()
                    {
                        Color = lightTrackers[i].LightParams.LightColor,
                        Intensity = lightTrackers[i].LightParams.LightIntensity,
                        GizmoIsActive = lightTrackers[i].LightParams.LightGizmoIsActive,
                        Type = lightTrackers[i].LightParams.LightType,
                    },
                };
            }

            for (var i = 0; i < assetTrackers.Length; ++i)
            {
                var id = Guid.NewGuid().ToString();
                rootChildrenIds.Add(id);

                blueprintData.ElementMap[id] = new MTIONSDKBlueprint.ClubhouseObject()
                {
                    Id = id,
                    Name = assetTrackers[i].Name,
                    ElementType = "object",
                    Position = assetTrackers[i].transform.position,
                    Rotation = assetTrackers[i].transform.rotation,
                    Scale = assetTrackers[i].transform.localScale,
                    ParentId = rootId,
                    TypeExtra = new()
                    {
                        AddressableAssetId = assetTrackers[i].GUID,
                    },
                };
            }

            var blueprintPath = Path.Combine(SDKUtil.GetSDKBlueprintDirectory(blueprintDescriptor.LocationOption), blueprintDescriptor.GUID);
            var blueprintDataPath = Path.Combine(blueprintPath, "clubhouse_file.json");
            var blueprintLocalDataPath = Path.Combine(blueprintPath, "clubhouse_local_data.json");
            SafeFileIO.WriteAllText(blueprintDataPath, JSONUtil.Serialize(blueprintData));
            SafeFileIO.WriteAllText(blueprintLocalDataPath, JSONUtil.Serialize(blueprintLocalData));
        }

        private void ExportRoomSceneData(MTIONSDKDescriptorSceneBase sceneBase)
        {
            CreateAssetExportBackup(sceneBase, sceneBase.LocationOption);

            try
            {

                var thumbnailId = GetThumbnailId(sceneBase);
                var camera = sceneBase.gameObject.GetComponentInChildren<Camera>();
                var thumbnailDir = Path.Combine(SDKUtil.GetSDKThumbnailDirectory(sceneBase.LocationOption), thumbnailId);
                if (!Directory.Exists(thumbnailDir))
                {
                    Directory.CreateDirectory(thumbnailDir);
                }
                ThumbnailGenerator.TakeSnapshotOfAssetInCurrentScene(camera, thumbnailDir, $"{thumbnailId}.png");

                var addressablesExporter = new AssetExporter(sceneBase, sceneBase.LocationOption);
                string guid = sceneBase.GUID;
                addressablesExporter.ExportSDKAsset();

                if (sceneBase == null)
                {
                    sceneBase = GameObject.FindObjectsOfType<MTIONSDKDescriptorSceneBase>()
                        .Where(descriptor => descriptor.GUID == guid).First();
                }

                CreateDescriptorFile(sceneBase);

                MVirtualCameraEventTracker[] cameraTrackers = GameObject.FindObjectsOfType<MVirtualCameraEventTracker>();
                MVirtualDisplayTracker[] displayTrackers = GameObject.FindObjectsOfType<MVirtualDisplayTracker>();
                MVirtualAssetTracker[] assetTrackers = GameObject.FindObjectsOfType<MVirtualAssetTracker>();
                MVirtualLightingTracker[] lightTrackers = GameObject.FindObjectsOfType<MVirtualLightingTracker>();
                MVirtualAvatarTracker[] avatarTrackers = GameObject.FindObjectsOfType<MVirtualAvatarTracker>();
                string configData = ConfigurationGenerator.ConvertSDKSceneToJsonString(
                    sceneBase,
                    cameraTrackers,
                    displayTrackers,
                    lightTrackers,
                    assetTrackers,
                    avatarTrackers,
                    thumbnailId);
                var basePersistentDirectory = SDKUtil.GetSDKItemDirectory(sceneBase, sceneBase.LocationOption);
                WriteConfigurationFile(basePersistentDirectory, configData);
            }
            catch (Exception ex)
            {
                DeleteAssetExport(sceneBase, sceneBase.LocationOption);
                RestoreAssetExportBackup(sceneBase, sceneBase.LocationOption);
                DeleteAssetExportBackup(sceneBase, sceneBase.LocationOption);
                throw new Exception($"Error while exporting ROOM {sceneBase.InternalID}, restored backup: {ex}");
            }
            finally
            {
                DeleteAssetExportBackup(sceneBase, sceneBase.LocationOption);
            }
        }

        private void ExportEnvironmentSceneData(MTIONSDKDescriptorSceneBase sceneBase)
        {
            CreateAssetExportBackup(sceneBase, sceneBase.LocationOption);

            try
            {

                var thumbnailId = GetThumbnailId(sceneBase);
                var camera = sceneBase.gameObject.GetComponentInChildren<Camera>();
                var thumbnailDir = Path.Combine(SDKUtil.GetSDKThumbnailDirectory(sceneBase.LocationOption), thumbnailId);
                if (!Directory.Exists(thumbnailDir))
                {
                    Directory.CreateDirectory(thumbnailDir);
                }
                ThumbnailGenerator.TakeSnapshotOfAssetInCurrentScene(camera, thumbnailDir, $"{thumbnailId}.png");

                var addressablesExporter = new AssetExporter(sceneBase, sceneBase.LocationOption);
                string guid = sceneBase.GUID;
                addressablesExporter.ExportSDKAsset();

                if (sceneBase == null)
                {
                    sceneBase = GameObject.FindObjectsOfType<MTIONSDKDescriptorSceneBase>()
                        .Where(descriptor => descriptor.GUID == guid).First();
                }

                CreateDescriptorFile(sceneBase);
                string configData = ConfigurationGenerator.ConvertSDKSceneToJsonString(
                    sceneBase,
                    null,
                    null,
                    null,
                    null,
                    null,
                    thumbnailId);
                string basePersistentDirectory = SDKUtil.GetSDKItemDirectory(sceneBase, sceneBase.LocationOption);
                WriteConfigurationFile(basePersistentDirectory, configData);
            }
            catch (Exception ex)
            {
                DeleteAssetExport(sceneBase, sceneBase.LocationOption);
                RestoreAssetExportBackup(sceneBase, sceneBase.LocationOption);
                DeleteAssetExportBackup(sceneBase, sceneBase.LocationOption);
                throw new Exception($"Error while exporting ENVIRONMENT {sceneBase.InternalID}, restored backup: {ex}");
            }
            finally
            {
                DeleteAssetExportBackup(sceneBase, sceneBase.LocationOption);
            }
        }

        private void ExportVirtualAssetData(MTIONSDKAssetBase assetBase)
        {
            string guid = assetBase.GUID;
            CreateAssetExportBackup(assetBase, LocationOptions);

            try
            {
                sceneObjectDescriptor = BuildManager.Instance.SceneDescriptorObject.GetComponent<MTIONSDKDescriptorSceneBase>();
                var basePersistentDirectory = SDKUtil.GetSDKItemDirectory(assetBase, LocationOptions);
                var camera = sceneObjectDescriptor.gameObject.GetComponentInChildren<Camera>();

                Type assetBaseType = assetBase.GetType();

                var addressablesExporter = new AssetExporter(assetBase, LocationOptions);
                addressablesExporter.ExportSDKAsset();

                if (assetBase == null)
                {
                    if (assetBaseType == typeof(MVirtualAssetTracker))
                    {
                        assetBase = GameObject.FindObjectsOfType<MVirtualAssetTracker>()
                            .Where(tracker => tracker.GUID == guid).First();
                    }
                    else if (assetBaseType == typeof(MVirtualAvatarTracker))
                    {
                        assetBase = GameObject.FindObjectsOfType<MVirtualAvatarTracker>()
                            .Where(tracker => tracker.GUID == guid).First();
                    }
                    else
                    {
                        assetBase = GameObject.FindObjectsOfType<MTIONSDKAssetBase>()
                            .Where(tracker => tracker.GUID == guid).First();
                    }
                }

                CreateDescriptorFile(assetBase);

                string configData;
                switch (assetBase.ObjectType)
                {
                    case MTIONObjectType.MTIONSDK_AVATAR:
                        configData = ConfigurationGenerator.ConvertSDKAvatarToJsonString(assetBase);
                        break;
                    case MTIONObjectType.MTIONSDK_ASSET:
                    default:
                        configData = ConfigurationGenerator.ConvertSDKAssetToJsonString(assetBase);
                        break;
                }

                WriteConfigurationFile(basePersistentDirectory, configData);
            }
            catch (Exception ex)
            {
                DeleteAssetExport(assetBase, LocationOptions);
                RestoreAssetExportBackup(assetBase, LocationOptions);
                DeleteAssetExportBackup(assetBase, LocationOptions);
                throw new Exception($"Error while exporting ASSET {assetBase.InternalID}, restored backup: {ex}");
            }
            finally
            {
                DeleteAssetExportBackup(assetBase, LocationOptions);
            }
        }

        private void CreateDescriptorFile(MTIONSDKAssetBase assetBase)
        {
            string basePersistentDirectory = SDKUtil.GetSDKItemDirectory(assetBase, LocationOptions);
            string descriptorData = JsonUtility.ToJson(assetBase);
            var descriptorFile = Path.Combine(basePersistentDirectory, $"{assetBase.InternalID}.json").Replace('\\', '/');
            if (SafeFileIO.Exists(descriptorFile))
            {
                SafeFileIO.Delete(descriptorFile);
            }
            SafeFileIO.WriteAllText(descriptorFile, descriptorData);
        }

        private static void WriteConfigurationFile(string basePersistentDirectory, string configFileData)
        {
            var configFile = Path.Combine(basePersistentDirectory, $"config.json").Replace('\\', '/');
            if (SafeFileIO.Exists(configFile))
            {
                SafeFileIO.Delete(configFile);
            }
            SafeFileIO.WriteAllText(configFile, configFileData);
        }

        private static void VerifyAndSaveSDKComponents(
            MVirtualCameraEventTracker[] cameraTrackers,
            MVirtualDisplayTracker[] displayTrackers,
            MVirtualLightingTracker[] lightTrackers,
            MVirtualAssetTracker[] assetTrackers,
            MVirtualAvatarTracker[] avatarTrackers)
        {
            foreach (var avatar in avatarTrackers)
            {
                SDKEditorUtil.InitAddressableAssetFields(avatar, MTIONObjectType.MTIONSDK_AVATAR);
            }

            foreach (var asset in assetTrackers)
            {
                SDKEditorUtil.InitAddressableAssetFields(asset, MTIONObjectType.MTIONSDK_ASSET);
            }

            LoopOverComponentTrackers(cameraTrackers);
            LoopOverComponentTrackers(displayTrackers);
            LoopOverComponentTrackers(lightTrackers);

            void LoopOverComponentTrackers(VirtualComponentTracker[] componentTrackers)
            {
                foreach (var component in componentTrackers)
                {
                    Debug.Assert(component != null);
                    Debug.Assert(component.GUID != null);
                }
            }
        }

        private string GetThumbnailId(MTIONSDKDescriptorSceneBase sceneBase)
        {
            var thumbnailMediaId = "";
            var localDataPath = Path.Combine(SDKUtil.GetSDKAssetDirectory(sceneBase.LocationOption), sceneBase.GUID, "config.json");

            if (!SafeFileIO.Exists(localDataPath))
            {
                localDataPath = Path.Combine(SDKUtil.GetSDKBlueprintDirectory(), sceneBase.GUID, "meta.json");
            }

            if (SafeFileIO.Exists(localDataPath))
            {
                var fileRawData = SafeFileIO.ReadAllText(localDataPath);
                var configFileData = JsonConvert.DeserializeObject<Dictionary<string, object>>(fileRawData);

                if (configFileData.ContainsKey("ThumbnailMediaId"))
                {
                    thumbnailMediaId = (string)configFileData["ThumbnailMediaId"];
                }

                if (configFileData.ContainsKey("thumbnail_media_id"))
                {
                    thumbnailMediaId = (string)configFileData["thumbnail_media_id"];
                }
            }

            if (string.IsNullOrEmpty(thumbnailMediaId))
            {
                thumbnailMediaId = Guid.NewGuid().ToString();
            }

            return thumbnailMediaId;
        }

        private void CreateAssetExportBackup(MTIONSDKAssetBase assetBase, ExportLocationOptions exportLocation)
        {
            var previouslyExported = SDKUtil.GetSDKItemDirectoryExists(assetBase, exportLocation);
            if (!previouslyExported) return;

            var sourceFolder = SDKUtil.GetSDKItemDirectory(assetBase, exportLocation);
            var destinationFolder = SDKUtil.GetSDKItemBackupDirectory(assetBase, exportLocation);
            CopyDirectory(sourceFolder, destinationFolder);
        }

        private void RestoreAssetExportBackup(MTIONSDKAssetBase assetBase, ExportLocationOptions exportLocation)
        {
            var backupExists = SDKUtil.GetSDKItemBackupDirectoryExists(assetBase, exportLocation);
            if (!backupExists) return;

            var sourceFolder = SDKUtil.GetSDKItemBackupDirectory(assetBase, exportLocation);
            var destinationFolder = SDKUtil.GetSDKItemDirectory(assetBase, exportLocation);
            CopyDirectory(sourceFolder, destinationFolder);
        }

        private void DeleteAssetExport(MTIONSDKAssetBase assetBase, ExportLocationOptions exportLocation)
        {
            var exportExists = SDKUtil.GetSDKItemDirectoryExists(assetBase, exportLocation);
            if (exportExists)
            {
                var path = SDKUtil.GetSDKItemDirectory(assetBase, exportLocation);
                Directory.Delete(path, true);
            }
        }

        private void DeleteAssetExportBackup(MTIONSDKAssetBase assetBase, ExportLocationOptions exportLocation)
        {
            var backupExists = SDKUtil.GetSDKItemBackupDirectoryExists(assetBase, exportLocation);
            if (backupExists)
            {
                var path = SDKUtil.GetSDKItemBackupDirectory(assetBase, exportLocation);
                Directory.Delete(path, true);
            }
        }

        private void CopyDirectory(string sourceFolder, string destinationFolder)
        {
            foreach (string dirPath in Directory.GetDirectories(sourceFolder, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourceFolder, destinationFolder));
            }

            foreach (string newPath in Directory.GetFiles(sourceFolder, "*.*", SearchOption.AllDirectories))
            {
                SafeFileIO.Copy(newPath, newPath.Replace(sourceFolder, destinationFolder), true);
            }
        }
    }
}
