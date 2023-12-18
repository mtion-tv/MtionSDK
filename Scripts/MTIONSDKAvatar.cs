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
            if (ObjectReference.transform.childCount > 0)
            {
                Transform objectReferenceChild = ObjectReference.transform.GetChild(0);
                if (objectReferenceChild.GetComponent<MTIONNavMeshAgent>() == null)
                {
                    MTIONNavMeshAgent agent = objectReferenceChild.gameObject.AddComponent<MTIONNavMeshAgent>();
                }
            }
        }
        
#endif
        
        #endregion
    }
}
