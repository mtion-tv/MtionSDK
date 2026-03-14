using System;
using System.Collections.Generic;
using System.Reflection;
using mtion.room.sdk.visualscripting;
using UnityEditor;
using UnityEngine;

namespace mtion.room.sdk
{
    public static class VisualScriptingReflectionUtility
    {
        private static readonly Type ScriptMachineType = FindType("Unity.VisualScripting.ScriptMachine, Unity.VisualScripting.Flow");
        private static readonly Type ScriptGraphAssetType = FindType("Unity.VisualScripting.ScriptGraphAsset, Unity.VisualScripting.Flow");
        private static readonly Type FlowGraphType = FindType("Unity.VisualScripting.FlowGraph, Unity.VisualScripting.Flow");

        public static bool SyncEntryPointRegistryFromVisualScripting(GameObject sdkRoot, out UVSSDKEntryPointRegistry registry, out List<string> errors)
        {
            errors = new List<string>();
            registry = null;
            if (sdkRoot == null)
            {
                errors.Add("The SDK root could not be resolved for UVS graph scan.");
                return false;
            }

            registry = VisualScriptingHostUtility.GetOrCreateEntryPointRegistry(sdkRoot);
            if (registry == null)
            {
                errors.Add("The UVS entry point registry could not be created.");
                return false;
            }

            if (ScriptMachineType == null || ScriptGraphAssetType == null || FlowGraphType == null)
            {
                errors.Add("Unity Visual Scripting runtime types are unavailable for SDK Entry Point scanning.");
                return false;
            }

            HashSet<string> validEntryPointIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> validDisplayNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> usedStableLookupIds = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<Transform, string> lookupNameByTarget = new Dictionary<Transform, string>();
            int repairedEntryPointIdCount = 0;

            Component[] machines = sdkRoot.GetComponentsInChildren(ScriptMachineType, true);
            for (int i = 0; i < machines.Length; i++)
            {
                Component machine = machines[i];
                if (machine == null)
                {
                    continue;
                }

                if (!TryGetMachineGraph(machine, out object graph))
                {
                    continue;
                }

                if (!TryEnumerateGraphUnits(graph, out IEnumerable<object> units))
                {
                    continue;
                }

                foreach (object unit in units)
                {
                    if (unit == null || unit.GetType().FullName != "mtion.room.sdk.visualscripting.UVSSDKEntryPointUnit")
                    {
                        continue;
                    }

                    string entryPointId = unit.GetType().GetField("entryPointId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(unit) as string;
                    string displayName = unit.GetType().GetField("displayName", BindingFlags.Public | BindingFlags.Instance)?.GetValue(unit) as string;
                    if (!EnsureUniqueEntryPointId(unit, validEntryPointIds, ref entryPointId, out bool entryPointIdWasRepaired))
                    {
                        errors.Add("An SDK Entry Point unit could not generate a valid internal id.");
                        continue;
                    }

                    if (entryPointIdWasRepaired)
                    {
                        repairedEntryPointIdCount++;
                    }

                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        displayName = UVSSDKEntryPointConstants.DefaultDisplayName;
                        unit.GetType().GetField("displayName", BindingFlags.Public | BindingFlags.Instance)?.SetValue(unit, displayName);
                    }

                    if (!validDisplayNames.Add(displayName))
                    {
                        errors.Add($"Duplicate SDK Entry Point display name detected: {displayName}");
                        continue;
                    }

                    string targetLookupName = GetOrEnsureTargetLookupName(sdkRoot.transform, machine.transform, lookupNameByTarget, usedStableLookupIds);
                    string relativePath = GetRelativePathFromRoot(sdkRoot.transform, machine.transform);
                    registry.EnsureEntryPoint(entryPointId, displayName, targetLookupName, relativePath);
                    MarkGraphOwnerDirty(machine);
                }
            }

            registry.RemoveMissingOrDuplicateEntries(validEntryPointIds, validDisplayNames);
            EditorUtility.SetDirty(registry);

            if (repairedEntryPointIdCount > 0)
            {
                Debug.Log($"[VisualScriptingReflectionUtility] Regenerated {repairedEntryPointIdCount} duplicate or missing SDK Entry Point internal ids while syncing UVS.");
            }

            return errors.Count == 0;
        }

        private static bool EnsureUniqueEntryPointId(object unit, HashSet<string> validEntryPointIds, ref string entryPointId, out bool wasRepaired)
        {
            wasRepaired = false;
            FieldInfo entryPointIdField = unit?.GetType().GetField("entryPointId", BindingFlags.Public | BindingFlags.Instance);
            if (entryPointIdField == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(entryPointId) && validEntryPointIds.Add(entryPointId))
            {
                return true;
            }

            string originalEntryPointId = entryPointId;
            do
            {
                entryPointId = UVSSDKEntryPointConstants.GenerateEntryPointId();
            }
            while (!validEntryPointIds.Add(entryPointId));

            entryPointIdField.SetValue(unit, entryPointId);
            wasRepaired = !string.Equals(originalEntryPointId, entryPointId, StringComparison.Ordinal);
            return true;
        }

        private static string GetOrEnsureTargetLookupName(
            Transform sdkRoot,
            Transform target,
            Dictionary<Transform, string> lookupNameByTarget,
            HashSet<string> usedStableLookupIds)
        {
            if (sdkRoot == null || target == null || sdkRoot == target)
            {
                return string.Empty;
            }

            if (lookupNameByTarget.TryGetValue(target, out string existingLookupName))
            {
                return existingLookupName;
            }

            UVSSDKStableLookupId stableLookupId = target.GetComponent<UVSSDKStableLookupId>();
            if (stableLookupId == null)
            {
                stableLookupId = target.gameObject.AddComponent<UVSSDKStableLookupId>();
            }

            stableLookupId.EnsureStableLookupId();
            while (!usedStableLookupIds.Add(stableLookupId.StableLookupId))
            {
                stableLookupId.StableLookupId = UVSSDKEntryPointConstants.GenerateStableLookupId();
            }

            string targetLookupName = stableLookupId.StableLookupObjectName;
            if (!string.Equals(target.name, targetLookupName, StringComparison.Ordinal))
            {
                target.name = targetLookupName;
                EditorUtility.SetDirty(target.gameObject);
            }

            EditorUtility.SetDirty(stableLookupId);
            lookupNameByTarget[target] = targetLookupName;
            return targetLookupName;
        }

        private static bool TryGetMachineGraph(Component machine, out object graph)
        {
            graph = null;
            if (machine == null)
            {
                return false;
            }

            PropertyInfo graphProperty = machine.GetType().GetProperty("graph", BindingFlags.Public | BindingFlags.Instance);
            graph = graphProperty?.GetValue(machine);
            return graph != null;
        }

        private static void MarkGraphOwnerDirty(Component machine)
        {
            if (machine == null)
            {
                return;
            }

            EditorUtility.SetDirty(machine);

            PropertyInfo nestProperty = machine.GetType().GetProperty("nest", BindingFlags.Public | BindingFlags.Instance);
            object nest = nestProperty?.GetValue(machine);
            if (nest == null)
            {
                return;
            }

            PropertyInfo sourceProperty = nest.GetType().GetProperty("source", BindingFlags.Public | BindingFlags.Instance);
            object graphSource = sourceProperty?.GetValue(nest);
            if (graphSource == null)
            {
                return;
            }

            if (!string.Equals(graphSource.ToString(), "Macro", StringComparison.Ordinal))
            {
                return;
            }

            PropertyInfo macroProperty = nest.GetType().GetProperty("macro", BindingFlags.Public | BindingFlags.Instance);
            UnityEngine.Object macro = macroProperty?.GetValue(nest) as UnityEngine.Object;
            if (macro == null)
            {
                return;
            }

            EditorUtility.SetDirty(macro);
            AssetDatabase.SaveAssetIfDirty(macro);
        }

        private static bool TryEnumerateGraphUnits(object graph, out IEnumerable<object> units)
        {
            units = null;
            if (graph == null)
            {
                return false;
            }

            PropertyInfo unitsProperty = graph.GetType().GetProperty("units", BindingFlags.Public | BindingFlags.Instance);
            object graphUnits = unitsProperty?.GetValue(graph);
            if (graphUnits is System.Collections.IEnumerable enumerable)
            {
                List<object> output = new List<object>();
                foreach (object item in enumerable)
                {
                    output.Add(item);
                }

                units = output;
                return true;
            }

            return false;
        }

        private static string GetRelativePathFromRoot(Transform root, Transform target)
        {
            if (root == null || target == null || root == target)
            {
                return string.Empty;
            }

            Stack<string> pathSegments = new Stack<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                pathSegments.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", pathSegments.ToArray());
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
