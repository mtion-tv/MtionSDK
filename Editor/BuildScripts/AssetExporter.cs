using mtion.room.sdk.compiled;
using mtion.service.api;
using mtion.utility;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Reflection;
using mtion.room.sdk.visualscripting;

namespace mtion.room.sdk
{
    public class AssetExporter
    {
        private const string EnableJsonCatalogDefine = "ENABLE_JSON_CATALOG";

        private sealed class AddressablesBuildStateSnapshot
        {
            public string ActiveProfileId;
            public object RemoteCatalogBuildPath;
            public object RemoteCatalogLoadPath;
            public string BuiltInBundleCustomNaming;
            public string ShaderBundleCustomNaming;
            public string MonoScriptBundleCustomNaming;
            public object BuiltInBundleNaming;
            public object ShaderBundleNaming;
            public object MonoScriptBundleNaming;
            public bool BuildRemoteCatalog;
            public bool DisableCatalogUpdateOnStartup;
            public bool ContiguousBundles;
            public bool IgnoreUnsupportedFilesInBuild;
            public string DefaultGroupName;
            public Dictionary<string, bool> IncludeInBuildByGroupName = new Dictionary<string, bool>();
            public BuildTarget ActiveBuildTarget;
            public BuildTargetGroup ActiveBuildTargetGroup;
            public BuildTarget SelectedStandaloneTarget;
            public ScriptingImplementation StandaloneScriptingBackend;
        }

        public sealed class ExportRequiresRecompileException : InvalidOperationException
        {
            public ExportRequiresRecompileException(string message)
                : base(message)
            {
            }
        }

        private static bool EditorCompiledWithJsonCatalog
        {
            get
            {
#if ENABLE_JSON_CATALOG
                return true;
#else
                return false;
#endif
            }
        }

        private MTIONSDKAssetBase assetBase;
        private ExportLocationOptions exportLocationOptions;

        public VisualScriptingInspectionReport LastVisualScriptingInspectionReport { get; private set; }

        public AssetExporter(MTIONSDKAssetBase assetBase, ExportLocationOptions exportLocationOptions)
        {
            this.assetBase = assetBase;
            this.exportLocationOptions = exportLocationOptions;
        }

        public static void EnsureProjectReadyForJsonCatalogExport()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            EnsureJsonCatalogConfiguration(settings);
        }

        public void ExportSDKAsset()
        {
            if (assetBase == null)
            {
                throw new ArgumentNullException();
            }

            EnsureProjectReadyForJsonCatalogExport();

            SDKServerManager.VerifyAssetGuid(assetBase);




            NormalizeVisualScriptingPlacement();

            if (assetBase.ObjectType == MTIONObjectType.MTIONSDK_ENVIRONMENT ||
                assetBase.ObjectType == MTIONObjectType.MTIONSDK_ROOM)
            {
                CreateAddressableAssetScene();
            }
            else
            {
                CreateAddressableAssetPrefab();
            }

            EditorUtility.SetDirty(assetBase);
            EditorSceneManager.SaveOpenScenes();

            try
            {
                VisualScriptingProjectPreflight.EnsureGeneratedDataIsHealthy(true);
                LastVisualScriptingInspectionReport = InspectVisualScriptingContract();
                CreateAddressablesGroupForExport();
                DeleteLegacyGLTFArtifacts();
                if (assetBase.ObjectType == MTIONObjectType.MTIONSDK_ENVIRONMENT)
                {
                    ExportLightmapData(assetBase, exportLocationOptions, null);
                }

                ExportAsAddressableAssetBundle(null);
                MarkAssetDirty();
            }
            finally
            {
                try
                {
                    RemoveAddressableGroup();
                }
                finally
                {
                    CleanupGeneratedUnityAssets();
                }
            }
        }


        private void MarkAssetDirty()
        {
            string thumbnailMediaId = assetBase.ObjectType == MTIONObjectType.MTIONSDK_ASSET || assetBase.ObjectType == MTIONObjectType.MTIONSDK_AVATAR
                ? null
                : assetBase.GUID;
            SDKExportUtility.WriteAssetMetaCache(assetBase, exportLocationOptions, thumbnailMediaId);
        }


        public void CreateAddressableAssetPrefab()
        {
            var dir = SDKUtil.GetAssetPrefabDirectory();
            Directory.CreateDirectory(dir);

            var exportPrefabPath = Path.Combine(dir, $"{assetBase.InternalID}.prefab").Replace('\\', '/');
            assetBase.AddressableID = exportPrefabPath;

            AssetDatabase.DeleteAsset(exportPrefabPath);

            PrefabUtility.SaveAsPrefabAsset(assetBase.ObjectReferenceProp, exportPrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (string.IsNullOrWhiteSpace(AssetDatabase.AssetPathToGUID(exportPrefabPath)))
            {
                throw new InvalidOperationException($"Failed to import generated prefab for export: {exportPrefabPath}");
            }
        }

        public void CreateAddressableAssetScene()
        {
            var dir = SDKUtil.GetSceneDirectory();
            Directory.CreateDirectory(dir);

            var exportScenePath = Path.Combine(dir, $"{assetBase.InternalID}.unity").Replace('\\', '/');
            assetBase.AddressableID = exportScenePath;

            var scenePath = assetBase.ObjectReferenceProp.scene.path;
            AssetDatabase.DeleteAsset(exportScenePath);

            if (!AssetDatabase.CopyAsset(scenePath, exportScenePath))
                Debug.LogWarning($"Failed to copy {exportScenePath}");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (string.IsNullOrWhiteSpace(AssetDatabase.AssetPathToGUID(exportScenePath)))
            {
                throw new InvalidOperationException($"Failed to import generated scene for export: {exportScenePath}");
            }
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

            profile.SetValue(profileId, AddressableAssetSettings.kLocalBuildPath, $"{localBuildRoot}/[BuildTarget]");
            profile.SetValue(profileId, AddressableAssetSettings.kLocalLoadPath, loadRoot);

            var variantGroup = settings.FindGroup(profileName);
            if (variantGroup == null)
            {
                variantGroup = settings.CreateGroup(profileName, false, false, false, null);
                variantGroup.AddSchema<BundledAssetGroupSchema>();
                variantGroup.AddSchema<ContentUpdateGroupSchema>();
                ConfigureGroupSchema(settings, variantGroup);

                string sceneAAId = assetBase.AddressableID;
                string sceneEntryGUID = AssetDatabase.AssetPathToGUID(sceneAAId);
                if (string.IsNullOrWhiteSpace(sceneEntryGUID))
                {
                    throw new InvalidOperationException($"Generated addressable asset is missing a valid Unity GUID: {sceneAAId}");
                }
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
                ConfigureGroupSchema(settings, variantGroup);

                string sceneAAId = assetBase.AddressableID;
                string sceneEntryGUID = AssetDatabase.AssetPathToGUID(sceneAAId);
                if (string.IsNullOrWhiteSpace(sceneEntryGUID))
                {
                    throw new InvalidOperationException($"Generated addressable asset is missing a valid Unity GUID: {sceneAAId}");
                }
                var sceneAssetEntry = settings.CreateOrMoveEntry(sceneEntryGUID, variantGroup, false, false);
                sceneAssetEntry.address = sceneAAId;

                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, sceneAssetEntry, true);

                if (assetBase.ObjectType == MTIONObjectType.MTIONSDK_ENVIRONMENT)
                {
                    string lightAAId = SDKUtil.GetLighmapAddressableId(assetBase);
                    string lightGUID = AssetDatabase.AssetPathToGUID(lightAAId);
                    AddressableAssetEntry lightmapAssetEntry = settings.CreateOrMoveEntry(lightGUID, variantGroup, false, false);
                    lightmapAssetEntry.address = lightAAId;

                    settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, lightmapAssetEntry, true);
                }
            }

            settings.activeProfileId = defaultProfileID;

            AddVisualScriptingAddressableEntries(settings, variantGroup);
        }

        private VisualScriptingInspectionReport InspectVisualScriptingContract()
        {
            VisualScriptingInspectionReport report;
            switch (assetBase.ObjectType)
            {
                case MTIONObjectType.MTIONSDK_ROOM:
                    report = VisualScriptingSupportUtil.InspectSceneForExport(assetBase.gameObject.scene, VisualScriptingExportTarget.RoomScene);
                    break;
                case MTIONObjectType.MTIONSDK_ENVIRONMENT:
                    report = VisualScriptingSupportUtil.InspectSceneForExport(assetBase.gameObject.scene, VisualScriptingExportTarget.EnvironmentScene);
                    break;
                default:
                    report = VisualScriptingSupportUtil.InspectGameObjectForExport(assetBase.ObjectReferenceProp, VisualScriptingExportTarget.PortablePrefab);
                    break;
            }

            foreach (string warning in report.Warnings)
            {
                Debug.LogWarning($"[AssetExporter] {warning}", assetBase);
            }

            if (report.Errors.Count > 0)
            {
                throw new InvalidOperationException($"Visual scripting validation failed for {assetBase.InternalID}: {string.Join(" ", report.Errors)}");
            }

            return report;
        }

        private void NormalizeVisualScriptingPlacement()
        {
            GameObject sdkRoot = assetBase != null ? assetBase.ObjectReferenceProp : null;
            if (sdkRoot == null)
            {
                return;
            }

            if (!VisualScriptingHostUtility.NormalizePlacement(sdkRoot, false, out _, out List<string> migratedComponents, out string error))
            {
                throw new InvalidOperationException(error);
            }

            if (!VisualScriptingReflectionUtility.SyncEntryPointRegistryFromVisualScripting(sdkRoot, out _, out List<string> syncErrors))
            {
                throw new InvalidOperationException(string.Join(" ", syncErrors));
            }

            if (migratedComponents.Count > 0)
            {
                Debug.Log($"[AssetExporter] Moved root-level UVS components into {VisualScriptingHostUtility.HostObjectName}: {string.Join(", ", migratedComponents.Distinct())}", assetBase);
            }
        }

        private void AddVisualScriptingAddressableEntries(AddressableAssetSettings settings, AddressableAssetGroup variantGroup)
        {
            if (settings == null || variantGroup == null || LastVisualScriptingInspectionReport == null)
            {
                return;
            }

            foreach (string assetPath in LastVisualScriptingInspectionReport.ReferencedAssetPaths)
            {
                if (string.IsNullOrWhiteSpace(assetPath) ||
                    !assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(assetPath, assetBase.AddressableID, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string entryGuid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrWhiteSpace(entryGuid))
                {
                    continue;
                }

                var existingEntry = settings.FindAssetEntry(entryGuid);
                if (existingEntry != null && existingEntry.parentGroup != null && existingEntry.parentGroup != variantGroup)
                {
                    continue;
                }

                AddressableAssetEntry assetEntry = settings.CreateOrMoveEntry(entryGuid, variantGroup, false, false);
                assetEntry.address = assetPath;
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, assetEntry, true);
            }
        }

        private static void ConfigureGroupSchema(AddressableAssetSettings settings, AddressableAssetGroup variantGroup)
        {
            BundledAssetGroupSchema groupSchema = variantGroup.GetSchema<BundledAssetGroupSchema>();
            if (groupSchema == null)
            {
                groupSchema = variantGroup.AddSchema<BundledAssetGroupSchema>();
            }

            groupSchema.UseAssetBundleCache = true;
            groupSchema.UseAssetBundleCrc = false;
            groupSchema.IncludeInBuild = false;
            groupSchema.RetryCount = 3;
            groupSchema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.FileNameHash;
            groupSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            groupSchema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            groupSchema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            groupSchema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
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

            AssetDatabase.SaveAssets();
        }

        public void ExportAsAddressableAssetBundle(Action onExportComplete)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);

            var localUnityDirectory = SDKUtil.GetSDKLocalUnityBuildPath(assetBase, exportLocationOptions);
            if (Directory.Exists(localUnityDirectory))
            {
                Directory.Delete(localUnityDirectory, true);
            }
            Directory.CreateDirectory(localUnityDirectory);

            AddressablesBuildStateSnapshot buildState = CaptureAddressablesBuildState(settings);

            try
            {
                settings.BuildRemoteCatalog = true;
                settings.DisableCatalogUpdateOnStartup = true;
                settings.ContiguousBundles = true;
                settings.IgnoreUnsupportedFilesInBuild = true;
                TrySetBuiltInBundleNaming(settings, "Custom");
                TrySetAddressablesEnum(settings, "MonoScriptBundleNaming", "UnityEditor.AddressableAssets.Build.MonoScriptBundleNaming", "Custom");

                string groupProfileName = SDKUtil.GetAddressableGroupName(assetBase);

                BuildTarget mainTarget = BuildTarget.StandaloneWindows64;
                EditorUserBuildSettings.selectedStandaloneTarget = mainTarget;
                AssetDatabase.SaveAssets();

                List<Tuple<BuildTargetGroup, BuildTarget>> targets = new List<Tuple<BuildTargetGroup, BuildTarget>>
                {
                    new Tuple<BuildTargetGroup, BuildTarget>(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64)
                };

                foreach (Tuple<BuildTargetGroup, BuildTarget> target in targets)
                {
                    if (EditorUserBuildSettings.activeBuildTarget != target.Item2)
                    {
                        throw new InvalidOperationException(
                            $"The active build target must remain {target.Item2} during export. Wait for Unity to finish updating, then start the export again.");
                    }

                    AddressableAssetProfileSettings profile = settings.profileSettings;
                    string profileId = profile.GetProfileId(groupProfileName);
                    if (string.IsNullOrEmpty(profileId))
                    {
                        throw new InvalidOperationException("No addressables profile ID associated with the export group.");
                    }

                    settings.activeProfileId = profileId;

                    settings.groups.ForEach(group =>
                    {
                        if (group.ReadOnly)
                        {
                            return;
                        }

                        BundledAssetGroupSchema groupSchema = group.GetSchema<BundledAssetGroupSchema>();
                        if (groupSchema == null)
                        {
                            return;
                        }

                        groupSchema.IncludeInBuild = group.name == groupProfileName;

                        if (group.name == groupProfileName && group.CanBeSetAsDefault())
                        {
                            settings.DefaultGroup = group;
                        }
                    });

                    BundledAssetGroupSchema schema = settings.groups.First(group => @group.name == groupProfileName).GetSchema<BundledAssetGroupSchema>();
                    settings.RemoteCatalogBuildPath = schema.BuildPath;
                    settings.RemoteCatalogLoadPath = schema.LoadPath;
                    TrySetBuiltInBundleCustomNaming(settings, groupProfileName);
                    TrySetAddressablesString(settings, "MonoScriptBundleCustomNaming", groupProfileName);

                    AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
                    bool success = string.IsNullOrEmpty(result.Error);

                    if (!success)
                    {
                        VisualScriptingGeneratedDataAuditResult auditResult = VisualScriptingProjectPreflight.AuditGeneratedData();
                        string errorMessage = result.Error;
                        if (!auditResult.IsHealthy)
                        {
                            errorMessage = $"{errorMessage} {auditResult.GetSummary()}";
                        }

                        Debug.LogError("Addressables build error encountered: " + errorMessage);
                        throw new Exception(errorMessage);
                    }
                }

                AssetDatabase.SaveAssets();

                RenameCatalogs(localUnityDirectory, targets);
                EnsureCanonicalCatalogFiles(localUnityDirectory, targets);
                NormalizeCatalogLoadPaths(localUnityDirectory, targets);
                EnsureCanonicalCatalogFiles(localUnityDirectory, targets);
                EnsureJsonCatalogFiles(localUnityDirectory, targets);

                onExportComplete?.Invoke();
            }
            finally
            {
                RestoreAddressablesBuildState(settings, buildState);
                AssetDatabase.SaveAssets();
            }
        }

        private static void TrySetAddressablesString(AddressableAssetSettings settings, string propertyName, string value)
        {
            var prop = settings.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.PropertyType == typeof(string) && prop.CanWrite)
            {
                prop.SetValue(settings, value);
            }
        }

        private static void TrySetBuiltInBundleNaming(AddressableAssetSettings settings, string enumFieldName)
        {
            if (TrySetAddressablesEnum(settings, "BuiltInBundleNaming", "UnityEditor.AddressableAssets.Build.BuiltInBundleNaming", enumFieldName))
            {
                return;
            }

            TrySetAddressablesEnum(settings, "ShaderBundleNaming", "UnityEditor.AddressableAssets.Settings.ShaderBundleNaming", enumFieldName);
        }

        private static void TrySetBuiltInBundleCustomNaming(AddressableAssetSettings settings, string value)
        {
            var prop = settings.GetType().GetProperty("BuiltInBundleCustomNaming", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.PropertyType == typeof(string) && prop.CanWrite)
            {
                prop.SetValue(settings, value);
                return;
            }

            TrySetAddressablesString(settings, "ShaderBundleCustomNaming", value);
        }

        private static bool TrySetAddressablesEnum(AddressableAssetSettings settings, string propertyName, string enumTypeFullName, string enumFieldName)
        {
            var asm = typeof(AddressableAssetSettings).Assembly;
            var enumType = asm.GetType(enumTypeFullName);
            if (enumType == null || !enumType.IsEnum) return false;
            var prop = settings.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null || !prop.CanWrite) return false;
            try
            {
                var value = Enum.Parse(enumType, enumFieldName);
                prop.SetValue(settings, value);
                return true;
            }
            catch
            {
                return false;
            }
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
                        string[] files = Directory.GetFiles(targetPath, "catalog*." + extension, SearchOption.AllDirectories)
                            .Concat(Directory.GetFiles(targetPath, "*catalog*." + extension, SearchOption.AllDirectories))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();
                        foreach (string file in files)
                        {
                            string targetFile = Path.GetDirectoryName(file) + "/catalog." + extension;
                            if (string.Equals(file.Replace('\\', '/'), targetFile, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                            if (SafeFileIO.Exists(targetFile)) FileUtil.DeleteFileOrDirectory(targetFile);
                            FileUtil.MoveFileOrDirectory(file, targetFile);
                        }
                    }
                }
            }
        }

        private static void EnsureCanonicalCatalogFiles(string path, List<Tuple<BuildTargetGroup, BuildTarget>> targets)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            foreach (var target in targets)
            {
                string buildTarget = SDKUtil.GetBuildTargetDirectory(target.Item2 == BuildTarget.WebGL ? SDKBuildTarget.WebGL : SDKBuildTarget.StandaloneWindows);
                string targetPath = Path.Combine(path, buildTarget).Replace('\\', '/');
                if (!Directory.Exists(targetPath))
                {
                    continue;
                }

                EnsureCanonicalCatalogFile(targetPath, "json");
                EnsureCanonicalCatalogFile(targetPath, "hash");
            }
        }

        private static void EnsureJsonCatalogFiles(string path, List<Tuple<BuildTargetGroup, BuildTarget>> targets)
        {
            foreach (var target in targets)
            {
                string buildTarget = SDKUtil.GetBuildTargetDirectory(target.Item2 == BuildTarget.WebGL ? SDKBuildTarget.WebGL : SDKBuildTarget.StandaloneWindows);
                string targetPath = Path.Combine(path, buildTarget).Replace('\\', '/');
                if (!Directory.Exists(targetPath))
                {
                    continue;
                }

                string jsonCatalog = Path.Combine(targetPath, "catalog.json").Replace('\\', '/');
                if (File.Exists(jsonCatalog))
                {
                    continue;
                }

                string[] binaryCatalogs = Directory.GetFiles(targetPath, "catalog*.bin", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(targetPath, "*catalog*.bin", SearchOption.AllDirectories))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (binaryCatalogs.Length > 0)
                {
                    throw new InvalidOperationException(
                        $"Addressables built a binary catalog for {buildTarget}. This project requires JSON catalogs. Ensure Addressables 'Enable Json Catalog' is enabled, the Standalone scripting define '{EnableJsonCatalogDefine}' is present, Unity has recompiled, and rerun export.");
                }

                throw new InvalidOperationException($"Addressables build did not produce catalog.json for {buildTarget}.");
            }
        }

        private static void EnsureCanonicalCatalogFile(string targetPath, string extension)
        {
            string expectedPath = Path.Combine(targetPath, $"catalog.{extension}").Replace('\\', '/');
            if (File.Exists(expectedPath))
            {
                return;
            }

            for (int attempt = 0; attempt < 20; attempt++)
            {
                string candidatePath = FindBestCatalogCandidate(targetPath, extension, expectedPath);
                if (!string.IsNullOrWhiteSpace(candidatePath))
                {
                    if (!string.Equals(candidatePath, expectedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(expectedPath))
                        {
                            File.Delete(expectedPath);
                        }

                        File.Move(candidatePath, expectedPath);
                    }

                    if (File.Exists(expectedPath))
                    {
                        return;
                    }
                }

                Thread.Sleep(100);
            }
        }

        private static string FindBestCatalogCandidate(string targetPath, string extension, string expectedPath)
        {
            List<string> candidates = Directory.GetFiles(targetPath, $"catalog*.{extension}", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(targetPath, $"*catalog*.{extension}", SearchOption.AllDirectories))
                .Select(path => path.Replace('\\', '/'))
                .Where(path => !string.Equals(path, expectedPath, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (candidates.Count == 0)
            {
                List<string> fallbackCandidates = Directory.GetFiles(targetPath, $"*.{extension}", SearchOption.AllDirectories)
                    .Select(path => path.Replace('\\', '/'))
                    .Where(path => !string.Equals(path, expectedPath, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (fallbackCandidates.Count == 1)
                {
                    candidates.Add(fallbackCandidates[0]);
                }
            }

            return candidates
                .OrderBy(path => !string.Equals(Path.GetDirectoryName(path)?.Replace('\\', '/'), targetPath, StringComparison.OrdinalIgnoreCase))
                .ThenBy(path => !Path.GetFileName(path).StartsWith("catalog", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(path => new FileInfo(path).Length)
                .ThenByDescending(path => File.GetLastWriteTimeUtc(path))
                .FirstOrDefault();
        }


        private static void EnsureJsonCatalogConfiguration(AddressableAssetSettings settings)
        {
            if (settings == null)
            {
                throw new InvalidOperationException("Addressables settings are missing.");
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                throw new ExportRequiresRecompileException(
                    "Unity is still compiling or updating scripts. Wait for Unity to finish, then start the export again.");
            }

            List<string> changes = new List<string>();
            bool changedSettings = false;
            if (!settings.EnableJsonCatalog)
            {
                settings.EnableJsonCatalog = true;
                changedSettings = true;
                changes.Add("enabled Addressables 'Enable Json Catalog'");
            }

            bool? bundleLocalCatalog = TryGetAddressablesBool(settings, "BundleLocalCatalog");
            if (bundleLocalCatalog.HasValue && bundleLocalCatalog.Value)
            {
                if (TrySetAddressablesBool(settings, "BundleLocalCatalog", false))
                {
                    changedSettings = true;
                    changes.Add("disabled Addressables 'Bundle Local Catalog'");
                }
            }

            if (changedSettings)
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            string standaloneSymbols = GetStandaloneScriptingDefineSymbols() ?? string.Empty;
            if (!HasDefineSymbol(standaloneSymbols, EnableJsonCatalogDefine))
            {
                string updatedSymbols = string.IsNullOrWhiteSpace(standaloneSymbols)
                    ? EnableJsonCatalogDefine
                    : $"{standaloneSymbols};{EnableJsonCatalogDefine}";
                SetStandaloneScriptingDefineSymbols(updatedSymbols);
                changes.Add($"added Standalone scripting define '{EnableJsonCatalogDefine}'");
            }

            if (EnsureStandaloneWindows64Target())
            {
                changes.Add($"switched the active build target to {BuildTarget.StandaloneWindows64}");
            }

            if (changes.Count > 0)
            {
                RequestEditorScriptRecompile();
                throw new ExportRequiresRecompileException(BuildRecompileRequiredMessage(changes));
            }

            if (!EditorCompiledWithJsonCatalog)
            {
                RequestEditorScriptRecompile();
                throw new ExportRequiresRecompileException(
                    $"The SDK detected that Unity is still compiled without '{EnableJsonCatalogDefine}'. The SDK requested a script recompile. Wait for Unity to finish recompiling, then start the export again.");
            }
        }

        private static bool EnsureStandaloneWindows64Target()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64)
            {
                EditorUserBuildSettings.selectedStandaloneTarget = BuildTarget.StandaloneWindows64;
                return false;
            }

            EditorUserBuildSettings.selectedStandaloneTarget = BuildTarget.StandaloneWindows64;
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                throw new InvalidOperationException("The SDK could not switch the active build target to StandaloneWindows64.");
            }

            return true;
        }

        private static string BuildRecompileRequiredMessage(IEnumerable<string> changes)
        {
            string changeSummary = string.Join(", ", changes);
            return $"The SDK updated the project for JSON catalog exports ({changeSummary}) and requested a script recompile. Wait for Unity to finish recompiling, then start the export again.";
        }

        private static void RequestEditorScriptRecompile()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
        }

        private static string GetStandaloneScriptingDefineSymbols()
        {
#if UNITY_2021_2_OR_NEWER
            return PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Standalone);
#else
            return PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
#endif
        }

        private static void SetStandaloneScriptingDefineSymbols(string defineSymbols)
        {
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Standalone, defineSymbols);
#else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, defineSymbols);
#endif
        }

        private static bool HasDefineSymbol(string defineSymbols, string symbol)
        {
            return defineSymbols.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(entry => string.Equals(entry.Trim(), symbol, StringComparison.Ordinal));
        }

        private static bool? TryGetAddressablesBool(AddressableAssetSettings settings, string propertyName)
        {
            var prop = settings.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null || !prop.CanRead || prop.PropertyType != typeof(bool))
            {
                return null;
            }

            return (bool)prop.GetValue(settings);
        }

        private static bool TrySetAddressablesBool(AddressableAssetSettings settings, string propertyName, bool value)
        {
            var prop = settings.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null || !prop.CanWrite || prop.PropertyType != typeof(bool))
            {
                return false;
            }

            prop.SetValue(settings, value);
            return true;
        }

        private static void NormalizeCatalogLoadPaths(string path, List<Tuple<BuildTargetGroup, BuildTarget>> targets)
        {
            if (!Directory.Exists(path)) return;

            foreach (var target in targets)
            {
                string buildTarget = SDKUtil.GetBuildTargetDirectory(target.Item2 == BuildTarget.WebGL ? SDKBuildTarget.WebGL : SDKBuildTarget.StandaloneWindows);
                string targetPath = Path.Combine(path, buildTarget).Replace('\\', '/');

                if (!Directory.Exists(targetPath))
                {
                    continue;
                }

                string[] catalogFiles = Directory.GetFiles(targetPath, "catalog*.json", SearchOption.AllDirectories);
                foreach (string catalogFile in catalogFiles)
                {
                    string text = File.ReadAllText(catalogFile);
                    string normalizedText = text.Replace($"{SDKUtil.LOAD_URL}\\\\", $"{SDKUtil.LOAD_URL}/");
                    if (!string.Equals(text, normalizedText, StringComparison.Ordinal))
                    {
                        File.WriteAllText(catalogFile, normalizedText);
                    }
                }
            }
        }

        private AddressablesBuildStateSnapshot CaptureAddressablesBuildState(AddressableAssetSettings settings)
        {
            AddressablesBuildStateSnapshot snapshot = new AddressablesBuildStateSnapshot
            {
                ActiveProfileId = settings.activeProfileId,
                RemoteCatalogBuildPath = TryGetAddressablesProperty(settings, "RemoteCatalogBuildPath"),
                RemoteCatalogLoadPath = TryGetAddressablesProperty(settings, "RemoteCatalogLoadPath"),
                BuildRemoteCatalog = settings.BuildRemoteCatalog,
                DisableCatalogUpdateOnStartup = settings.DisableCatalogUpdateOnStartup,
                ContiguousBundles = settings.ContiguousBundles,
                IgnoreUnsupportedFilesInBuild = settings.IgnoreUnsupportedFilesInBuild,
                DefaultGroupName = settings.DefaultGroup != null ? settings.DefaultGroup.name : null,
                ActiveBuildTarget = EditorUserBuildSettings.activeBuildTarget,
                ActiveBuildTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                SelectedStandaloneTarget = EditorUserBuildSettings.selectedStandaloneTarget,
                StandaloneScriptingBackend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone),
                BuiltInBundleCustomNaming = TryGetAddressablesString(settings, "BuiltInBundleCustomNaming"),
                ShaderBundleCustomNaming = TryGetAddressablesString(settings, "ShaderBundleCustomNaming"),
                MonoScriptBundleCustomNaming = TryGetAddressablesString(settings, "MonoScriptBundleCustomNaming"),
                BuiltInBundleNaming = TryGetAddressablesProperty(settings, "BuiltInBundleNaming"),
                ShaderBundleNaming = TryGetAddressablesProperty(settings, "ShaderBundleNaming"),
                MonoScriptBundleNaming = TryGetAddressablesProperty(settings, "MonoScriptBundleNaming"),
            };

            settings.groups.ForEach(group =>
            {
                if (group.ReadOnly)
                {
                    return;
                }

                BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema != null)
                {
                    snapshot.IncludeInBuildByGroupName[group.name] = schema.IncludeInBuild;
                }
            });

            return snapshot;
        }

        private void RestoreAddressablesBuildState(AddressableAssetSettings settings, AddressablesBuildStateSnapshot snapshot)
        {
            if (settings == null || snapshot == null)
            {
                return;
            }

            settings.activeProfileId = snapshot.ActiveProfileId;
            settings.BuildRemoteCatalog = snapshot.BuildRemoteCatalog;
            settings.DisableCatalogUpdateOnStartup = snapshot.DisableCatalogUpdateOnStartup;
            settings.ContiguousBundles = snapshot.ContiguousBundles;
            settings.IgnoreUnsupportedFilesInBuild = snapshot.IgnoreUnsupportedFilesInBuild;

            TrySetAddressablesProperty(settings, "RemoteCatalogBuildPath", snapshot.RemoteCatalogBuildPath);
            TrySetAddressablesProperty(settings, "RemoteCatalogLoadPath", snapshot.RemoteCatalogLoadPath);

            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group.ReadOnly)
                {
                    continue;
                }

                BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null)
                {
                    continue;
                }

                if (snapshot.IncludeInBuildByGroupName.TryGetValue(group.name, out bool includeInBuild))
                {
                    schema.IncludeInBuild = includeInBuild;
                }

                if (!string.IsNullOrEmpty(snapshot.DefaultGroupName) && group.name == snapshot.DefaultGroupName && group.CanBeSetAsDefault())
                {
                    settings.DefaultGroup = group;
                }
            }

            if (snapshot.BuiltInBundleNaming != null)
            {
                TrySetAddressablesProperty(settings, "BuiltInBundleNaming", snapshot.BuiltInBundleNaming);
            }

            if (snapshot.ShaderBundleNaming != null)
            {
                TrySetAddressablesProperty(settings, "ShaderBundleNaming", snapshot.ShaderBundleNaming);
            }

            if (snapshot.MonoScriptBundleNaming != null)
            {
                TrySetAddressablesProperty(settings, "MonoScriptBundleNaming", snapshot.MonoScriptBundleNaming);
            }

            TrySetAddressablesString(settings, "BuiltInBundleCustomNaming", snapshot.BuiltInBundleCustomNaming ?? string.Empty);
            TrySetAddressablesString(settings, "ShaderBundleCustomNaming", snapshot.ShaderBundleCustomNaming ?? string.Empty);
            TrySetAddressablesString(settings, "MonoScriptBundleCustomNaming", snapshot.MonoScriptBundleCustomNaming ?? string.Empty);

            if (EditorUserBuildSettings.activeBuildTarget != snapshot.ActiveBuildTarget)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(snapshot.ActiveBuildTargetGroup, snapshot.ActiveBuildTarget);
            }

            EditorUserBuildSettings.selectedStandaloneTarget = snapshot.SelectedStandaloneTarget;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, snapshot.StandaloneScriptingBackend);
        }

        private static string TryGetAddressablesString(AddressableAssetSettings settings, string propertyName)
        {
            object value = TryGetAddressablesProperty(settings, propertyName);
            return value as string;
        }

        private static object TryGetAddressablesProperty(AddressableAssetSettings settings, string propertyName)
        {
            var prop = settings.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return prop != null && prop.CanRead ? prop.GetValue(settings) : null;
        }

        private static bool TrySetAddressablesProperty(AddressableAssetSettings settings, string propertyName, object value)
        {
            var prop = settings.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null || !prop.CanWrite || value == null || !prop.PropertyType.IsInstanceOfType(value))
            {
                return false;
            }

            prop.SetValue(settings, value);
            return true;
        }

        private void CleanupGeneratedUnityAssets()
        {
            if (!string.IsNullOrWhiteSpace(assetBase.AddressableID))
            {
                AssetDatabase.DeleteAsset(assetBase.AddressableID);
            }

            if (assetBase.ObjectType == MTIONObjectType.MTIONSDK_ENVIRONMENT)
            {
                string lightmapDirectory = SDKUtil.GetAssetLightmapDirectory(assetBase);
                if (AssetDatabase.IsValidFolder(lightmapDirectory))
                {
                    AssetDatabase.DeleteAsset(lightmapDirectory);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void DeleteLegacyGLTFArtifacts()
        {
            string webglDirectory = Path.Combine(SDKUtil.GetSDKItemDirectory(assetBase, exportLocationOptions), "webgl").Replace('\\', '/');
            if (Directory.Exists(webglDirectory))
            {
                Directory.Delete(webglDirectory, true);
            }
        }
    }
}
