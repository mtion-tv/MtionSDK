using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using mtion.room.sdk.visualscripting;

namespace mtion.room.sdk
{
    public static class VisualScriptingHostUtility
    {
        public const string HostObjectName = VisualScriptingSupportUtil.HostObjectName;


        public static GameObject GetHost(GameObject sdkRoot)
        {
            if (sdkRoot == null)
            {
                return null;
            }

            Transform existingHost = sdkRoot.transform.Find(HostObjectName);
            return existingHost != null ? existingHost.gameObject : null;
        }

        public static GameObject GetOrCreateHost(GameObject sdkRoot)
        {
            if (sdkRoot == null)
            {
                return null;
            }

            GameObject host = GetHost(sdkRoot);
            if (host != null)
            {
                NormalizeHostTransform(host);
                return host;
            }

            host = new GameObject(HostObjectName);
            Undo.RegisterCreatedObjectUndo(host, "Create UVS Host");
            host.transform.SetParent(sdkRoot.transform, false);
            host.transform.SetSiblingIndex(0);
            NormalizeHostTransform(host);
            EditorUtility.SetDirty(host);
            EditorUtility.SetDirty(sdkRoot);
            EditorSceneManager.MarkAllScenesDirty();
            return host;
        }

        public static UVSSDKEntryPointRegistry GetOrCreateEntryPointRegistry(GameObject sdkRoot)
        {
            GameObject host = GetOrCreateHost(sdkRoot);
            if (host == null)
            {
                return null;
            }

            UVSSDKEntryPointRegistry registry = host.GetComponent<UVSSDKEntryPointRegistry>();
            if (registry == null)
            {
                registry = Undo.AddComponent<UVSSDKEntryPointRegistry>(host);
                EditorUtility.SetDirty(registry);
                EditorSceneManager.MarkAllScenesDirty();
            }

            return registry;
        }


        public static List<Component> GetRootLevelVisualScriptingComponents(GameObject sdkRoot)
        {
            List<Component> components = new List<Component>();
            if (sdkRoot == null)
            {
                return components;
            }

            Component[] rootComponents = sdkRoot.GetComponents<Component>();
            for (int i = 0; i < rootComponents.Length; i++)
            {
                Component component = rootComponents[i];
                if (VisualScriptingSupportUtil.IsVisualScriptingComponent(component))
                {
                    components.Add(component);
                }
            }

            return components;
        }

        public static bool HasRootLevelVisualScriptingComponents(GameObject sdkRoot)
        {
            return GetRootLevelVisualScriptingComponents(sdkRoot).Count > 0;
        }

        public static bool NormalizePlacement(GameObject sdkRoot, bool createHostIfMissing, out GameObject host, out List<string> migratedComponentNames, out string error)
        {
            migratedComponentNames = new List<string>();
            host = null;
            error = null;

            if (sdkRoot == null)
            {
                error = "The SDK object root could not be resolved for UVS setup.";
                return false;
            }

            List<Component> rootComponents = GetRootLevelVisualScriptingComponents(sdkRoot);
            if (createHostIfMissing || rootComponents.Count > 0)
            {
                host = GetOrCreateHost(sdkRoot);
            }
            else
            {
                host = GetHost(sdkRoot);
            }

            if (host == null)
            {
                return true;
            }

            for (int i = 0; i < rootComponents.Count; i++)
            {
                Component component = rootComponents[i];
                if (component == null)
                {
                    continue;
                }

                if (!TryMoveComponentToHost(component, host, out string moveError))
                {
                    error = moveError;
                    return false;
                }

                migratedComponentNames.Add(component.GetType().Name);
            }

            NormalizeHostTransform(host);
            EditorUtility.SetDirty(host);
            EditorUtility.SetDirty(sdkRoot);
            EditorSceneManager.MarkAllScenesDirty();
            return true;
        }

        private static bool TryMoveComponentToHost(Component sourceComponent, GameObject host, out string error)
        {
            error = null;
            if (sourceComponent == null)
            {
                error = "The UVS component to migrate is missing.";
                return false;
            }

            if (host == null)
            {
                error = "The UVS host is missing.";
                return false;
            }

            try
            {
                ComponentUtility.CopyComponent(sourceComponent);
                ComponentUtility.PasteComponentAsNew(host);
                Undo.DestroyObjectImmediate(sourceComponent);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to move UVS component '{sourceComponent.GetType().Name}' to '{HostObjectName}': {ex.Message}";
                return false;
            }
        }

        private static void NormalizeHostTransform(GameObject host)
        {
            if (host == null)
            {
                return;
            }

            host.transform.localPosition = Vector3.zero;
            host.transform.localRotation = Quaternion.identity;
            host.transform.localScale = Vector3.one;
        }
    }
}
