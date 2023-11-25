using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using mtion.room.sdk.compiled;

namespace mtion.room.sdk
{
    [SelectionBase]
    [ExecuteInEditMode]
    public class MVirtualAssetTracker : MTIONSDKAssetBase
    {
        [HideInInspector]
        public compiled.AssetParameters AssetParams = new compiled.AssetParameters();

#if UNITY_EDITOR
        protected override void Awake()
        {
            MigrateFromDescriptorSO();
        }
        
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);

            Gizmos.color = Color.black;
            UnityEditor.Handles.BeginGUI();
            UnityEditor.Handles.Label(transform.position + -transform.up * 0.04f, gameObject.name);
            UnityEditor.Handles.EndGUI();
        }

        private void OnDrawGizmosSelected()
        {
           
        }

        private void OnTransformChildrenChanged()
        {
            if (transform.childCount > 0)
            {
                Name = transform.GetChild(0).name;
            }
        }
#endif

        public compiled.VirtualObjectComponentType GetAssetType()
        {
            return AssetParams.VirtualObjectType;
        }
    }
}
