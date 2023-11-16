using mtion.room.sdk.compiled;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityGLTF;

namespace mtion.room.sdk
{
    public class AssetExporter
    {
        private MTIONSDKAssetBase assetBase;
        private ExportLocationOptions exportLocationOptions;

        public AssetExporter(MTIONSDKAssetBase assetBase, ExportLocationOptions exportLocationOptions)
        {
            this.assetBase = assetBase;
            this.exportLocationOptions = exportLocationOptions;
        }

        public void ExportSDKAsset()
        {
            if (assetBase == null)
            {
                throw new ArgumentNullException();
            }

            if (assetBase.ObjectType == MTIONObjectType.MTIONSDK_ENVIRONMENT)
            {
                // Export scene
                CreateAddressableAssetScene();
            }
            else
            {
                // Export prefab
                CreateAddressableAssetPrefab();
            }

            EditorUtility.SetDirty(assetBase);
            EditorSceneManager.SaveOpenScenes();

            CreateAddressablesGroupForExport();
            if (assetBase.ExportGLTFEnabled)
            {
                ExportAsGLTF(assetBase, exportLocationOptions, null);
            }
            if (assetBase.ObjectType == MTIONObjectType.MTIONSDK_ENVIRONMENT)
            {
                ExportLightmapData(assetBase, exportLocationOptions, null);

            }

            try
            {
                ExportAsAddressableAssetBundle(null);
            }
            finally
            {
                RemoveAddressableGroup();
            }
        }

        public void CreateAddressableAssetPrefab()
        {
            // Create base director if not present
            var dir = SDKUtil.GetAssetPrefabDirectory();
            Directory.CreateDirectory(dir);

            // Create descriptor object
            var roomPrefabPath = Path.Combine(dir, $"{assetBase.InternalID}.prefab").Replace('\\', '/');
            assetBase.AddressableID = roomPrefabPath;

            // Delete if exsits
            AssetDatabase.DeleteAsset(roomPrefabPath);

            // Create Assets
            PrefabUtility.SaveAsPrefabAsset(assetBase.ObjectReference, roomPrefabPath);
            AssetDatabase.SaveAssets();
        }

        public void CreateAddressableAssetScene()
        {
            // Create base director if not present
            var dir = SDKUtil.GetSceneDirectory();
            Directory.CreateDirectory(dir);


            // Create descriptor object
            var envScenePath = Path.Combine(dir, $"{assetBase.InternalID}.unity").Replace('\\', '/');
            assetBase.AddressableID = envScenePath;

            var scenePath = assetBase.ObjectReference.scene.path;
            AssetDatabase.DeleteAsset(envScenePath);

            if (!AssetDatabase.CopyAsset(scenePath, envScenePath))
                Debug.LogWarning($"Failed to copy {envScenePath}");
        }

        public void CreateAddressablesGroupForExport()
        {
            // Create Addressable asset
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            }

            AddressableAssetProfileSettings profile = settings.profileSettings;
            string defaultProfileID = settings.activeProfileId;
            string profileName = SDKUtil.GetAddressableGroupName(assetBase);
            string profileId = profile.GetProfileId(profileName);
            if (string.IsNullOrEmpty(profileId))
            {
                profileId = profile.AddProfile(profileName, settings.activeProfileId);
            }
            settings.activeProfileId = profileId;

            // Setup profile

            // Add build and load paths
            string localBuildRoot = SDKUtil.GetSDKLocalUnityBuildPath(assetBase, exportLocationOptions);
            string loadRoot = SDKUtil.LOAD_URL;

            // TODO: Look at production state
            compiled.AWSUtil.SDKState = compiled.ProductionLevel.Development;
            string remoteTarget = compiled.AWSUtil.ServerEndpoint;

            profile.SetValue(profileId, AddressableAssetSettings.kLocalBuildPath, $"{localBuildRoot}/[BuildTarget]");
            profile.SetValue(profileId, AddressableAssetSettings.kLocalLoadPath, loadRoot);

            // TODO: Look at this for S3
            profile.SetValue(profileId, AddressableAssetSettings.kRemoteBuildPath,
                $"ServerData/Rooms/{assetBase.InternalID}/[BuildTarget]");
            profile.SetValue(profileId, AddressableAssetSettings.kRemoteLoadPath,
                $"{remoteTarget}Rooms/{assetBase.InternalID}/[BuildTarget]");

            // Find group & create if not present
            var variantGroup = settings.FindGroup(profileName);
            if (variantGroup == null)
            {
                // Create group
                variantGroup = settings.CreateGroup(profileName, false, false, false, null);
                var bundleSchema = variantGroup.AddSchema<BundledAssetGroupSchema>();
                var updateSchema = variantGroup.AddSchema<ContentUpdateGroupSchema>();

                // ensure correct group settings
                BundledAssetGroupSchema groupSchema = variantGroup.GetSchema<BundledAssetGroupSchema>();
                groupSchema.UseAssetBundleCache = true;
                groupSchema.UseAssetBundleCrc = false;
                groupSchema.IncludeInBuild = false;
                groupSchema.RetryCount = 3;
                groupSchema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.OnlyHash;
                groupSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                groupSchema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
                groupSchema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                groupSchema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);

                // Create entry for main scene assets
                string sceneAAId = assetBase.AddressableID;
                string sceneEntryGUID = AssetDatabase.AssetPathToGUID(sceneAAId);
                AddressableAssetEntry sceneAssetEntry = settings.CreateOrMoveEntry(sceneEntryGUID, variantGroup, false, false);
                sceneAssetEntry.address = sceneAAId;
                //entry.SetLabel("", true, true, false);

                // Set scene assets entry
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, sceneAssetEntry, true);

                //  Add lightmap data entry
                if (assetBase.ObjectType == MTIONObjectType.MTIONSDK_ENVIRONMENT)
                {
                    // Create entry for main scene assets\
                    // TODO: Add this directly to config file
                    string lightAAId = SDKUtil.GetLighmapAddressableId(assetBase);
                    string lightGUID = AssetDatabase.AssetPathToGUID(lightAAId);
                    if (lightGUID.Length > 0)
                    {
                        AddressableAssetEntry lightmapAssetEntry =
                            settings.CreateOrMoveEntry(lightGUID, variantGroup, false, false);
                        lightmapAssetEntry.address = lightAAId;

                        //entry.SetLabel("", true, true, false);

                        // Set scene assets entry
                        settings.SetDirty(
                            AddressableAssetSettings.ModificationEvent.EntryMoved,
                            lightmapAssetEntry,
                            true);
                    }
                }

            }
            else
            {
                // Scene data
                string sceneAAId = assetBase.AddressableID;
                string sceneEntryGUID = AssetDatabase.AssetPathToGUID(sceneAAId);
                var sceneAssetEntry = settings.FindAssetEntry(sceneEntryGUID);
                // TODO: Update schema for group

                // Add entry
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, sceneAssetEntry, true);

                // Lightdata
                if (assetBase.ObjectType == MTIONObjectType.MTIONSDK_ENVIRONMENT)
                {
                    string lightAAId = SDKUtil.GetLighmapAddressableId(assetBase);
                    string lightGUID = AssetDatabase.AssetPathToGUID(lightAAId);
                    AddressableAssetEntry lightmapAssetEntry = settings.CreateOrMoveEntry(lightGUID, variantGroup, false, false);

                    // Set scene assets entry
                    settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, lightmapAssetEntry, true);
                }
            }

            // Reset profile
            settings.activeProfileId = defaultProfileID;
        }


        public void RemoveAddressableGroup()
        {
            // Remove all Addresssable asset settings
            var settings = AddressableAssetSettingsDefaultObject.Settings;

            // Remove Profile 
            AddressableAssetProfileSettings profile = settings.profileSettings;
            string profileName = SDKUtil.GetAddressableGroupName(assetBase);
            string profileId = profile.GetProfileId(profileName);
            if (!string.IsNullOrEmpty(profileId))
            {
                profile.RemoveProfile(profileId);
            }

            // Remove Entry
            string entryGUID = AssetDatabase.AssetPathToGUID(assetBase.AddressableID);
            var entry = settings.FindAssetEntry(entryGUID);
            if (entry != null)
            {
                settings.RemoveAssetEntry(entryGUID);
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryRemoved, entry, true);
            }

            // Remove Group
            var variantGroup = settings.FindGroup(SDKUtil.GetAddressableGroupName(assetBase));
            if (variantGroup != null)
            {
                settings.RemoveGroup(variantGroup);
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupRemoved, entry, true);
            }

            // Reset Bundle Names
            settings.ShaderBundleCustomNaming = "";
            settings.MonoScriptBundleCustomNaming = "";
            settings.BuildRemoteCatalog = false;
            settings.DisableCatalogUpdateOnStartup = false;
            settings.ContiguousBundles = false;
            settings.IgnoreUnsupportedFilesInBuild = false;
            settings.ShaderBundleNaming = ShaderBundleNaming.ProjectName;
            settings.MonoScriptBundleNaming = MonoScriptBundleNaming.Disabled;

            AssetDatabase.SaveAssets();
        }

        public void ExportAsAddressableAssetBundle(Action onExportComplete)
        {
            // Get Path
            var localUnityDirectory = SDKUtil.GetSDKLocalUnityBuildPath(assetBase, exportLocationOptions);
            if (Directory.Exists(localUnityDirectory))
            {
                Directory.Delete(localUnityDirectory, true);
                Directory.CreateDirectory(localUnityDirectory);
            }

            // Look at this: https://github.com/WetzoldStudios/traVRsal-sdk/blob/master/Editor/PublishUI.cs#L371
            // Generate addressable build for local unity project
            string defaultProfileID = AddressableAssetSettingsDefaultObject.Settings.activeProfileId;

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            settings.BuildRemoteCatalog = true;
            settings.DisableCatalogUpdateOnStartup = true;
            settings.ContiguousBundles = true;
            settings.IgnoreUnsupportedFilesInBuild = true;
            settings.ShaderBundleNaming = ShaderBundleNaming.DefaultGroupGuid;
            settings.MonoScriptBundleNaming = MonoScriptBundleNaming.DefaultGroupGuid;

            string groupProfileName = SDKUtil.GetAddressableGroupName(assetBase);

            // Set main build target
            BuildTarget mainTarget = BuildTarget.StandaloneWindows64;
            EditorUserBuildSettings.selectedStandaloneTarget = mainTarget;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x); // TODO: Should see about chaning this to il2cpp
            AssetDatabase.SaveAssets();

            List<Tuple<BuildTargetGroup, BuildTarget>> targets = new List<Tuple<BuildTargetGroup, BuildTarget>>();
            bool allTargets = true;
            if (allTargets)
            {
                targets.Add(new Tuple<BuildTargetGroup, BuildTarget>(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64));
            }
            else
            {
                targets.Add(new Tuple<BuildTargetGroup, BuildTarget>(BuildTargetGroup.Standalone, mainTarget));
            }

            // Cache current build values

            Dictionary<string, bool> buildStatusMap = new Dictionary<string, bool>();
            settings.groups.ForEach(group =>
            {
                if (group.ReadOnly) return;
                buildStatusMap.Add(group.name, group.GetSchema<BundledAssetGroupSchema>().IncludeInBuild);
            });

            // iterate over all supported platforms
            foreach (Tuple<BuildTargetGroup, BuildTarget> target in targets)
            {
                while (EditorApplication.isUpdating)
                {
                    Thread.Sleep(10);
                }

                EditorUserBuildSettings.SwitchActiveBuildTarget(target.Item1, target.Item2);

                AddressableAssetProfileSettings profile = settings.profileSettings;
                string profileId = profile.GetProfileId(groupProfileName);
                if (string.IsNullOrEmpty(profileId))
                {
                    EditorUtility.DisplayDialog("Error", $"No profile ID associated with room name.", "ok");
                    return;
                }

                settings.activeProfileId = profileId;

                // Set build to only include assets we want
                settings.groups.ForEach(group =>
                {
                    // Cache current build value
                    if (group.ReadOnly) return;

                    group.GetSchema<BundledAssetGroupSchema>().IncludeInBuild = group.name == groupProfileName;

                    // default group ensures there is no accidental local default group resulting in local paths being baked into addressable for shaders
                    if (group.name == groupProfileName && group.CanBeSetAsDefault()) settings.DefaultGroup = group;
                });

                BundledAssetGroupSchema schema = settings.groups.First(group => @group.name == groupProfileName).GetSchema<BundledAssetGroupSchema>();
                settings.RemoteCatalogBuildPath = schema.BuildPath;
                settings.RemoteCatalogLoadPath = schema.LoadPath;
                settings.ShaderBundleCustomNaming = groupProfileName;
                settings.MonoScriptBundleCustomNaming = groupProfileName;

                AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
                bool success = string.IsNullOrEmpty(result.Error);

                if (!success)
                {
                    Debug.LogError("Addressables build error encountered: " + result.Error);
                    throw new Exception(result.Error);
                }
            }

            // Rest profile
            {
                bool resetDefault = true;
                settings.activeProfileId = defaultProfileID;

                settings.groups.ForEach(group =>
                {
                    if (group.ReadOnly) return;
                    group.GetSchema<BundledAssetGroupSchema>().IncludeInBuild = buildStatusMap[group.name];

                    if (resetDefault && group.CanBeSetAsDefault())
                    {
                        settings.DefaultGroup = group;
                        resetDefault = false;
                    }
                });
            }

            AssetDatabase.SaveAssets();

            RenameCatalogs(localUnityDirectory, targets);

            onExportComplete?.Invoke();
        }

        private static void ExportAsGLTF(MTIONSDKAssetBase assetBase, ExportLocationOptions exportLocationOptions, Action onExportComplete)
        {
            // Get Path
            var baseDirector = SDKUtil.GetSDKItemDirectory(assetBase, exportLocationOptions);
            var localWebGLDirectory = SDKUtil.GetSDKLocalWebGLBuildPath(assetBase, exportLocationOptions);
            if (Directory.Exists(localWebGLDirectory))
            {
                Directory.Delete(localWebGLDirectory, true);
                Directory.CreateDirectory(localWebGLDirectory);
            }

            var fileName = $"{SDKUtil.GetGLTFGuid(assetBase)}.glb";
            var filePath = Path.Combine(localWebGLDirectory, fileName).Replace('\\', '/');
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            // UnityGLTF Export
            var exportOptions = new ExportOptions { TexturePathRetriever = GLTFExportMenu.RetrieveTexturePath };

            // Cache current position, rotation, and scale
            var originalPosition = assetBase.ObjectReference.transform.position;
            var originalRotation = assetBase.ObjectReference.transform.rotation;
            var originalScale = assetBase.ObjectReference.transform.localScale;

            // Set position, rotation, and scale to 0,0,0, 0,0,0, 1,1,1
            assetBase.ObjectReference.transform.position = Vector3.zero;
            assetBase.ObjectReference.transform.rotation = Quaternion.identity;
            assetBase.ObjectReference.transform.localScale = Vector3.one;

            // Export object
            var transforms = new Transform[] { assetBase.ObjectReference.transform };
            var exporter = new GLTFSceneExporter(transforms, exportOptions);

            GLTFSceneExporter.SaveFolderPath = localWebGLDirectory;
            exporter.SaveGLB(localWebGLDirectory, fileName);

            // Reset position
            assetBase.ObjectReference.transform.position = originalPosition;
            assetBase.ObjectReference.transform.rotation = originalRotation;
            assetBase.ObjectReference.transform.localScale = originalScale;

            onExportComplete?.Invoke();
        }

        private static void ExportLightmapData(MTIONSDKAssetBase assetBase, ExportLocationOptions exportLocationOptions, Action onExportComplete)
        {
            var localLightdataDirectory = SDKUtil.GetSDKLocalLightdataBuildPath(assetBase, exportLocationOptions);
            if (Directory.Exists(localLightdataDirectory))
            {
                Directory.Delete(localLightdataDirectory, true);
                Directory.CreateDirectory(localLightdataDirectory);
            }

            // Archive data
            var lightmapFilePath = $"{localLightdataDirectory}/{assetBase.InternalID}.bin";
            var unityLightdataDirectory = SDKUtil.GetAssetLightmapDirectory(assetBase);
            if (Directory.Exists(unityLightdataDirectory))
            {
                ZipFile.CreateFromDirectory(unityLightdataDirectory, lightmapFilePath);
            }

            // notify children
            onExportComplete?.Invoke();
        }

        private static void RenameCatalogs(string path, List<Tuple<BuildTargetGroup, BuildTarget>> targets)
        {
            if (!Directory.Exists(path)) return;

            foreach (var target in targets)
            {
                string buildTarget = SDKUtil.GetBuildTargetDirectory(target.Item2 == BuildTarget.WebGL ? SDKBuildTarget.WebGL : SDKBuildTarget.StandaloneWindows);
                string targetPath = Path.Combine(path, buildTarget).Replace('\\', '/');

                if (Directory.Exists(targetPath))
                {
                    foreach (string extension in new[] { "hash", "json" })
                    {
                        string[] files = Directory.GetFiles(targetPath, "catalog_*." + extension, SearchOption.AllDirectories);
                        foreach (string file in files)
                        {
                            string targetFile = Path.GetDirectoryName(file) + "/catalog." + extension;
                            if (File.Exists(targetFile)) FileUtil.DeleteFileOrDirectory(targetFile);
                            FileUtil.MoveFileOrDirectory(file, targetFile);
                        }
                    }
                }
            }
        }
    }
}
