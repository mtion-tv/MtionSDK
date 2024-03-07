using mtion.room.sdk.action;
using System;
using UnityEngine;

namespace mtion.room.sdk.compiled
{
    [ExecuteInEditMode]
    public sealed class MTIONSDKAvatar : MTIONSDKDescriptorSceneBase
    {
        #region MonoBehaviour implementation
        
#if UNITY_EDITOR
        
        private void Update()
        {
            for (int i = 0; i < ObjectReference.transform.childCount; ++i)
            {
                Transform child = ObjectReference.transform.GetChild(i);
                if (child != null && child.GetComponentInChildren<MActionBehaviourGroup>() == null)
                {
                    if (child.GetComponentInChildren<AvatarAnimations>() == null)
                    {
                        child.gameObject.AddComponent<AvatarAnimations>();
                    }

                    if (child.GetComponentInChildren<AvatarMovementSettings>() == null)
                    {
                        child.gameObject.AddComponent<AvatarMovementSettings>();
                    }
                }
            }
        }
        
#endif
        
        #endregion
    }
}
