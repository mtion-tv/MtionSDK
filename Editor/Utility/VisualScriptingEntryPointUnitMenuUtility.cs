using UnityEditor;
using UnityEngine;

namespace mtion.room.sdk
{
    public static class VisualScriptingEntryPointUnitMenuUtility
    {
        [MenuItem("GameObject/mtion/Visual Scripting/SDK Entry Point", false, 10)]
        private static void CreateEntryPointEmitter(MenuCommand command)
        {
            GameObject target = command.context as GameObject;
            if (target == null)
            {
                target = Selection.activeGameObject;
            }

            if (target == null)
            {
                EditorUtility.DisplayDialog("No Valid Target", "Select a GameObject inside the VisualScriptingContainer before adding an SDK Entry Point.", "Close");
                return;
            }

            EditorUtility.DisplayDialog(
                "Add From Graph",
                "SDK Entry Point is a Unity Visual Scripting graph unit, not a GameObject component. Open the Script Graph on the selected object, add 'SDK Entry Point' from the fuzzy finder, then run Configure UVS to sync the registry.",
                "Close");
        }

        [MenuItem("GameObject/mtion/Visual Scripting/SDK Entry Point", true)]
        private static bool ValidateCreateEntryPointEmitter()
        {
            GameObject target = Selection.activeGameObject;
            return target != null;
        }

    }
}
