using System;
using System.Collections.Generic;
using mtion.room.sdk.visualscripting;
using UnityEditor;
using UnityEngine;

namespace mtion.room.sdk.editor
{
    [CustomEditor(typeof(UVSSDKEntryPointRegistry))]
    public sealed class UVSSDKEntryPointRegistryEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            UVSSDKEntryPointRegistry registry = target as UVSSDKEntryPointRegistry;
            if (registry == null)
            {
                return;
            }

            if (registry.HasDuplicateDisplayNames(out List<string> duplicateDisplayNames))
            {
                EditorGUILayout.HelpBox(
                    $"Duplicate SDK Entry Point display names are not allowed: {string.Join(", ", duplicateDisplayNames)}",
                    MessageType.Error);
            }

            GUILayout.Space(8);
            if (GUILayout.Button("Refresh From UVS Graphs"))
            {
                GameObject sdkRoot = registry.transform.parent != null ? registry.transform.parent.gameObject : registry.gameObject;
                if (!VisualScriptingReflectionUtility.SyncEntryPointRegistryFromVisualScripting(sdkRoot, out _, out List<string> errors))
                {
                    EditorUtility.DisplayDialog("UVS Entry Point Sync Failed", string.Join("\n", errors), "Close");
                }
            }
        }
    }
}
