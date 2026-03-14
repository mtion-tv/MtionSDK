using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace mtion.room.sdk.visualscripting
{
    [Serializable]
    public sealed class UVSSDKEntryPointDefinition
    {
        [SerializeField]
        private string _entryPointId = UVSSDKEntryPointConstants.GenerateEntryPointId();
        [SerializeField]
        private string _displayName = UVSSDKEntryPointConstants.DefaultDisplayName;
        [SerializeField]
        private string _targetRelativePathFromRoot = string.Empty;
        [SerializeField]
        private string _targetLookupName = string.Empty;

        public string EntryPointId
        {
            get => _entryPointId;
            set => _entryPointId = string.IsNullOrWhiteSpace(value) ? UVSSDKEntryPointConstants.GenerateEntryPointId() : value.Trim();
        }

        public string DisplayName
        {
            get => _displayName;
            set => _displayName = string.IsNullOrWhiteSpace(value) ? UVSSDKEntryPointConstants.DefaultDisplayName : value.Trim();
        }

        public string TargetRelativePathFromRoot
        {
            get => _targetRelativePathFromRoot;
            set => _targetRelativePathFromRoot = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public string TargetLookupName
        {
            get => _targetLookupName;
            set => _targetLookupName = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class UVSSDKStableLookupId : MonoBehaviour, ISerializationCallbackReceiver
    {
        [SerializeField]
        private string _stableLookupId = UVSSDKEntryPointConstants.GenerateStableLookupId();

        public string StableLookupId
        {
            get => _stableLookupId;
            set => _stableLookupId = string.IsNullOrWhiteSpace(value) ? UVSSDKEntryPointConstants.GenerateStableLookupId() : value.Trim();
        }

        public string StableLookupObjectName => UVSSDKEntryPointConstants.GetStableLookupObjectName(_stableLookupId);

        public void OnBeforeSerialize()
        {
            EnsureStableLookupId();
        }

        public void OnAfterDeserialize()
        {
            EnsureStableLookupId();
        }

        public void EnsureStableLookupId()
        {
            if (string.IsNullOrWhiteSpace(_stableLookupId))
            {
                _stableLookupId = UVSSDKEntryPointConstants.GenerateStableLookupId();
            }
        }
    }

    public sealed class UVSSDKEntryPointRegistry : MonoBehaviour, ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<UVSSDKEntryPointDefinition> _entryPoints = new List<UVSSDKEntryPointDefinition>();

        public IReadOnlyList<UVSSDKEntryPointDefinition> EntryPoints => _entryPoints;

        public List<string> GetDisplayNameDropdownOptions()
        {
            List<string> options = new List<string>(_entryPoints.Count);
            for (int i = 0; i < _entryPoints.Count; i++)
            {
                UVSSDKEntryPointDefinition entryPoint = _entryPoints[i];
                if (entryPoint == null || string.IsNullOrWhiteSpace(entryPoint.EntryPointId))
                {
                    continue;
                }

                options.Add(UVSSDKEntryPointConstants.FormatDropdownOption(entryPoint.DisplayName, entryPoint.EntryPointId));
            }

            return options;
        }

        public UVSSDKEntryPointDefinition FindByEntryPointId(string entryPointId)
        {
            if (string.IsNullOrWhiteSpace(entryPointId))
            {
                return null;
            }

            for (int i = 0; i < _entryPoints.Count; i++)
            {
                UVSSDKEntryPointDefinition entryPoint = _entryPoints[i];
                if (entryPoint != null && string.Equals(entryPoint.EntryPointId, entryPointId, StringComparison.Ordinal))
                {
                    return entryPoint;
                }
            }

            return null;
        }

        public void EnsureEntryPoint(string entryPointId, string displayName, string targetLookupName, string targetRelativePathFromRoot)
        {
            if (string.IsNullOrWhiteSpace(entryPointId))
            {
                return;
            }

            UVSSDKEntryPointDefinition existingEntryPoint = FindByEntryPointId(entryPointId);
            if (existingEntryPoint != null)
            {
                existingEntryPoint.DisplayName = displayName;
                existingEntryPoint.TargetLookupName = targetLookupName;
                existingEntryPoint.TargetRelativePathFromRoot = targetRelativePathFromRoot;
                MarkDirty();
                return;
            }

            _entryPoints.Add(new UVSSDKEntryPointDefinition
            {
                EntryPointId = entryPointId,
                DisplayName = displayName,
                TargetLookupName = targetLookupName,
                TargetRelativePathFromRoot = targetRelativePathFromRoot,
            });
            MarkDirty();
        }

        public void RemoveMissingOrDuplicateEntries(HashSet<string> validEntryPointIds, HashSet<string> validDisplayNames)
        {
            bool changed = false;
            for (int i = _entryPoints.Count - 1; i >= 0; i--)
            {
                UVSSDKEntryPointDefinition entryPoint = _entryPoints[i];
                if (entryPoint == null ||
                    string.IsNullOrWhiteSpace(entryPoint.EntryPointId) ||
                    !validEntryPointIds.Contains(entryPoint.EntryPointId) ||
                    string.IsNullOrWhiteSpace(entryPoint.DisplayName) ||
                    !validDisplayNames.Contains(entryPoint.DisplayName))
                {
                    _entryPoints.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
            {
                MarkDirty();
            }
        }

        public bool HasDuplicateDisplayNames(out List<string> duplicateDisplayNames)
        {
            duplicateDisplayNames = _entryPoints
                .Where(entryPoint => entryPoint != null && !string.IsNullOrWhiteSpace(entryPoint.DisplayName))
                .GroupBy(entryPoint => entryPoint.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return duplicateDisplayNames.Count > 0;
        }

        private static Transform FindDescendantByName(Transform parent, string targetName)
        {
            if (parent == null || string.IsNullOrWhiteSpace(targetName))
            {
                return null;
            }

            Transform match = null;
            Transform[] descendants = parent.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                Transform descendant = descendants[i];
                if (descendant == null || descendant == parent || !string.Equals(descendant.gameObject.name, targetName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (match != null)
                {
                    return null;
                }

                match = descendant;
            }

            return match;
        }

        private static Transform FindByRelativePath(Transform root, string relativePath)
        {
            if (root == null || string.IsNullOrWhiteSpace(relativePath))
            {
                return root;
            }

            string[] pathSegments = relativePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            Transform current = root;
            for (int i = 0; i < pathSegments.Length && current != null; i++)
            {
                current = current.Find(pathSegments[i]);
            }

            return current;
        }

        public GameObject ResolveTargetGameObject(GameObject sdkRoot, string entryPointId)
        {
            UVSSDKEntryPointDefinition definition = FindByEntryPointId(entryPointId);
            if (definition == null || sdkRoot == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(definition.TargetLookupName) && string.IsNullOrWhiteSpace(definition.TargetRelativePathFromRoot))
            {
                return sdkRoot;
            }

            Transform targetTransform = FindDescendantByName(sdkRoot.transform, definition.TargetLookupName);
            if (targetTransform == null)
            {
                targetTransform = FindByRelativePath(sdkRoot.transform, definition.TargetRelativePathFromRoot);
            }

            return targetTransform != null ? targetTransform.gameObject : null;
        }

        public void OnBeforeSerialize()
        {
            SanitizeEntries();
        }

        public void OnAfterDeserialize()
        {
            SanitizeEntries();
        }

        private void SanitizeEntries()
        {
            HashSet<string> usedEntryPointIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> usedDisplayNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = _entryPoints.Count - 1; i >= 0; i--)
            {
                UVSSDKEntryPointDefinition entryPoint = _entryPoints[i];
                if (entryPoint == null)
                {
                    _entryPoints.RemoveAt(i);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entryPoint.EntryPointId))
                {
                    entryPoint.EntryPointId = UVSSDKEntryPointConstants.GenerateEntryPointId();
                }

                if (string.IsNullOrWhiteSpace(entryPoint.DisplayName))
                {
                    entryPoint.DisplayName = UVSSDKEntryPointConstants.DefaultDisplayName;
                }

                if (!usedEntryPointIds.Add(entryPoint.EntryPointId) || !usedDisplayNames.Add(entryPoint.DisplayName))
                {
                    _entryPoints.RemoveAt(i);
                }
            }

            _entryPoints.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));
        }

        private void MarkDirty()
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkAllScenesDirty();
#endif
        }
    }
}
