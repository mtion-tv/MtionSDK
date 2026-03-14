using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using System.Reflection;
using System.Text.RegularExpressions;
#endif

namespace mtion.room.sdk
{
    public enum VisualScriptingExportTarget
    {
        PortablePrefab,
        RoomScene,
        EnvironmentScene,
    }

    [Serializable]
    public sealed class VisualScriptingInspectionReport
    {
        private readonly HashSet<string> _scopeKinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _referencedAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _warnings = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _errors = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _missingTypeNames = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _unsupportedTypeNames = new HashSet<string>(StringComparer.Ordinal);

        public bool HasVisualScripting { get; set; }
        public IReadOnlyCollection<string> ScopeKinds => _scopeKinds;
        public IReadOnlyCollection<string> ReferencedAssetPaths => _referencedAssetPaths;
        public IReadOnlyCollection<string> Warnings => _warnings;
        public IReadOnlyCollection<string> Errors => _errors;
        public IReadOnlyCollection<string> MissingTypeNames => _missingTypeNames;
        public IReadOnlyCollection<string> UnsupportedTypeNames => _unsupportedTypeNames;

        public bool UsesScope(string scopeKind)
        {
            return _scopeKinds.Contains(scopeKind);
        }

        public void AddScope(string scopeKind)
        {
            if (!string.IsNullOrWhiteSpace(scopeKind))
            {
                _scopeKinds.Add(scopeKind);
            }
        }

        public void AddReferencedAssetPath(string assetPath)
        {
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                _referencedAssetPaths.Add(assetPath.Replace('\\', '/'));
            }
        }

        public void AddWarning(string warning)
        {
            if (!string.IsNullOrWhiteSpace(warning))
            {
                _warnings.Add(warning);
            }
        }

        public void AddError(string error)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                _errors.Add(error);
            }
        }

        public void AddMissingType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName) || !_missingTypeNames.Add(typeName))
            {
                return;
            }

            AddError($"Visual scripting references missing type '{typeName}'. Remove stale or unsupported units and regenerate Unity Visual Scripting data.");
        }

        public void AddUnsupportedType(string typeName, string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(typeName) || !_unsupportedTypeNames.Add(typeName))
            {
                return;
            }

            AddError($"Visual scripting references unsupported type '{typeName}' from assembly '{assemblyName}'. Only standard Unity units and first-party MTION app facades are supported.");
        }

        public List<string> GetOrderedScopes()
        {
            return _scopeKinds.OrderBy(scope => scope, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    public static class VisualScriptingSupportUtil
    {
        public const string HostObjectName = "VisualScriptingContainer";

        private static readonly string[] ScopeNames =
        {
            "Flow",
            "Graph",
            "Object",
            "Scene",
            "Application",
            "Saved",
        };

#if UNITY_EDITOR
        private static readonly Regex TypeReferencePattern = new Regex(@"(?<![A-Za-z0-9_])(?:[A-Za-z_][A-Za-z0-9_]*\.)+[A-Z][A-Za-z0-9_+`]*(?![A-Za-z0-9_])", RegexOptions.Compiled);
        private static readonly Dictionary<string, Type> ResolvedTypeCache = new Dictionary<string, Type>(StringComparer.Ordinal);
        private static readonly HashSet<string> MissingTypeCache = new HashSet<string>(StringComparer.Ordinal);
#endif


        public static bool ContainsVisualScriptingComponent(GameObject root)
        {
            if (root == null)
            {
                return false;
            }

            return GetVisualScriptingComponents(root).Count > 0;
        }

        public static bool IsVisualScriptingHostObject(GameObject gameObject)
        {
            return gameObject != null && string.Equals(gameObject.name, HostObjectName, StringComparison.Ordinal);
        }

        public static List<Component> GetVisualScriptingComponents(GameObject root)
        {
            List<Component> components = new List<Component>();
            if (root == null)
            {
                return components;
            }

            Component[] childComponents = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < childComponents.Length; i++)
            {
                if (IsVisualScriptingComponent(childComponents[i]))
                {
                    components.Add(childComponents[i]);
                }
            }

            return components;
        }

        public static List<Component> GetVisualScriptingComponents(Scene scene)
        {
            List<Component> components = new List<Component>();
            if (!scene.IsValid())
            {
                return components;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                components.AddRange(GetVisualScriptingComponents(roots[i]));
            }

            return components;
        }

        public static List<GameObject> GetVisualScriptingSupportRoots(Scene scene, GameObject primaryRoot)
        {
            List<GameObject> retainedRoots = new List<GameObject>();
            if (!scene.IsValid())
            {
                return retainedRoots;
            }

            GameObject[] sceneRoots = scene.GetRootGameObjects();
            for (int i = 0; i < sceneRoots.Length; i++)
            {
                GameObject sceneRoot = sceneRoots[i];
                if (sceneRoot == null || sceneRoot == primaryRoot)
                {
                    continue;
                }

                if (ContainsVisualScriptingComponent(sceneRoot))
                {
                    retainedRoots.Add(sceneRoot);
                }
            }

            return retainedRoots;
        }

        public static void DisableVisualScriptingBehaviours(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Behaviour[] behaviours = root.GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                Behaviour behaviour = behaviours[i];
                if (behaviour == null || !IsVisualScriptingType(behaviour.GetType()))
                {
                    continue;
                }

                behaviour.enabled = false;
            }
        }

        public static void DisableVisualScriptingBehaviours(Scene scene)
        {
            if (!scene.IsValid())
            {
                return;
            }

            GameObject[] sceneRoots = scene.GetRootGameObjects();
            for (int i = 0; i < sceneRoots.Length; i++)
            {
                DisableVisualScriptingBehaviours(sceneRoots[i]);
            }
        }

        public static bool IsVisualScriptingComponent(Component component)
        {
            return component != null && IsVisualScriptingType(component.GetType());
        }

        public static bool IsVisualScriptingType(Type type)
        {
            if (type == null)
            {
                return false;
            }

            string assemblyName = type.Assembly.GetName().Name ?? string.Empty;
            if (assemblyName.StartsWith("Unity.VisualScripting", StringComparison.Ordinal))
            {
                return true;
            }

            string typeNamespace = type.Namespace ?? string.Empty;
            return typeNamespace.StartsWith("Unity.VisualScripting", StringComparison.Ordinal);
        }

#if UNITY_EDITOR

        public static VisualScriptingInspectionReport InspectGameObjectForExport(GameObject root, VisualScriptingExportTarget target)
        {
            VisualScriptingInspectionReport report = new VisualScriptingInspectionReport();
            if (root == null)
            {
                return report;
            }

            HashSet<int> visitedObjects = new HashSet<int>();
            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                InspectUnityObject(components[i], report, visitedObjects);
            }

            ApplyExportRules(report, target);
            return report;
        }

        public static VisualScriptingInspectionReport InspectSceneForExport(Scene scene, VisualScriptingExportTarget target)
        {
            VisualScriptingInspectionReport report = new VisualScriptingInspectionReport();
            if (!scene.IsValid())
            {
                return report;
            }

            HashSet<int> visitedObjects = new HashSet<int>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Component[] components = roots[i].GetComponentsInChildren<Component>(true);
                for (int j = 0; j < components.Length; j++)
                {
                    InspectUnityObject(components[j], report, visitedObjects);
                }
            }

            ApplyExportRules(report, target);
            return report;
        }

        private static void InspectUnityObject(UnityEngine.Object inspectedObject, VisualScriptingInspectionReport report, HashSet<int> visitedObjects)
        {
            if (inspectedObject == null)
            {
                return;
            }

            int instanceId = inspectedObject.GetInstanceID();
            if (!visitedObjects.Add(instanceId))
            {
                return;
            }

            bool isVisualScriptingObject = inspectedObject is Component component
                ? IsVisualScriptingComponent(component)
                : IsVisualScriptingType(inspectedObject.GetType());

            if (!isVisualScriptingObject)
            {
                return;
            }

            report.HasVisualScripting = true;

            SerializedObject serializedObject = new SerializedObject(inspectedObject);
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.Next(enterChildren))
            {
                enterChildren = true;
                switch (iterator.propertyType)
                {
                    case SerializedPropertyType.String:
                        AnalyzeSerializedString(iterator.stringValue, report);
                        break;
                    case SerializedPropertyType.ObjectReference:
                        AnalyzeObjectReference(iterator.objectReferenceValue, report, visitedObjects);
                        break;
                }
            }
        }

        private static void AnalyzeObjectReference(UnityEngine.Object referencedObject, VisualScriptingInspectionReport report, HashSet<int> visitedObjects)
        {
            if (referencedObject == null || referencedObject is MonoScript || !EditorUtility.IsPersistent(referencedObject))
            {
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(referencedObject);
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            report.AddReferencedAssetPath(assetPath);

            if (IsVisualScriptingType(referencedObject.GetType()))
            {
                InspectUnityObject(referencedObject, report, visitedObjects);
            }
        }

        private static void AnalyzeSerializedString(string serializedValue, VisualScriptingInspectionReport report)
        {
            if (string.IsNullOrWhiteSpace(serializedValue))
            {
                return;
            }

            if (serializedValue.IndexOf("Unity.VisualScripting", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                report.HasVisualScripting = true;
            }

            for (int i = 0; i < ScopeNames.Length; i++)
            {
                string scopeName = ScopeNames[i];
                if (ContainsSerializedEnumValue(serializedValue, "Kind", scopeName) ||
                    ContainsSerializedEnumValue(serializedValue, "kind", scopeName) ||
                    ContainsSerializedEnumValue(serializedValue, "scope", scopeName))
                {
                    report.AddScope(scopeName);
                }
            }

            AnalyzeSerializedTypeReferences(serializedValue, report);
        }

        private static bool ContainsSerializedEnumValue(string serializedValue, string key, string value)
        {
            return serializedValue.IndexOf($"\"{key}\":\"{value}\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   serializedValue.IndexOf($"\"{key}\": \"{value}\"", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ApplyExportRules(VisualScriptingInspectionReport report, VisualScriptingExportTarget target)
        {
            if (!report.HasVisualScripting)
            {
                return;
            }

            switch (target)
            {
                case VisualScriptingExportTarget.PortablePrefab:
                    AddPortablePrefabScopeErrors(report);
                    break;
                case VisualScriptingExportTarget.RoomScene:
                    if (report.UsesScope("Application"))
                    {
                        report.AddWarning("Room visual scripting should avoid Application variables. Prefer room Scene scope or app-exposed services instead.");
                    }
                    if (report.UsesScope("Saved"))
                    {
                        report.AddWarning("Room visual scripting should avoid Saved variables for transient room state. Prefer room Scene scope instead.");
                    }
                    break;
                case VisualScriptingExportTarget.EnvironmentScene:
                    if (report.UsesScope("Application"))
                    {
                        report.AddWarning("Environment visual scripting should avoid Application variables. Prefer keeping environment logic self-contained.");
                    }
                    if (report.UsesScope("Saved"))
                    {
                        report.AddWarning("Environment visual scripting should avoid Saved variables for transient runtime state.");
                    }
                    report.AddWarning("Environment visual scripting is supported, but room logic should remain the primary scene-scoped interaction surface.");
                    break;
            }
        }

        private static void AddPortablePrefabScopeErrors(VisualScriptingInspectionReport report)
        {
            if (report.UsesScope("Scene"))
            {
                report.AddError("Portable asset visual scripting cannot use Scene variables. Use Object or Graph scope instead.");
            }
            if (report.UsesScope("Application"))
            {
                report.AddError("Portable asset visual scripting cannot use Application variables. Use app-exposed services instead of shared variable state.");
            }
            if (report.UsesScope("Saved"))
            {
                report.AddError("Portable asset visual scripting cannot use Saved variables. Keep asset logic self-contained with Object or Graph scope.");
            }
        }

        private static void AnalyzeSerializedTypeReferences(string serializedValue, VisualScriptingInspectionReport report)
        {
            MatchCollection matches = TypeReferencePattern.Matches(serializedValue);
            for (int i = 0; i < matches.Count; i++)
            {
                string candidate = matches[i].Value;
                if (!TryResolveTypeCandidate(candidate, out Type resolvedType))
                {
                    if (LooksLikeMissingTypeReference(candidate))
                    {
                        report.AddMissingType(candidate);
                    }

                    continue;
                }

                if (!IsSupportedVisualScriptingTypeReference(resolvedType))
                {
                    report.AddUnsupportedType(resolvedType.FullName ?? candidate, resolvedType.Assembly.GetName().Name);
                }
            }
        }

        public static bool TryResolveTypeCandidate(string candidate, out Type resolvedType)
        {
            resolvedType = null;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            if (ResolvedTypeCache.TryGetValue(candidate, out resolvedType))
            {
                return resolvedType != null;
            }

            if (MissingTypeCache.Contains(candidate))
            {
                return false;
            }

            string currentCandidate = candidate;
            while (!string.IsNullOrWhiteSpace(currentCandidate) && currentCandidate.Contains("."))
            {
                if (TryResolveTypeName(currentCandidate, out resolvedType))
                {
                    ResolvedTypeCache[candidate] = resolvedType;
                    return true;
                }

                int lastSegmentSeparator = currentCandidate.LastIndexOf('.');
                if (lastSegmentSeparator <= 0)
                {
                    break;
                }

                currentCandidate = currentCandidate.Substring(0, lastSegmentSeparator);
            }

            MissingTypeCache.Add(candidate);
            return false;
        }

        public static bool IsSupportedVisualScriptingTypeReference(Type resolvedType)
        {
            if (resolvedType == null)
            {
                return false;
            }

            string assemblyName = resolvedType.Assembly.GetName().Name ?? string.Empty;
            if (assemblyName.Equals("mscorlib", StringComparison.Ordinal) ||
                assemblyName.Equals("netstandard", StringComparison.Ordinal) ||
                assemblyName.StartsWith("System", StringComparison.Ordinal) ||
                assemblyName.StartsWith("Unity", StringComparison.Ordinal) ||
                assemblyName.Equals("mtion", StringComparison.Ordinal) ||
                assemblyName.StartsWith("MTIONStudio", StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        private static bool TryResolveTypeName(string typeName, out Type resolvedType)
        {
            resolvedType = Type.GetType(typeName, false);
            if (resolvedType != null)
            {
                return true;
            }

            if (TryResolveBuiltInGenericType(typeName, out resolvedType))
            {
                return true;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                try
                {
                    resolvedType = assembly.GetType(typeName, false);
                    if (resolvedType != null)
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        private static bool TryResolveBuiltInGenericType(string typeName, out Type resolvedType)
        {
            switch (typeName)
            {
                case "System.Collections.Generic.List":
                    resolvedType = typeof(List<>);
                    return true;
                case "System.Collections.Generic.Dictionary":
                    resolvedType = typeof(Dictionary<,>);
                    return true;
                case "System.Collections.Generic.HashSet":
                    resolvedType = typeof(HashSet<>);
                    return true;
                default:
                    resolvedType = null;
                    return false;
            }
        }

        private static bool LooksLikeMissingTypeReference(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            return candidate.StartsWith("Aura2API.", StringComparison.Ordinal) ||
                   candidate.StartsWith("UnityChan.", StringComparison.Ordinal) ||
                   candidate.StartsWith("AssemblyCSharp.", StringComparison.Ordinal) ||
                   candidate.IndexOf("API.", StringComparison.Ordinal) > 0;
        }
#endif
    }
}
