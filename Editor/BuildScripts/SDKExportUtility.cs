using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using mtion.room.sdk.compiled;
using mtion.service.api;
using mtion.utility;
using Newtonsoft.Json;

namespace mtion.room.sdk
{
    public static class SDKExportUtility
    {
        public const string ExportManifestFileName = "export_manifest.json";
        public const string ExportReportFileName = "export_report.json";


        public static ExportManifestFile CreateManifest(MTIONSDKAssetBase assetBase, string formatVersion)
        {
            return new ExportManifestFile
            {
                ExportedAtMS = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                SDKVersion = ConfigurationGenerator.GetSDKPackageVersion(),
                FormatVersion = formatVersion,
                ObjectType = assetBase.ObjectType,
                GUID = assetBase.GUID,
                InternalID = assetBase.InternalID,
                Name = assetBase.Name,
                LocationOption = assetBase is MTIONSDKDescriptorSceneBase sceneBase
                    ? sceneBase.LocationOption
                    : ExportLocationOptions.PersistentStorage,
            };
        }

        public static ExportManifestArtifact CreateFileArtifact(string baseDirectory, string label, string absolutePath, bool required)
        {
            return new ExportManifestArtifact
            {
                Label = label,
                RelativePath = GetRelativePath(baseDirectory, absolutePath),
                Required = required,
            };
        }

        public static ExportManifestArtifact CreateDirectoryArtifact(string baseDirectory, string label, string absolutePath, bool required, int minimumEntryCount)
        {
            return new ExportManifestArtifact
            {
                Label = label,
                RelativePath = GetRelativePath(baseDirectory, absolutePath),
                Required = required,
                IsDirectory = true,
                MinimumEntryCount = Math.Max(0, minimumEntryCount),
            };
        }

        public static void AddRequirement(ExportManifestFile manifest, string type, string value, bool required = true)
        {
            if (manifest.Requirements.Any(req =>
                    string.Equals(req.Type, type, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(req.Value, value, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            manifest.Requirements.Add(new ExportManifestRequirement
            {
                Type = type,
                Value = value,
                Required = required,
            });
        }

        public static void AddWarning(ExportManifestFile manifest, string warning)
        {
            if (!manifest.Warnings.Contains(warning))
            {
                manifest.Warnings.Add(warning);
            }
        }


        public static BlueprintMetaLocalCache WriteBlueprintMetaCache(MTIONSDKBlueprint blueprintDescriptor, string thumbnailMediaId)
        {
            string blueprintDirectory = Path.Combine(
                SDKUtil.GetSDKBlueprintDirectory(blueprintDescriptor.LocationOption),
                blueprintDescriptor.GUID).Replace('\\', '/');
            string metaFilePath = Path.Combine(blueprintDirectory, SDKUtil.CLUBHOUSE_META).Replace('\\', '/');

            BlueprintMetaLocalCache cache = ReadBlueprintMetaCache(metaFilePath) ?? new BlueprintMetaLocalCache();
            cache.Id = blueprintDescriptor.GUID;
            cache.BlueprintId = string.IsNullOrWhiteSpace(cache.BlueprintId) ? blueprintDescriptor.GUID : cache.BlueprintId;
            cache.ThumbnailMediaId = !string.IsNullOrWhiteSpace(thumbnailMediaId)
                ? thumbnailMediaId
                : (!string.IsNullOrWhiteSpace(cache.ThumbnailMediaId) ? cache.ThumbnailMediaId : blueprintDescriptor.GUID);
            cache.Name = !string.IsNullOrWhiteSpace(blueprintDescriptor.Name) ? blueprintDescriptor.Name : cache.Name;
            cache.Description = blueprintDescriptor.Description ?? cache.Description;
            cache.Version = NormalizeVersion(blueprintDescriptor.Version);
            cache.FormatVersion = MTIONSDKBlueprint.CurrentFormatVersion;
            cache.RequiresDownload = false;

            Directory.CreateDirectory(blueprintDirectory);
            SafeFileIO.WriteAllText(metaFilePath, JsonConvert.SerializeObject(cache, Formatting.Indented));
            return cache;
        }

        public static AssetMetaLocalCache WriteAssetMetaCache(MTIONSDKAssetBase assetBase, ExportLocationOptions locationOption, string thumbnailMediaId = null)
        {
            string assetDirectory = SDKUtil.GetSDKItemDirectory(assetBase, locationOption);
            string metaFilePath = Path.Combine(assetDirectory, SDKUtil.CLUBHOUSE_META).Replace('\\', '/');

            AssetMetaLocalCache cache = ReadAssetMetaCache(metaFilePath) ?? new AssetMetaLocalCache();
            bool isNewCache = string.IsNullOrWhiteSpace(cache.Id);

            cache.Id = assetBase.GUID;
            cache.AssetType = ConvertSDKObjectTypeToAssetType(assetBase.ObjectType);
            cache.LocalVersion = NormalizeVersion(assetBase.Version);
            cache.Version = NormalizeVersion(assetBase.Version);
            cache.ThumbnailMediaId = !string.IsNullOrWhiteSpace(thumbnailMediaId)
                ? thumbnailMediaId
                : (!string.IsNullOrWhiteSpace(cache.ThumbnailMediaId) ? cache.ThumbnailMediaId : assetBase.GUID);
            cache.Name = !string.IsNullOrWhiteSpace(assetBase.Name) ? assetBase.Name : cache.Name;
            cache.Description = assetBase.Description ?? cache.Description;
            cache.IsDirty = true;
            cache.RequiresDownload = false;

            if (isNewCache)
            {
                cache.Visibility = Visibility.PRIVATE;
                cache.PublishedStatus = PublishedStatus.NONE;
                cache.IsSynced = false;
            }

            ApplyTimestamps(cache, assetBase.CreateTimeMS, assetBase.UpdateTimeMS);

            Directory.CreateDirectory(assetDirectory);
            SafeFileIO.WriteAllText(metaFilePath, JsonConvert.SerializeObject(cache, Formatting.Indented));
            return cache;
        }

        public static BlueprintMetaLocalCache ReadBlueprintMetaCache(string metaFilePath)
        {
            return TryReadMetaCache<BlueprintMetaLocalCache>(metaFilePath);
        }

        public static AssetMetaLocalCache ReadAssetMetaCache(string metaFilePath)
        {
            return TryReadMetaCache<AssetMetaLocalCache>(metaFilePath);
        }

        public static void PopulateHostRequirements(ExportManifestFile manifest, IEnumerable<DisplayComponentType> displayTypes)
        {
            List<DisplayComponentType> displayTypeList = displayTypes.Distinct().ToList();

            AddRequirement(manifest, "platform", SDKUtil.GetBuildTargetDirectory(SDKBuildTarget.StandaloneWindows));
            AddRequirement(manifest, "runtime", "IL2CPP");
            if (displayTypeList.Count > 0)
            {
                AddRequirement(manifest, "layer", "VirtualDisplayLayer");
            }

            foreach (DisplayComponentType displayType in displayTypeList)
            {
                switch (displayType)
                {
                    case DisplayComponentType.VIDEO_PLAYER:
                    case DisplayComponentType.VIDEO_LIST_PLAYER:
                        AddRequirement(manifest, "plugin", "AVProVideo");
                        break;
                    case DisplayComponentType.DIRECT_STREAM_VIEW:
                        AddRequirement(manifest, "plugin", "VuplexWebView");
                        break;
                    case DisplayComponentType.DESKTOP_CAPTURE:
                    case DisplayComponentType.EXTERNAL_WEBCAM:
                    case DisplayComponentType.VTUBER_EXTERNAL_CAMERA_FEED:
                        AddRequirement(manifest, "plugin", "CaptureAPI");
                        break;
                    case DisplayComponentType.WINDOW_CAPTURE:
                        AddRequirement(manifest, "plugin", "WindowCapture");
                        break;
                    case DisplayComponentType.NDI_SOURCE:
                        AddRequirement(manifest, "plugin", "NDI");
                        break;
                    case DisplayComponentType.SPOUT_SOURCE:
                        AddRequirement(manifest, "plugin", "Spout");
                        break;
                    case DisplayComponentType.WEBRTC_SOURCE:
                        AddRequirement(manifest, "plugin", "WebRTC");
                        break;
                }
            }

            if (displayTypeList.Contains(DisplayComponentType.VTUBER_EXTERNAL_CAMERA_FEED))
            {
                AddRequirement(manifest, "layer", "MainVirtualCameraIgnore");
            }
        }


        public static bool VerifyArtifacts(ExportManifestFile manifest, string baseDirectory, out List<string> missingArtifacts)
        {
            missingArtifacts = new List<string>();

            foreach (ExportManifestArtifact artifact in manifest.Artifacts)
            {
                string absolutePath = GetAbsolutePath(baseDirectory, artifact.RelativePath);
                if (!artifact.IsDirectory && !File.Exists(absolutePath))
                {
                    TryResolveCatalogArtifact(baseDirectory, artifact, absolutePath);
                    absolutePath = GetAbsolutePath(baseDirectory, artifact.RelativePath);
                }

                if (artifact.IsDirectory)
                {
                    if (Directory.Exists(absolutePath))
                    {
                        artifact.ResolvedEntryCount = Directory.GetFiles(absolutePath, "*", SearchOption.AllDirectories).Length;
                        artifact.Exists = artifact.ResolvedEntryCount >= artifact.MinimumEntryCount;
                    }
                    else
                    {
                        artifact.ResolvedEntryCount = 0;
                        artifact.Exists = false;
                    }
                }
                else if (File.Exists(absolutePath))
                {
                    FileInfo fileInfo = new FileInfo(absolutePath);
                    artifact.Exists = true;
                    artifact.ResolvedEntryCount = 1;
                    artifact.SizeBytes = fileInfo.Length;
                    artifact.Sha256 = ComputeSha256(absolutePath);
                }
                else
                {
                    artifact.Exists = false;
                    artifact.ResolvedEntryCount = 0;
                    artifact.SizeBytes = 0;
                    artifact.Sha256 = null;
                }

                if (artifact.Required && !artifact.Exists)
                {
                    missingArtifacts.Add(artifact.Label);
                }
            }

            return missingArtifacts.Count == 0;
        }

        public static string DescribeDirectory(string directoryPath, int maxEntries = 40)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return "<no directory>";
            }

            if (!Directory.Exists(directoryPath))
            {
                return $"missing: {directoryPath}";
            }

            try
            {
                string normalizedRoot = NormalizePath(directoryPath);
                List<string> entries = Directory
                    .GetFileSystemEntries(normalizedRoot, "*", SearchOption.AllDirectories)
                    .Select(path => GetRelativePath(normalizedRoot, path))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .Take(Math.Max(1, maxEntries))
                    .ToList();

                return entries.Count == 0
                    ? $"empty: {normalizedRoot}"
                    : string.Join(", ", entries);
            }
            catch (Exception ex)
            {
                return $"error reading {directoryPath}: {ex.Message}";
            }
        }

        public static string WriteManifest(string baseDirectory, ExportManifestFile manifest)
        {
            Directory.CreateDirectory(baseDirectory);

            string path = Path.Combine(baseDirectory, ExportManifestFileName).Replace('\\', '/');
            string json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
            File.WriteAllText(path, json);
            return path;
        }

        public static string WriteReport(string baseDirectory, SDKExportReport report)
        {
            Directory.CreateDirectory(baseDirectory);

            string path = Path.Combine(baseDirectory, ExportReportFileName).Replace('\\', '/');
            string json = JsonConvert.SerializeObject(report, Formatting.Indented);
            File.WriteAllText(path, json);
            return path;
        }

        public static string GetRelativePath(string baseDirectory, string absolutePath)
        {
            string normalizedBase = NormalizePath(baseDirectory);
            string normalizedAbsolute = NormalizePath(absolutePath);

            Uri baseUri = new Uri(AppendDirectorySeparator(normalizedBase));
            Uri targetUri = new Uri(normalizedAbsolute);
            return Uri.UnescapeDataString(baseUri.MakeRelativeUri(targetUri).ToString()).Replace('\\', '/');
        }

        public static string GetAbsolutePath(string baseDirectory, string relativePath)
        {
            return NormalizePath(Path.Combine(baseDirectory, relativePath));
        }

        private static string ComputeSha256(string filePath)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(filePath))
            {
                byte[] hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).Replace('\\', '/');
        }

        private static T TryReadMetaCache<T>(string metaFilePath) where T : class
        {
            if (string.IsNullOrWhiteSpace(metaFilePath) || !SafeFileIO.Exists(metaFilePath))
            {
                return null;
            }

            try
            {
                string json = SafeFileIO.ReadAllText(metaFilePath);
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                return null;
            }
        }

        private static int NormalizeVersion(float version)
        {
            return Math.Max(1, (int)Math.Ceiling(version <= 0f ? 1f : version));
        }

        private static void ApplyTimestamps(AssetMetaLocalCache cache, long createTimeMS, long updateTimeMS)
        {
            if (createTimeMS > 0)
            {
                cache.CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(createTimeMS).UtcDateTime;
            }

            if (updateTimeMS > 0)
            {
                cache.UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(updateTimeMS).UtcDateTime;
            }
        }

        private static AssetType ConvertSDKObjectTypeToAssetType(MTIONObjectType objectType)
        {
            switch (objectType)
            {
                case MTIONObjectType.MTIONSDK_ROOM:
                    return AssetType.ROOM;
                case MTIONObjectType.MTIONSDK_ENVIRONMENT:
                    return AssetType.ENVIRONMENT;
                case MTIONObjectType.MTIONSDK_AVATAR:
                    return AssetType.AVATAR;
                case MTIONObjectType.MTIONSDK_ASSET:
                default:
                    return AssetType.OBJECT;
            }
        }

        private static void TryResolveCatalogArtifact(string baseDirectory, ExportManifestArtifact artifact, string expectedAbsolutePath)
        {
            string fileName = Path.GetFileName(expectedAbsolutePath);
            bool isCatalogDataFile = string.Equals(fileName, "catalog.json", StringComparison.OrdinalIgnoreCase);
            bool isCatalogHashFile = string.Equals(fileName, "catalog.hash", StringComparison.OrdinalIgnoreCase);
            if (!isCatalogDataFile && !isCatalogHashFile)
            {
                return;
            }

            string targetDirectory = Path.GetDirectoryName(expectedAbsolutePath);
            string[] candidateExtensions = isCatalogDataFile ? new[] { "json" } : new[] { "hash" };
            for (int attempt = 0; attempt < 20; attempt++)
            {
                if (File.Exists(expectedAbsolutePath))
                {
                    return;
                }

                string candidatePath = FindCatalogCandidate(targetDirectory, candidateExtensions, expectedAbsolutePath);
                if (!string.IsNullOrWhiteSpace(candidatePath))
                {
                    artifact.RelativePath = GetRelativePath(baseDirectory, candidatePath);
                    return;
                }

                System.Threading.Thread.Sleep(100);
            }
        }

        private static string FindCatalogCandidate(string targetDirectory, IEnumerable<string> extensions, string expectedAbsolutePath)
        {
            if (string.IsNullOrWhiteSpace(targetDirectory) || !Directory.Exists(targetDirectory))
            {
                return null;
            }

            string normalizedExpectedPath = NormalizePath(expectedAbsolutePath);
            List<string> candidates = new List<string>();
            foreach (string extension in extensions)
            {
                candidates.AddRange(Directory.GetFiles(targetDirectory, $"catalog*.{extension}", SearchOption.AllDirectories));
                candidates.AddRange(Directory.GetFiles(targetDirectory, $"*catalog*.{extension}", SearchOption.AllDirectories));
            }

            candidates = candidates
                .Select(NormalizePath)
                .Where(path => !string.Equals(path, normalizedExpectedPath, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (candidates.Count == 0)
            {
                foreach (string extension in extensions)
                {
                    List<string> fallbackCandidates = Directory.GetFiles(targetDirectory, $"*.{extension}", SearchOption.AllDirectories)
                        .Select(NormalizePath)
                        .Where(path => !string.Equals(path, normalizedExpectedPath, StringComparison.OrdinalIgnoreCase))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (fallbackCandidates.Count == 1)
                    {
                        candidates.Add(fallbackCandidates[0]);
                    }
                }
            }

            string normalizedTargetDirectory = NormalizePath(targetDirectory);
            return candidates
                .OrderBy(path => !string.Equals(Path.GetDirectoryName(path), normalizedTargetDirectory, StringComparison.OrdinalIgnoreCase))
                .ThenBy(path => !Path.GetFileName(path).StartsWith("catalog", StringComparison.OrdinalIgnoreCase))
                .ThenBy(path => string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenByDescending(path => new FileInfo(path).Length)
                .ThenByDescending(path => File.GetLastWriteTimeUtc(path))
                .FirstOrDefault();
        }

        private static string AppendDirectorySeparator(string path)
        {
            if (path.EndsWith("/", StringComparison.Ordinal))
            {
                return path;
            }

            return path + "/";
        }
    }
}
