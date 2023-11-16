using mtion.room.sdk.compiled;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace mtion.room.sdk
{
    public abstract class VirtualComponentTracker : MonoBehaviour
    {
        public abstract MTIONObjectType GetMTIONObjectType();

        [SerializeField] public string GUID;

#if UNITY_EDITOR
        public void GenerateNewGUID()
        {
            GUID = System.Guid.NewGuid().ToString("n");
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
