using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using mtion.room.sdk.visualscripting;
using UnityEditor;
using UnityEngine;

namespace mtion.room.sdk
{
    public sealed class VisualScriptingGeneratedDataAuditResult
    {
        public bool IsHealthy => MissingTypeNames.Count == 0;
        public bool WasRepaired { get; set; }
        public List<string> MissingTypeNames { get; } = new List<string>();
        public string AssetPath { get; set; }

        public string GetSummary()
        {
            if (IsHealthy)
            {
                return "Unity Visual Scripting generated data is healthy.";
            }

            return $"Unity Visual Scripting generated data references missing types: {string.Join(", ", MissingTypeNames)}";
        }
    }

    public static class VisualScriptingProjectPreflight
    {
        private const string GeneratedUnitOptionsAssetPath = "Assets/Unity.VisualScripting.Generated/VisualScripting.Flow/UnitOptions.db";
        private static readonly Regex GeneratedTypePattern = new Regex(@"(?m)^([A-Za-z_][A-Za-z0-9_\.]+)@(literal|expose)$", RegexOptions.Compiled);
        private static long _cachedGeneratedUnitOptionsTicks = long.MinValue;
        private static bool _cachedGeneratedUnitOptionsExists;
        private static VisualScriptingGeneratedDataAuditResult _cachedAuditResult;


        public static VisualScriptingGeneratedDataAuditResult AuditGeneratedData()
        {
            string absolutePath = GetAbsolutePath(GeneratedUnitOptionsAssetPath);
            bool fileExists = File.Exists(absolutePath);
            long fileTicks = fileExists ? File.GetLastWriteTimeUtc(absolutePath).Ticks : long.MinValue;
            if (_cachedAuditResult != null &&
                _cachedGeneratedUnitOptionsExists == fileExists &&
                _cachedGeneratedUnitOptionsTicks == fileTicks)
            {
                return CloneAuditResult(_cachedAuditResult);
            }

            VisualScriptingGeneratedDataAuditResult result = new VisualScriptingGeneratedDataAuditResult
            {
                AssetPath = GeneratedUnitOptionsAssetPath,
            };

            if (!fileExists)
            {
                CacheAuditResult(result, fileExists, fileTicks);
                return CloneAuditResult(result);
            }

            byte[] bytes = File.ReadAllBytes(absolutePath);
            string text = Encoding.UTF8.GetString(bytes);
            HashSet<string> missingTypeNames = new HashSet<string>(StringComparer.Ordinal);

            MatchCollection matches = GeneratedTypePattern.Matches(text);
            for (int i = 0; i < matches.Count; i++)
            {
                string candidateTypeName = matches[i].Groups[1].Value;
                if (string.IsNullOrWhiteSpace(candidateTypeName))
                {
                    continue;
                }

                if (!VisualScriptingSupportUtil.TryResolveTypeCandidate(candidateTypeName, out _))
                {
                    missingTypeNames.Add(candidateTypeName);
                }
            }

            result.MissingTypeNames.AddRange(missingTypeNames.OrderBy(typeName => typeName, StringComparer.Ordinal));
            CacheAuditResult(result, fileExists, fileTicks);
            return CloneAuditResult(result);
        }

        public static void EnsureGeneratedDataIsHealthy(bool attemptRepair)
        {
            VisualScriptingGeneratedDataAuditResult auditResult = AuditGeneratedData();
            if (auditResult.IsHealthy)
            {
                return;
            }

            if (!attemptRepair)
            {
                throw new InvalidOperationException(auditResult.GetSummary());
            }

            if (!AssetDatabase.DeleteAsset(GeneratedUnitOptionsAssetPath))
            {
                throw new InvalidOperationException($"{auditResult.GetSummary()} Failed to delete stale generated asset '{GeneratedUnitOptionsAssetPath}'.");
            }

            InvalidateCache();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            auditResult.WasRepaired = true;
            Debug.LogWarning($"[VisualScriptingProjectPreflight] Removed stale Unity Visual Scripting generated data at '{GeneratedUnitOptionsAssetPath}' because it referenced missing types: {string.Join(", ", auditResult.MissingTypeNames)}");

            VisualScriptingGeneratedDataAuditResult refreshedResult = AuditGeneratedData();
            if (!refreshedResult.IsHealthy)
            {
                throw new InvalidOperationException($"{refreshedResult.GetSummary()} Please regenerate Unity Visual Scripting project data before exporting.");
            }
        }

        public static bool EnsureSdkEntryPointNodeLibraryIsReady(bool attemptRepair)
        {
            if (!NeedsSdkEntryPointNodeLibraryRebuild(out string reason))
            {
                return false;
            }

            if (!attemptRepair)
            {
                throw new InvalidOperationException(reason);
            }

            RebuildNodeLibrary();
            InvalidateCache();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            if (NeedsSdkEntryPointNodeLibraryRebuild(out string refreshedReason))
            {
                throw new InvalidOperationException($"{refreshedReason} Make sure '{UVSSDKEntryPointConstants.EntryPointUnitCategory}' is available in the Visual Scripting node library and that assembly '{nameof(UVSSDKEntryPointUnit)}' is included in Visual Scripting assembly options.");
            }

            Debug.Log($"[VisualScriptingProjectPreflight] Rebuilt Unity Visual Scripting node library because {reason}");
            return true;
        }

        public static void InvalidateCache()
        {
            _cachedAuditResult = null;
            _cachedGeneratedUnitOptionsExists = false;
            _cachedGeneratedUnitOptionsTicks = long.MinValue;
        }

        private static bool NeedsSdkEntryPointNodeLibraryRebuild(out string reason)
        {
            reason = null;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                throw new InvalidOperationException("Unity is still compiling/updating scripts. Wait for the editor to finish, then run Configure UVS again.");
            }

            if (FindType($"{UVSSDKEntryPointConstants.EntryPointUnitTypeName}, MTIONStudioSDK_Public_VisualScripting") == null)
            {
                reason = $"The SDK Entry Point unit type '{UVSSDKEntryPointConstants.EntryPointUnitTypeName}' is unavailable.";
                return true;
            }

            string absolutePath = GetAbsolutePath(GeneratedUnitOptionsAssetPath);
            if (!File.Exists(absolutePath))
            {
                reason = "the Unity Visual Scripting node library has not been generated yet.";
                return true;
            }

            string unitOptionsText = Encoding.UTF8.GetString(File.ReadAllBytes(absolutePath));
            if (!unitOptionsText.Contains(UVSSDKEntryPointConstants.EntryPointUnitTypeName, StringComparison.Ordinal))
            {
                reason = "the SDK Entry Point unit is missing from the Unity Visual Scripting node library.";
                return true;
            }

            if (!unitOptionsText.Contains(UVSSDKEntryPointConstants.EntryPointUnitCategory, StringComparison.Ordinal))
            {
                reason = $"the SDK Entry Point unit category is stale and does not include '{UVSSDKEntryPointConstants.EntryPointUnitCategory}'.";
                return true;
            }

            return false;
        }

        private static void RebuildNodeLibrary()
        {
            Type unitBaseType = FindType("Unity.VisualScripting.UnitBase, Unity.VisualScripting.Flow.Editor");
            MethodInfo rebuildMethod = unitBaseType?.GetMethod("Rebuild", BindingFlags.Public | BindingFlags.Static);
            if (rebuildMethod == null)
            {
                throw new InvalidOperationException("Unity Visual Scripting node rebuild API is unavailable. Use 'Edit > Project Settings > Visual Scripting > Node Library > Regenerate Nodes'.");
            }

            try
            {
                rebuildMethod.Invoke(null, Array.Empty<object>());
            }
            catch (TargetInvocationException ex)
            {
                throw new InvalidOperationException(ex.InnerException?.Message ?? ex.Message, ex.InnerException ?? ex);
            }
        }

        private static void CacheAuditResult(VisualScriptingGeneratedDataAuditResult result, bool fileExists, long fileTicks)
        {
            _cachedAuditResult = CloneAuditResult(result);
            _cachedGeneratedUnitOptionsExists = fileExists;
            _cachedGeneratedUnitOptionsTicks = fileTicks;
        }

        private static VisualScriptingGeneratedDataAuditResult CloneAuditResult(VisualScriptingGeneratedDataAuditResult source)
        {
            VisualScriptingGeneratedDataAuditResult clone = new VisualScriptingGeneratedDataAuditResult
            {
                AssetPath = source.AssetPath,
                WasRepaired = source.WasRepaired,
            };
            clone.MissingTypeNames.AddRange(source.MissingTypeNames);
            return clone;
        }

        private static string GetAbsolutePath(string assetPath)
        {
            string relativePath = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }

        private static Type FindType(string assemblyQualifiedOrAssemblyScopedName)
        {
            if (string.IsNullOrWhiteSpace(assemblyQualifiedOrAssemblyScopedName))
            {
                return null;
            }

            Type type = Type.GetType(assemblyQualifiedOrAssemblyScopedName, false);
            if (type != null)
            {
                return type;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(assemblyQualifiedOrAssemblyScopedName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
