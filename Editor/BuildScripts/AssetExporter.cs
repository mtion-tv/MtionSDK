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
                CreateAddressableAssetScene();
            }
            else
            {
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
            var dir = SDKUtil.GetAssetPrefabDirectory();
            Directory.CreateDirectory(dir);

            var roomPrefabPath = Path.Combine(dir, $"{assetBase.InternalID}.prefab").Replace('\\', '/');
            assetBase.AddressableID = roomPrefabPath;

            AssetDatabase.DeleteAsset(roomPrefabPath);

            PrefabUtility.SaveAsPrefabAsset(assetBase.ObjectReferenceProp, roomPrefabPath);
            AssetDatabase.SaveAssets();
        }

        public void CreateAddressableAssetScene()
        {
            var dir = SDKUtil.GetSceneDirectory();
            Directory.CreateDirectory(dir);


            var envScenePath = Path.Combine(dir, $"{assetBase.InternalID}.unity").Replace('\\', '/');
            assetBase.AddressableID = envScenePath;

            var scenePath = assetBase.ObjectReferenceProp.scene.path;
            AssetDatabase.DeleteAsset(envScenePath);

            if (!AssetDatabase.CopyAsset(scenePath, envScenePath))
                Debug.LogWarning($"Failed to copy {envScenePath}");
        }

        public void CreateAddressablesGroupForExport()
        {
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


            string localBuildRoot = SDKUtil.GetSDKLocalUnityBuildPath(assetBase, exportLocationOptions);
            string loadRoot = SDKUtil.LOAD_URL;

            compiled.AWSUtil.SDKState = compiled.ProductionLevel.Development;
            string remoteTarget = compiled.AWSUtil.ServerEndpoint;

            profile.SetValue(profileId, AddressableAssetSettings.kLocalBuildPath, $"{localBuildRoot}/[BuildTarget]");
            profile.SetValue(profileId, AddressableAssetSettings.kLocalLoadPath, loadRoot);

            profile.SetValue(profileId, AddressableAssetSettings.kRemoteBuildPath,
                $"ServerData/Rooms/{assetBase.InternalID}/[BuildTarget]");
            profile.SetValue(profileId, AddressableAssetSettings.kRemoteLoadPath,
                $"{remoteTarget}Rooms/{assetBase.InternalID}/[BuildTarget]");

            var variantGroup = settings.FindGroup(profileName);
            if (variantGroup == null)
            {
                variantGroup = settings.CreateGroup(profileName, false, false, false, null);
                var bundleSchema = variantGroup.AddSchema<BundledAssetGroupSchema>();
                var updateSchema = variantGroup.AddSchema<ContentUpdateGroupSchema>();

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

                string sceneAAId = assetBase.AddressableID;
                string sceneEntryGUID = AssetDatabase.AssetPathToGUID(sceneAAId);
                AddressableAssetEntry sceneAssetEntry = settings.CreateOrMoveEntry(sceneEntryGUID, variantGroup, false, false);
                sceneAssetEntry.address = sceneAAId;

                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, sceneAssetEntry, true);

                if (assetBase.ObjectType == MTIONObjectType.MTIONSDK_ENVIRONMENT)
                {
                    string lightAAId = SDKUtil.GetLighmapAddressableId(assetBase);
                    string lightGUID = AssetDatabase.AssetPathToGUID(lightAAId);
                    if (lightGUID.Length > 0)
                    {
                        AddressableAssetEntry lightmapAssetEntry =
                            settings.CreateOrMoveEntry(lightGUID, variantGroup, false, false);
                        lightmapAssetEntry.address = lightAAId;


                        settings.SetDirty(
                            AddressableAssetSettings.ModificationEvent.EntryMoved,
                            lightmapAssetEntry,
                            true);
                    }
                }

            }
            else
            {
                string sceneAAId = assetBase.AddressableID;
                string sceneEntryGUID = AssetDatabase.AssetPathToGUID(sceneAAId);
                var sceneAssetEntry = settings.FindAssetEntry(sceneEntryGUID);

                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, sceneAssetEntry, true);

                if (assetBase.ObjectType == MTIONObjectType.MTIONSDK_ENVIRONMENT)
                {
                    string lightAAId = SDKUtil.GetLighmapAddressableId(assetBase);
                    string lightGUID = AssetDatabase.AssetPathToGUID(lightAAId);
                    AddressableAssetEntry lightmapAssetEntry = settings.CreateOrMoveEntry(lightGUID, variantGroup, false, false);

                    settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, lightmapAssetEntry, true);
                }
            }

            settings.activeProfileId = defaultProfileID;
        }


        public void RemoveAddressableGroup()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;

            AddressableAssetProfileSettings profile = settings.profileSettings;
            string profileName = SDKUtil.GetAddressableGroupName(assetBase);
            string profileId = profile.GetProfileId(profileName);
            if (!string.IsNullOrEmpty(profileId))
            {
                profile.RemoveProfile(profileId);
            }

            string entryGUID = AssetDatabase.AssetPathToGUID(assetBase.AddressableID);
            var entry = settings.FindAssetEntry(entryGUID);
            if (entry != null)
            {
                settings.RemoveAssetEntry(entryGUID);
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryRemoved, entry, true);
            }

            var variantGroup = settings.FindGroup(SDKUtil.GetAddressableGroupName(assetBase));
            if (variantGroup != null)
            {
                settings.RemoveGroup(variantGroup);
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupRemoved, entry, true);
            }

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
            var localUnityDirectory = SDKUtil.GetSDKLocalUnityBuildPath(assetBase, exportLocationOptions);
            if (Directory.Exists(localUnityDirectory))
            {
                Directory.Delete(localUnityDirectory, true);
                Directory.CreateDirectory(localUnityDirectory);
            }

            string defaultProfileID = AddressableAssetSettingsDefaultObject.Settings.activeProfileId;

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            settings.BuildRemoteCatalog = true;
            settings.DisableCatalogUpdateOnStartup = true;
            settings.ContiguousBundles = true;
            settings.IgnoreUnsupportedFilesInBuild = true;
            settings.ShaderBundleNaming = ShaderBundleNaming.DefaultGroupGuid;
            settings.MonoScriptBundleNaming = MonoScriptBundleNaming.DefaultGroupGuid;

            string groupProfileName = SDKUtil.GetAddressableGroupName(assetBase);

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


            Dictionary<string, bool> buildStatusMap = new Dictionary<string, bool>();
            settings.groups.ForEach(group =>
            {
                if (group.ReadOnly) return;
                buildStatusMap.Add(group.name, group.GetSchema<BundledAssetGroupSchema>().IncludeInBuild);
            });

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

                settings.groups.ForEach(group =>
                {
                    if (group.ReadOnly) return;

                    group.GetSchema<BundledAssetGroupSchema>().IncludeInBuild = group.name == groupProfileName;

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

            var exportOptions = new ExportOptions { TexturePathRetriever = GLTFExportMenu.RetrieveTexturePath };

            var originalPosition = assetBase.ObjectReferenceProp.transform.position;
            var originalRotation = assetBase.ObjectReferenceProp.transform.rotation;
            var originalScale = assetBase.ObjectReferenceProp.transform.localScale;

            assetBase.ObjectReferenceProp.transform.position = Vector3.zero;
            assetBase.ObjectReferenceProp.transform.rotation = Quaternion.identity;
            assetBase.ObjectReferenceProp.transform.localScale = Vector3.one;

            var transforms = new Transform[] { assetBase.ObjectReferenceProp.transform };
            var exporter = new GLTFSceneExporter(transforms, exportOptions);

            GLTFSceneExporter.SaveFolderPath = localWebGLDirectory;
            exporter.SaveGLB(localWebGLDirectory, fileName);

            assetBase.ObjectReferenceProp.transform.position = originalPosition;
            assetBase.ObjectReferenceProp.transform.rotation = originalRotation;
            assetBase.ObjectReferenceProp.transform.localScale = originalScale;

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

            var lightmapFilePath = $"{localLightdataDirectory}/{assetBase.InternalID}.bin";
            var unityLightdataDirectory = SDKUtil.GetAssetLightmapDirectory(assetBase);
            if (Directory.Exists(unityLightdataDirectory))
            {
                ZipFile.CreateFromDirectory(unityLightdataDirectory, lightmapFilePath);
            }

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
