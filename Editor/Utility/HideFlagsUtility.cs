using UnityEngine;
using UnityEditor;

namespace mtion.room.sdk
{
    public static class MtionHideFlagsUtility
    {
        [MenuItem("MTION SDK/Tools/Show All Objects")]
        private static void ShowAll()
        {
            var allGameObjects = Object.FindObjectsOfType<GameObject>(true);
            foreach (var go in allGameObjects)
            {
                switch (go.hideFlags)
                {
                    case HideFlags.HideAndDontSave:
                        go.hideFlags = HideFlags.DontSave;
                        break;
                    case HideFlags.HideInHierarchy:
                    case HideFlags.HideInInspector:
                        go.hideFlags = HideFlags.None;
                        break;
                }
            }
        }
    }
}