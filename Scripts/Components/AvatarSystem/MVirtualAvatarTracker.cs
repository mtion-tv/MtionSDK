using mtion.room.sdk.compiled;
using UnityEngine;

namespace mtion.room.sdk
{
    public sealed class MVirtualAvatarTracker : MTIONSDKAssetBase
    {
        
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);

            Gizmos.color = Color.black;
            UnityEditor.Handles.BeginGUI();
            UnityEditor.Handles.Label(transform.position + -transform.up * 0.04f, gameObject.name);
            UnityEditor.Handles.EndGUI();
        }
#endif
    }
}
