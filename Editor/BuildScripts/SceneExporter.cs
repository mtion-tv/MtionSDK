using mtion.room.sdk.compiled;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace mtion.room.sdk
{
    public class SceneExporter
    {
        private MTIONSDKDescriptorSceneBase sceneObjectDescriptor;
        private UserSdkAuthentication userAuthentication;
        private float exportPercentComplete;

        private ExportLocationOptions LocationOptions => sceneObjectDescriptor.LocationOption;
        public float ExportPercentComplete => exportPercentComplete;

        public SceneExporter(MTIONSDKDescriptorSceneBase sceneObjectDescriptor, UserSdkAuthentication userAuthentication)
        {
            this.sceneObjectDescriptor = sceneObjectDescriptor;
            this.userAuthentication = userAuthentication;
        }

        public void ExportSDKScene()
        {
            if (sceneObjectDescriptor == null)
            {
                throw new ArgumentNullException();
            }

            MTIONObjectType objectType = sceneObjectDescriptor.ObjectType;
            switch (objectType)
            {
                case MTIONObjectType.MTIONSDK_ASSET:
                    ExportAssetScene();
                    break;

                case MTIONObjectType.MTIONSDK_ROOM:
                    ExportRoomScene();
                    break;

                case MTIONObjectType.MTIONSDK_ENVIRONMENT:
                    ExportEnvironmentScene();
                    break;
            }
        }

        private void ExportAssetScene()
        {
            SceneVerificationUtil.VerifySceneIntegrity(sceneObjectDescriptor);

            // Export Asset Scene
            ExportVirtualAssetData(sceneObjectDescriptor);

            // Create asset output directory
            var myDocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var assetOutputDirectory = Path.Combine(myDocumentsPath, "MTIONAssets");
            if (!Directory.Exists(assetOutputDirectory))
            {
                Directory.CreateDirectory(assetOutputDirectory);
            }

            // Create asset file
            var assetFile = Path.Combine(assetOutputDirectory, $"{sceneObjectDescriptor.Name}.mtion");
            if (File.Exists(assetFile))
            {
                File.Delete(assetFile);
            }

            // Export as archive file
            var basePresistentDirector = SDKUtil.GetSDKItemDirectory(sceneObjectDescriptor, LocationOptions);
            ZipFile.CreateFromDirectory(basePresistentDirector, assetFile, System.IO.Compression.CompressionLevel.Optimal, true);
        }


        private void ExportRoomScene()
        {
            // Prepare scene for export
            ComponentVerificationUtil.VerifyAllComponentsIntegrity(sceneObjectDescriptor, MTIONObjectType.MTIONSDK_CAMERA);
            ComponentVerificationUtil.VerifyAllComponentsIntegrity(sceneObjectDescriptor, MTIONObjectType.MTIONSDK_DISPLAY);
            ComponentVerificationUtil.VerifyAllComponentsIntegrity(sceneObjectDescriptor, MTIONObjectType.MTIONSDK_LIGHT);
            ComponentVerificationUtil.VerifyAllComponentsIntegrity(sceneObjectDescriptor, MTIONObjectType.MTIONSDK_ASSET);

            // Find All SDK Components
            MVirtualCameraEventTracker[] cameraTrackers = GameObject.FindObjectsOfType<MVirtualCameraEventTracker>();
            MVirtualDisplayTracker[] displayTrackers = GameObject.FindObjectsOfType<MVirtualDisplayTracker>();
            MVirtualAssetTracker[] assetTrackers = GameObject.FindObjectsOfType<MVirtualAssetTracker>();
            MVirtualLightingTracker[] lightTrackers = GameObject.FindObjectsOfType<MVirtualLightingTracker>();

            VerifyAndSaveSDKComponents(cameraTrackers, displayTrackers, lightTrackers, assetTrackers);

            //////////////////////////////
            // Export Main Room Scene
            exportPercentComplete = 0.0f;

            ExportRoomSceneData(sceneObjectDescriptor);
            exportPercentComplete += 0.3f;

            //////////////////////////////
            // Export Virtual Assets in Scene

            var allAssets = GameObject.FindObjectsOfType<MVirtualAssetTracker>();
            var assetGuidsToExport = AssetComparisonUtil.FilterDuplicateAssets(allAssets, sceneObjectDescriptor.LocationOption)
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


        private void ExportEnvironmentScene()
        {
            //////////////////////////////
            // Export Main Room Scene (ordering important)
            exportPercentComplete = 0.5f;

            // 1. Export all lightmap data and store as assets in file system. 
            ExportLightingData(() =>
            {
                // 2. Export all scene data
                ExportEnvironmentSceneData(sceneObjectDescriptor);
                exportPercentComplete = 1.0f;
            });


            // Export all lighting data associated with the scene
            void ExportLightingData(Action onComplete)
            {
                Lightmapper lightmapper = Lightmapper.UnityLightmapper;
#if BAKERY_INCLUDED
                lightmapper = Lightmapper.BakeryLightmapper;
#endif
                MLightmapBuildManager.StartStoringProcess(sceneObjectDescriptor, lightmapper, onComplete);

            }
        }

        private void ExportRoomSceneData(MTIONSDKDescriptorSceneBase sceneBase)
        {
            CreateAssetExportBackup(sceneBase, sceneBase.LocationOption);

            try
            {
                // TODO: Enhance screenshot (make more aesthetically pleasing)
                // Take camera snapshot
                var camera = sceneObjectDescriptor.gameObject.GetComponentInChildren<Camera>();
                var basePersistentDirectory = SDKUtil.GetSDKItemDirectory(sceneBase, sceneBase.LocationOption);
                ThumbnailGenerator.TakeSnapshotOfAssetInCurrentScene(camera, basePersistentDirectory);

                var addressablesExporter = new AssetExporter(sceneBase, sceneBase.LocationOption);
                addressablesExporter.ExportSDKAsset();

                CreateDescriptorFile(sceneBase);

                MVirtualCameraEventTracker[] cameraTrackers = GameObject.FindObjectsOfType<MVirtualCameraEventTracker>();
                MVirtualDisplayTracker[] displayTrackers = GameObject.FindObjectsOfType<MVirtualDisplayTracker>();
                MVirtualAssetTracker[] assetTrackers = GameObject.FindObjectsOfType<MVirtualAssetTracker>();
                MVirtualLightingTracker[] lightTrackers = GameObject.FindObjectsOfType<MVirtualLightingTracker>();
                string configData = ConfigurationGenerator.ConvertSDKSceneToJsonString(
                    sceneBase,
                    userAuthentication,
                    cameraTrackers,
                    displayTrackers,
                    lightTrackers,
                    assetTrackers);
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
                // TODO: Enhance screenshot (make more aesthetically pleasing)
                //Take camera snapshot
                var camera = sceneObjectDescriptor.gameObject.GetComponentInChildren<Camera>();
                string basePersistentDirectory = SDKUtil.GetSDKItemDirectory(sceneBase, sceneBase.LocationOption);
                ThumbnailGenerator.TakeSnapshotOfAssetInCurrentScene(camera, basePersistentDirectory);

                var addressablesExporter = new AssetExporter(sceneBase, sceneBase.LocationOption);
                addressablesExporter.ExportSDKAsset();

                CreateDescriptorFile(sceneBase);
                string configData = ConfigurationGenerator.ConvertSDKSceneToJsonString(
                    sceneBase,
                    userAuthentication,
                    null,
                    null,
                    null,
                    null);
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
                // Generate a thumbnail for the asset
                sceneObjectDescriptor = BuildManager.Instance.SceneDescriptorObject.GetComponent<MTIONSDKDescriptorSceneBase>();
                var basePersistentDirectory = SDKUtil.GetSDKItemDirectory(assetBase, LocationOptions);
                var camera = sceneObjectDescriptor.gameObject.GetComponentInChildren<Camera>();
                ThumbnailGenerator.TakeSnapshotOfAssetInCurrentScene(camera, basePersistentDirectory);

                var addressablesExporter = new AssetExporter(assetBase, LocationOptions);
                addressablesExporter.ExportSDKAsset();

                // Build process destroys and reloads scene, lossing all references to existing scene-tied GameObjects
                if (assetBase == null)
                {
                    assetBase = GameObject.FindObjectsOfType<MVirtualAssetTracker>()
                   .Where(tracker => tracker.GUID == guid).First();
                }

                CreateDescriptorFile(assetBase);

                string configData = ConfigurationGenerator.ConvertSDKAssetToJsonString(assetBase, userAuthentication);
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
            if (File.Exists(descriptorFile))
            {
                File.Delete(descriptorFile);
            }
            File.WriteAllText(descriptorFile, descriptorData);
        }

        private static void WriteConfigurationFile(string basePersistentDirectory, string configFileData)
        {
            var configFile = Path.Combine(basePersistentDirectory, $"config.json").Replace('\\', '/');
            if (File.Exists(configFile))
            {
                File.Delete(configFile);
            }
            File.WriteAllText(configFile, configFileData);
        }

        private static void VerifyAndSaveSDKComponents(
            MVirtualCameraEventTracker[] cameraTrackers,
            MVirtualDisplayTracker[] displayTrackers,
            MVirtualLightingTracker[] lightTrackers,
            MVirtualAssetTracker[] assetTrackers)
        {
            foreach (var asset in assetTrackers)
            {
                SDKEditorUtil.InitAddressableAssetFields(asset, asset.ObjectType);
            }

            LoopOverComponentTrackers(cameraTrackers);
            LoopOverComponentTrackers(displayTrackers);
            LoopOverComponentTrackers(lightTrackers);

            void LoopOverComponentTrackers(VirtualComponentTracker[] componentTrackers)
            {
                foreach (var component in componentTrackers)
                {
                    // These should be resolved in PackageInitialization migration script. Assert correctness
                    Debug.Assert(component != null);
                    Debug.Assert(component.GUID != null);
                }
            }
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
                File.Copy(newPath, newPath.Replace(sourceFolder, destinationFolder), true);
            }
        }
    }
}
