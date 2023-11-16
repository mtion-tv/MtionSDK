using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace mtion.room.sdk.compiled
{
    public class MSDKSerializableBaseSO : ScriptableObject
    {
        [SerializeField] private string _guid;
        public string Guid => _guid;
        [SerializeField, ReadOnly] public MTIONObjectType ObjectType;

#if UNITY_EDITOR
        public void SetGUID(string guid)
        {
            _guid = guid;
        }
#endif
    }
}
