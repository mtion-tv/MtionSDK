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
using UnityEngine.SceneManagement;

namespace mtion.room.sdk
{
    public class SceneExporter
    {
        private MTIONSDKDescriptorSceneBase sceneObjectDescriptor;
        private float exportPercentComplete;
        private readonly SDKExportReport exportReport;

        private ExportLocationOptions LocationOptions => sceneObjectDescriptor.LocationOption;
        public float ExportPercentComplete => exportPercentComplete;
        public SDKExportReport ExportReport => exportReport;
        public bool IsCompleted { get; private set; }

        public event Action<SceneExporter, Exception> ExportFinished;

        public SceneExporter(MTIONSDKDescriptorSceneBase sceneObjectDescriptor)
        {
            this.sceneObjectDescriptor = sceneObjectDescriptor;
            exportReport = new SDKExportReport
            {
                StartedAtMS = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            RefreshReportRootMetadata();
        }

        public void ExportSDKScene()
        {
            if (sceneObjectDescriptor == null)
            {
                throw new ArgumentNullException();
            }

            try
            {
                SDKServerManager.Init();

                if (string.IsNullOrEmpty(SDKServerManager.UserId))
                {
                    throw new InvalidOperationException("Error exporting SDK scene - not authenticated");
                }

                RefreshReportRootMetadata();
                sceneObjectDescriptor.TemporaryHideGizmosForBuild();

                MTIONObjectType objectType = sceneObjectDescriptor.ObjectType;
                switch (objectType)
                {
                    case MTIONObjectType.MTIONSDK_BLUEPRINT:
                        PrepareExportTimestamps(sceneObjectDescriptor);
                        ExportBlueprintScene();
                        break;

                    case MTIONObjectType.MTIONSDK_ASSET:
                        PrepareExportTimestamps(sceneObjectDescriptor);
                        ExportAssetScene(MTIONObjectType.MTIONSDK_ASSET);
                        CompleteExport();
                        break;

                    case MTIONObjectType.MTIONSDK_ROOM:
                        PrepareExportTimestamps(sceneObjectDescriptor);
                        ExportRoomScene();
                        CompleteExport();
                        break;

                    case MTIONObjectType.MTIONSDK_ENVIRONMENT:
                        PrepareExportTimestamps(sceneObjectDescriptor);
                        ExportEnvironmentScene();
                        break;

                    case MTIONObjectType.MTIONSDK_AVATAR:
                        PrepareExportTimestamps(sceneObjectDescriptor);
                        ExportAssetScene(MTIONObjectType.MTIONSDK_AVATAR);
                        CompleteExport();
                        break;
                }
            }
            catch (Exception ex)
            {
                CompleteExport(ex);
                throw;
            }
        }

        private void ExportAssetScene(MTIONObjectType objectType)
        {
            SDKServerManager.Init();
            SetProgress(0.0f, $"Preparing {sceneObjectDescriptor.InternalID} for export");

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

            RefreshReportRootMetadata();

            SceneVerificationUtil.VerifySceneIntegrity(sceneObjectDescriptor);
            ExportVirtualAssetData(sceneObjectDescriptor);
            SetProgress(1.0f, $"Finished exporting {sceneObjectDescriptor.InternalID}");
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
                RefreshReportRootMetadata();
            }

            {
                SDKServerManager.VerifyAssetGuid(blueprintDescriptor);
            }

            MarkSceneDirty(blueprintDescriptor);

            var roomDescriptor = blueprintDescriptor.GetMTIONSDKRoom();
            if (roomDescriptor == null)
            {
                if (blueprintDescriptor.TryResolveRoomScenePath(out string resolvedRoomScenePath, out string roomResolveError))
                {
                    EditorSceneManager.OpenScene(resolvedRoomScenePath, OpenSceneMode.Additive);
                    roomDescriptor = blueprintDescriptor.GetMTIONSDKRoom();
                }

                if (roomDescriptor == null)
                {
                    throw new InvalidOperationException(roomResolveError ?? "Blueprint room scene is not loaded or could not be resolved.");
                }
            }
            {
                SDKServerManager.VerifyAssetGuid(roomDescriptor);
            }

            var environmentDescriptor = blueprintDescriptor.GetMTIONSDKEnvironment();
            if (environmentDescriptor == null)
            {
                if (blueprintDescriptor.TryResolveEnvironmentScenePath(out string resolvedEnvironmentScenePath, out string environmentResolveError))
                {
                    EditorSceneManager.OpenScene(resolvedEnvironmentScenePath, OpenSceneMode.Additive);
                    environmentDescriptor = blueprintDescriptor.GetMTIONSDKEnvironment();
                }

                if (environmentDescriptor == null)
                {
                    throw new InvalidOperationException(environmentResolveError ?? "Blueprint environment scene is not loaded or could not be resolved.");
                }
            }
            {
                var (resource, updated) = SDKServerManager.VerifyAssetGuid(environmentDescriptor);

                if (updated)
                {
                    roomDescriptor.EnvironmentInternalID = environmentDescriptor.GUID;
                    EditorUtility.SetDirty(roomDescriptor);
                }
            }

            roomDescriptor.Name = $"{blueprintDescriptor.Name}_Room";
            roomDescriptor.LocationOption = blueprintDescriptor.LocationOption;
            roomDescriptor.EnvironmentInternalID = environmentDescriptor.InternalID;
            PrepareExportTimestamps(roomDescriptor);

            environmentDescriptor.Name = $"{blueprintDescriptor.Name}_Environment";
            environmentDescriptor.LocationOption = blueprintDescriptor.LocationOption;
            PrepareExportTimestamps(environmentDescriptor);

            EditorSceneManager.SaveOpenScenes();

            if (!blueprintDescriptor.TryResolveRoomScenePath(out string roomScenePath, out string resolvedRoomSceneError))
            {
                throw new InvalidOperationException(resolvedRoomSceneError);
            }

            if (!blueprintDescriptor.TryResolveEnvironmentScenePath(out string environmentScenePath, out string environmentResolveErrorAfterVerify))
            {
                throw new InvalidOperationException(environmentResolveErrorAfterVerify);
            }

            var roomScene = EditorSceneManager.GetSceneByPath(roomScenePath);
            if (!roomScene.IsValid() || !roomScene.isLoaded)
            {
                roomScene = EditorSceneManager.OpenScene(roomScenePath, OpenSceneMode.Additive);
            }
            EditorSceneManager.SetActiveScene(roomScene);
            ExportRoomScene(roomDescriptor);

            blueprintDescriptor = sceneObjectDescriptor as MTIONSDKBlueprint;
            environmentDescriptor = blueprintDescriptor.GetMTIONSDKEnvironment();
            var environmentScene = EditorSceneManager.GetSceneByPath(environmentScenePath);
            if (!environmentScene.IsValid() || !environmentScene.isLoaded)
            {
                environmentScene = EditorSceneManager.OpenScene(environmentScenePath, OpenSceneMode.Additive);
            }
            EditorSceneManager.SetActiveScene(environmentScene);
            ExportEnvironmentScene(environmentDescriptor, () =>
            {
                try
                {
                    blueprintDescriptor = sceneObjectDescriptor as MTIONSDKBlueprint;
                    roomDescriptor = blueprintDescriptor.GetMTIONSDKRoom();
                    environmentDescriptor = blueprintDescriptor.GetMTIONSDKEnvironment();

                    roomScene = EditorSceneManager.GetSceneByPath(roomScenePath);
                    environmentScene = EditorSceneManager.GetSceneByPath(environmentScenePath);
                    if (roomScene.IsValid() && roomScene.isLoaded)
                    {
                        EditorSceneManager.SetActiveScene(roomScene);
                    }

                    ExportBlueprintSceneData(blueprintDescriptor, roomDescriptor, environmentDescriptor);

                    roomScene = EditorSceneManager.GetSceneByPath(roomScenePath);
                    if (roomScene.IsValid() && roomScene.isLoaded)
                    {
                        EditorSceneManager.SetActiveScene(roomScene);
                    }

                    CompleteExport();
                }
                catch (Exception ex)
                {
                    CompleteExport(ex);
                }
            });

        }

        private void MarkSceneDirty(MTIONSDKDescriptorSceneBase sceneBase)
        {
            var baseDirectory = Path.Combine(SDKUtil.GetSDKBlueprintDirectory(sceneBase.LocationOption), sceneBase.GUID);
            if (!Directory.Exists(baseDirectory))
            {
                Directory.CreateDirectory(baseDirectory);
            }

            if (sceneBase is MTIONSDKBlueprint blueprintDescriptor)
            {
                CleanupUnexpectedBlueprintArtifacts(baseDirectory);
                SDKExportUtility.WriteBlueprintMetaCache(blueprintDescriptor, GetThumbnailId(sceneBase));
            }
        }

        private static void CleanupUnexpectedBlueprintArtifacts(string blueprintDirectory)
        {
            foreach (string directoryName in new[] { "unity", "webgl", "lightdata" })
            {
                string directoryPath = Path.Combine(blueprintDirectory, directoryName).Replace('\\', '/');
                if (Directory.Exists(directoryPath))
                {
                    Directory.Delete(directoryPath, true);
                }
            }

            foreach (string pattern in new[] { "MSDK-*.json", "config.json", "catalog*.json", "catalog*.hash", "catalog*.bin", "*.bundle", "unity.zip", ".temp_access" })
            {
                foreach (string filePath in Directory.GetFiles(blueprintDirectory, pattern, SearchOption.TopDirectoryOnly))
                {
                    File.Delete(filePath);
                }
            }
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

            SetProgress(0.0f, $"Exporting room {roomDescriptor.InternalID}");

            ExportRoomSceneData(roomDescriptor);
            SetProgress(0.3f, $"Exported room scene data for {roomDescriptor.InternalID}");


            var allAssets = GameObject.FindObjectsOfType<MVirtualAssetTracker>();
            var assetGuidsToExport = AssetComparisonUtil.FilterDuplicateAssets(allAssets, roomDescriptor.LocationOption)
                .Select(tracker => tracker.GUID).ToList();
            if (assetGuidsToExport.Count == 0)
            {
                SetProgress(1.0f, $"Finished exporting room {roomDescriptor.InternalID}");
                return;
            }

            var exportDelta = 0.7f / assetGuidsToExport.Count;

            for (int i = 0; i < assetGuidsToExport.Count; i++)
            {
                var asset = GameObject.FindObjectsOfType<MVirtualAssetTracker>()
                    .Where(tracker => tracker.GUID == assetGuidsToExport[i]).First();

                SetProgress(exportPercentComplete, $"Exporting linked asset {asset.InternalID}");
                ExportVirtualAssetData(asset);
                SetProgress(Mathf.Min(1.0f, exportPercentComplete + exportDelta), $"Exported linked asset {asset.InternalID}");
            }

            SetProgress(1.0f, $"Finished exporting room {roomDescriptor.InternalID}");
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

            SetProgress(0.5f, $"Exporting environment {environmentDescriptor.InternalID}");

            ExportLightingData(() =>
            {
                try
                {
                    ExportEnvironmentSceneData(environmentDescriptor);
                    SetProgress(1.0f, $"Finished exporting environment {environmentDescriptor.InternalID}");
                    onComplete?.Invoke();

                    if (sceneObjectDescriptor.ObjectType == MTIONObjectType.MTIONSDK_ENVIRONMENT)
                    {
                        CompleteExport();
                    }
                }
                catch (Exception ex)
                {
                    CompleteExport(ex);
                }
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
                CreateTime = blueprintDescriptor.CreateTimeMS,
                UpdateTime = blueprintDescriptor.UpdateTimeMS,
            };
            var blueprintThumbnailId = GetThumbnailId(blueprintDescriptor);

            var roomThumbnailId = GetThumbnailId(roomDescriptor);
            if (!string.IsNullOrEmpty(roomThumbnailId))
            {
                var roomThumbnailPath = Path.Combine(SDKUtil.GetSDKThumbnailDirectory(blueprintDescriptor.LocationOption), roomThumbnailId, $"{roomThumbnailId}.png");
                if (SafeFileIO.Exists(roomThumbnailPath))
                {
                    var bpThumbnailDir = Path.Combine(SDKUtil.GetSDKThumbnailDirectory(blueprintDescriptor.LocationOption), blueprintThumbnailId);
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
            SDKExportUtility.WriteBlueprintMetaCache(blueprintDescriptor, blueprintThumbnailId);

            WriteManifestForBlueprintExport(
                blueprintDescriptor,
                blueprintPath,
                blueprintDataPath,
                blueprintLocalDataPath,
                Path.Combine(SDKUtil.GetSDKThumbnailDirectory(blueprintDescriptor.LocationOption), blueprintThumbnailId, $"{blueprintThumbnailId}.png"),
                new MTIONSDKDescriptorSceneBase[] { roomDescriptor, environmentDescriptor });
        }

        private void ExportRoomSceneData(MTIONSDKDescriptorSceneBase sceneBase)
        {
            string internalId = sceneBase.InternalID;
            CreateAssetExportBackup(sceneBase, sceneBase.LocationOption);

            try
            {
                var thumbnailId = sceneBase.GUID;
                var thumbnailDir = Path.Combine(SDKUtil.GetSDKThumbnailDirectory(sceneBase.LocationOption), thumbnailId);
                if (!Directory.Exists(thumbnailDir))
                {
                    Directory.CreateDirectory(thumbnailDir);
                }
                if (!ThumbnailGenerator.TryGenerateSceneThumbnail(sceneBase, thumbnailDir, $"{thumbnailId}.png", out string thumbnailDiagnostic))
                {
                    throw new InvalidOperationException($"Thumbnail generation failed for {sceneBase.InternalID}. {thumbnailDiagnostic}");
                }

                var addressablesExporter = new AssetExporter(sceneBase, sceneBase.LocationOption);
                string guid = sceneBase.GUID;
                addressablesExporter.ExportSDKAsset();

                if (sceneBase == null)
                {
                    sceneBase = GameObject.FindObjectsOfType<MTIONSDKDescriptorSceneBase>()
                        .Where(descriptor => descriptor.GUID == guid).First();
                }

                string descriptorFilePath = CreateDescriptorFile(sceneBase);

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
                string configFilePath = WriteConfigurationFile(basePersistentDirectory, configData);
                SDKExportUtility.WriteAssetMetaCache(sceneBase, sceneBase.LocationOption, thumbnailId);
                WriteManifestForDescriptorExport(sceneBase, basePersistentDirectory, descriptorFilePath, configFilePath, thumbnailId);
            }
            catch (Exception ex)
            {
                DeleteAssetExport(sceneBase, sceneBase.LocationOption);
                RestoreAssetExportBackup(sceneBase, sceneBase.LocationOption);
                DeleteAssetExportBackup(sceneBase, sceneBase.LocationOption);
                throw new Exception($"Error while exporting ROOM {internalId}, restored backup: {ex}");
            }
            finally
            {
                DeleteAssetExportBackup(sceneBase, sceneBase.LocationOption);
            }
        }

        private void ExportEnvironmentSceneData(MTIONSDKDescriptorSceneBase sceneBase)
        {
            string internalId = sceneBase.InternalID;
            CreateAssetExportBackup(sceneBase, sceneBase.LocationOption);

            try
            {
                var thumbnailId = sceneBase.GUID;
                var thumbnailDir = Path.Combine(SDKUtil.GetSDKThumbnailDirectory(sceneBase.LocationOption), thumbnailId);
                if (!Directory.Exists(thumbnailDir))
                {
                    Directory.CreateDirectory(thumbnailDir);
                }
                if (!ThumbnailGenerator.TryGenerateSceneThumbnail(sceneBase, thumbnailDir, $"{thumbnailId}.png", out string thumbnailDiagnostic))
                {
                    throw new InvalidOperationException($"Thumbnail generation failed for {sceneBase.InternalID}. {thumbnailDiagnostic}");
                }

                var addressablesExporter = new AssetExporter(sceneBase, sceneBase.LocationOption);
                string guid = sceneBase.GUID;
                addressablesExporter.ExportSDKAsset();

                if (sceneBase == null)
                {
                    sceneBase = GameObject.FindObjectsOfType<MTIONSDKDescriptorSceneBase>()
                        .Where(descriptor => descriptor.GUID == guid).First();
                }

                string descriptorFilePath = CreateDescriptorFile(sceneBase);
                string configData = ConfigurationGenerator.ConvertSDKSceneToJsonString(
                    sceneBase,
                    null,
                    null,
                    null,
                    null,
                    null,
                    thumbnailId);
                string basePersistentDirectory = SDKUtil.GetSDKItemDirectory(sceneBase, sceneBase.LocationOption);
                string configFilePath = WriteConfigurationFile(basePersistentDirectory, configData);
                SDKExportUtility.WriteAssetMetaCache(sceneBase, sceneBase.LocationOption, thumbnailId);
                WriteManifestForDescriptorExport(sceneBase, basePersistentDirectory, descriptorFilePath, configFilePath, thumbnailId);
            }
            catch (Exception ex)
            {
                DeleteAssetExport(sceneBase, sceneBase.LocationOption);
                RestoreAssetExportBackup(sceneBase, sceneBase.LocationOption);
                DeleteAssetExportBackup(sceneBase, sceneBase.LocationOption);
                throw new Exception($"Error while exporting ENVIRONMENT {internalId}, restored backup: {ex}");
            }
            finally
            {
                DeleteAssetExportBackup(sceneBase, sceneBase.LocationOption);
            }
        }

        private void ExportVirtualAssetData(MTIONSDKAssetBase assetBase)
        {
            MTIONSDKAssetBase backupTarget = assetBase;
            string guid = assetBase.GUID;
            string internalId = assetBase.InternalID;
            CreateAssetExportBackup(backupTarget, LocationOptions);

            try
            {
                sceneObjectDescriptor = BuildManager.Instance.SceneDescriptorObject.GetComponent<MTIONSDKDescriptorSceneBase>();
                var basePersistentDirectory = SDKUtil.GetSDKItemDirectory(assetBase, LocationOptions);

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

                string descriptorFilePath = CreateDescriptorFile(assetBase);

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

                string configFilePath = WriteConfigurationFile(basePersistentDirectory, configData);
                SDKExportUtility.WriteAssetMetaCache(assetBase, LocationOptions);
                WriteManifestForDescriptorExport(assetBase, basePersistentDirectory, descriptorFilePath, configFilePath, null);
            }
            catch (Exception ex)
            {
                DeleteAssetExport(backupTarget, LocationOptions);
                RestoreAssetExportBackup(backupTarget, LocationOptions);
                DeleteAssetExportBackup(backupTarget, LocationOptions);
                throw new Exception($"Error while exporting ASSET {internalId}, restored backup: {ex}");
            }
            finally
            {
                DeleteAssetExportBackup(backupTarget, LocationOptions);
            }
        }

        private string CreateDescriptorFile(MTIONSDKAssetBase assetBase)
        {
            string basePersistentDirectory = SDKUtil.GetSDKItemDirectory(assetBase, LocationOptions);
            string descriptorData = JsonUtility.ToJson(assetBase);
            var descriptorFile = Path.Combine(basePersistentDirectory, $"{assetBase.InternalID}.json").Replace('\\', '/');
            if (SafeFileIO.Exists(descriptorFile))
            {
                SafeFileIO.Delete(descriptorFile);
            }
            SafeFileIO.WriteAllText(descriptorFile, descriptorData);
            return descriptorFile;
        }

        private static string WriteConfigurationFile(string basePersistentDirectory, string configFileData)
        {
            var configFile = Path.Combine(basePersistentDirectory, $"config.json").Replace('\\', '/');
            if (SafeFileIO.Exists(configFile))
            {
                SafeFileIO.Delete(configFile);
            }
            SafeFileIO.WriteAllText(configFile, configFileData);
            return configFile;
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
                localDataPath = Path.Combine(SDKUtil.GetSDKBlueprintDirectory(sceneBase.LocationOption), sceneBase.GUID, "meta.json");
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


        private void RefreshReportRootMetadata()
        {
            exportReport.RootGuid = sceneObjectDescriptor != null ? sceneObjectDescriptor.GUID : null;
            exportReport.RootInternalId = sceneObjectDescriptor != null ? sceneObjectDescriptor.InternalID : null;
            exportReport.RootType = sceneObjectDescriptor != null ? sceneObjectDescriptor.ObjectType.ToString() : null;
            exportReport.OutputDirectory = GetReportOutputDirectory();
        }

        public void PersistReport()
        {
            RefreshReportRootMetadata();
            SDKExportUtility.WriteReport(exportReport.OutputDirectory, exportReport);
        }

        public void FailReport(Exception ex)
        {
            CompleteExport(ex);
        }

        private void CompleteExport(Exception ex = null)
        {
            if (IsCompleted)
            {
                return;
            }

            if (sceneObjectDescriptor != null)
            {
                sceneObjectDescriptor.RestoreGizmosAfterBuild();
            }

            RefreshReportRootMetadata();
            exportReport.CompletedAtMS = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (ex != null)
            {
                exportReport.Succeeded = false;
                exportReport.Exception = ex.ToString();
                exportReport.Summary = $"Export failed for {sceneObjectDescriptor?.InternalID ?? "unknown"}.";
                exportReport.AddError(ex.Message);
            }
            else
            {
                exportReport.Succeeded = true;
                exportReport.Summary = $"Export completed for {sceneObjectDescriptor?.InternalID ?? "unknown"}.";
            }

            IsCompleted = true;
            ExportFinished?.Invoke(this, ex);
        }

        private void SetProgress(float progress, string status)
        {
            exportPercentComplete = Mathf.Clamp01(progress);
            EditorUtility.DisplayProgressBar("MTION SDK Export", status, exportPercentComplete);
        }

        private static void PrepareExportTimestamps(MTIONSDKAssetBase assetBase)
        {
            if (assetBase == null)
            {
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (assetBase.CreateTimeMS <= 0)
            {
                assetBase.CreateTimeMS = now;
            }

            assetBase.UpdateTimeMS = now;
            EditorUtility.SetDirty(assetBase);
        }

        private void RecordRestoredBackup(MTIONSDKAssetBase assetBase)
        {
            if (!exportReport.RestoredBackups.Contains(assetBase.InternalID))
            {
                exportReport.RestoredBackups.Add(assetBase.InternalID);
                exportReport.AddWarning($"Restored previous export backup for {assetBase.InternalID}.");
            }
        }

        private string GetReportOutputDirectory()
        {
            if (sceneObjectDescriptor == null)
            {
                return SDKUtil.GetSDKExportDirectory();
            }

            if (sceneObjectDescriptor.ObjectType == MTIONObjectType.MTIONSDK_BLUEPRINT && !string.IsNullOrWhiteSpace(sceneObjectDescriptor.GUID))
            {
                return Path.Combine(SDKUtil.GetSDKBlueprintDirectory(sceneObjectDescriptor.LocationOption), sceneObjectDescriptor.GUID).Replace('\\', '/');
            }

            if (!string.IsNullOrWhiteSpace(sceneObjectDescriptor.GUID))
            {
                return SDKUtil.GetSDKItemDirectory(sceneObjectDescriptor, sceneObjectDescriptor.LocationOption);
            }

            return Path.Combine(SDKUtil.GetSDKExportDirectory(), sceneObjectDescriptor.InternalID ?? "pending").Replace('\\', '/');
        }

        private void WriteManifestForDescriptorExport(MTIONSDKAssetBase assetBase, string baseDirectory, string descriptorFilePath, string configFilePath, string thumbnailId)
        {
            ExportManifestFile manifest = SDKExportUtility.CreateManifest(assetBase, "1.0.0");
            manifest.LocationOption = GetLocationOption(assetBase);
            SDKExportUtility.PopulateHostRequirements(
                manifest,
                assetBase is MTIONSDKDescriptorSceneBase ? GetDisplayTypesInScene() : new List<DisplayComponentType>());
            ApplyVisualScriptingManifestMetadata(manifest, assetBase);

            string buildTargetDirectory = Path.Combine(
                SDKUtil.GetSDKLocalUnityBuildPath(assetBase, GetLocationOption(assetBase)),
                SDKUtil.GetBuildTargetDirectory(SDKBuildTarget.StandaloneWindows));
            string catalogJsonPath = Path.Combine(buildTargetDirectory, "catalog.json");
            string catalogHashPath = Path.Combine(buildTargetDirectory, "catalog.hash");

            manifest.Artifacts.Add(SDKExportUtility.CreateFileArtifact(baseDirectory, "Descriptor", descriptorFilePath, true));
            manifest.Artifacts.Add(SDKExportUtility.CreateFileArtifact(baseDirectory, "Configuration", configFilePath, true));
            manifest.Artifacts.Add(SDKExportUtility.CreateDirectoryArtifact(baseDirectory, "Addressables Content", buildTargetDirectory, true, 3));
            manifest.Artifacts.Add(SDKExportUtility.CreateFileArtifact(baseDirectory, "Catalog File", catalogJsonPath, true));
            manifest.Artifacts.Add(SDKExportUtility.CreateFileArtifact(baseDirectory, "Catalog Hash", catalogHashPath, true));

            if (!string.IsNullOrWhiteSpace(thumbnailId))
            {
                string thumbnailPath = Path.Combine(SDKUtil.GetSDKThumbnailDirectory(GetLocationOption(assetBase)), thumbnailId, $"{thumbnailId}.png");
                manifest.Artifacts.Add(SDKExportUtility.CreateFileArtifact(baseDirectory, "Thumbnail", thumbnailPath, true));
            }

            if (assetBase.ObjectType == MTIONObjectType.MTIONSDK_ENVIRONMENT)
            {
                string lightdataPath = Path.Combine(
                    SDKUtil.GetSDKLocalLightdataBuildPath(assetBase, GetLocationOption(assetBase)),
                    $"{assetBase.InternalID}.bin");
                manifest.Artifacts.Add(SDKExportUtility.CreateFileArtifact(baseDirectory, "Lightdata", lightdataPath, true));
            }

            FinalizeManifest(baseDirectory, manifest);
        }

        private void ApplyVisualScriptingManifestMetadata(ExportManifestFile manifest, MTIONSDKAssetBase assetBase)
        {
            VisualScriptingInspectionReport report = InspectVisualScripting(assetBase);
            if (report == null || !report.HasVisualScripting)
            {
                return;
            }

            SDKExportUtility.AddRequirement(manifest, "feature", "UnityVisualScripting");
            foreach (string scope in report.GetOrderedScopes())
            {
                SDKExportUtility.AddRequirement(manifest, "uvs_scope", scope, false);
            }

            foreach (string warning in report.Warnings)
            {
                SDKExportUtility.AddWarning(manifest, warning);
                exportReport.AddWarning(warning);
            }
        }

        private VisualScriptingInspectionReport InspectVisualScripting(MTIONSDKAssetBase assetBase)
        {
            if (assetBase == null)
            {
                return null;
            }

            switch (assetBase.ObjectType)
            {
                case MTIONObjectType.MTIONSDK_ROOM:
                    return VisualScriptingSupportUtil.InspectSceneForExport(assetBase.gameObject.scene, VisualScriptingExportTarget.RoomScene);
                case MTIONObjectType.MTIONSDK_ENVIRONMENT:
                    return VisualScriptingSupportUtil.InspectSceneForExport(assetBase.gameObject.scene, VisualScriptingExportTarget.EnvironmentScene);
                case MTIONObjectType.MTIONSDK_AVATAR:
                case MTIONObjectType.MTIONSDK_ASSET:
                    return VisualScriptingSupportUtil.InspectGameObjectForExport(assetBase.ObjectReferenceProp, VisualScriptingExportTarget.PortablePrefab);
                default:
                    return null;
            }
        }

        private void WriteManifestForBlueprintExport(MTIONSDKBlueprint blueprintDescriptor, string baseDirectory, string blueprintDataPath, string blueprintLocalDataPath, string thumbnailPath, IEnumerable<MTIONSDKDescriptorSceneBase> linkedDescriptors)
        {
            ExportManifestFile manifest = SDKExportUtility.CreateManifest(blueprintDescriptor, MTIONSDKBlueprint.CurrentFormatVersion);
            SDKExportUtility.PopulateHostRequirements(manifest, GetDisplayTypesInScene());
            manifest.LinkedScenePaths.Clear();

            if (blueprintDescriptor.TryResolveRoomScenePath(out string roomScenePath, out _))
            {
                manifest.LinkedScenePaths.Add(roomScenePath);
            }
            if (blueprintDescriptor.TryResolveEnvironmentScenePath(out string environmentScenePath, out _))
            {
                manifest.LinkedScenePaths.Add(environmentScenePath);
            }

            manifest.Artifacts.Add(SDKExportUtility.CreateFileArtifact(baseDirectory, "Blueprint Data", blueprintDataPath, true));
            manifest.Artifacts.Add(SDKExportUtility.CreateFileArtifact(baseDirectory, "Blueprint Local Data", blueprintLocalDataPath, true));

            if (!string.IsNullOrWhiteSpace(thumbnailPath))
            {
                manifest.Artifacts.Add(SDKExportUtility.CreateFileArtifact(baseDirectory, "Blueprint Thumbnail", thumbnailPath, false));
            }

            foreach (MTIONSDKDescriptorSceneBase linkedDescriptor in linkedDescriptors)
            {
                if (linkedDescriptor != null)
                {
                    SDKExportUtility.AddRequirement(manifest, "linked_asset", linkedDescriptor.GUID, true);
                }
            }

            FinalizeManifest(baseDirectory, manifest);
        }

        private void FinalizeManifest(string baseDirectory, ExportManifestFile manifest)
        {
            if (!SDKExportUtility.VerifyArtifacts(manifest, baseDirectory, out List<string> missingArtifacts))
            {
                foreach (string missingArtifact in missingArtifacts)
                {
                    exportReport.AddError($"Missing required export artifact: {missingArtifact}");
                }

                foreach (ExportManifestArtifact artifact in manifest.Artifacts)
                {
                    if (!artifact.Required || artifact.Exists)
                    {
                        continue;
                    }

                    string absolutePath = SDKExportUtility.GetAbsolutePath(baseDirectory, artifact.RelativePath);
                    string diagnosticDirectory = artifact.IsDirectory
                        ? absolutePath
                        : Path.GetDirectoryName(absolutePath);
                    exportReport.AddError($"Artifact diagnostic for {artifact.Label}: {SDKExportUtility.DescribeDirectory(diagnosticDirectory)}");
                }

                throw new InvalidOperationException($"Export verification failed for {manifest.InternalID}: {string.Join(", ", missingArtifacts)}");
            }

            string manifestPath = SDKExportUtility.WriteManifest(baseDirectory, manifest);
            exportReport.AddInfo($"Wrote export manifest: {manifestPath}");
            MergeArtifactsIntoReport(manifest.Artifacts);
        }

        private void MergeArtifactsIntoReport(IEnumerable<ExportManifestArtifact> artifacts)
        {
            foreach (ExportManifestArtifact artifact in artifacts)
            {
                bool alreadyTracked = exportReport.Artifacts.Exists(existingArtifact =>
                    string.Equals(existingArtifact.RelativePath, artifact.RelativePath, StringComparison.OrdinalIgnoreCase));
                if (!alreadyTracked)
                {
                    exportReport.Artifacts.Add(artifact);
                }
            }
        }

        private List<DisplayComponentType> GetDisplayTypesInScene()
        {
            return GameObject.FindObjectsOfType<MVirtualDisplayTracker>()
                .Select(tracker => tracker.DisplayParams.DisplayType)
                .ToList();
        }

        private ExportLocationOptions GetLocationOption(MTIONSDKAssetBase assetBase)
        {
            return assetBase is MTIONSDKDescriptorSceneBase descriptorSceneBase
                ? descriptorSceneBase.LocationOption
                : LocationOptions;
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
            RecordRestoredBackup(assetBase);
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
