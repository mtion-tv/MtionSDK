using mtion.room.sdk.compiled;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk
{
    [SelectionBase]
    public class MVirtualLightingTracker : VirtualComponentTracker
    {
        public override MTIONObjectType GetMTIONObjectType()
        {
            return MTIONObjectType.MTIONSDK_LIGHT;
        }

        public compiled.LightingParameters LightParams = new compiled.LightingParameters();

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(transform.position, 0.03f);

            Gizmos.color = Color.black;
            UnityEditor.Handles.BeginGUI();
            UnityEditor.Handles.Label(transform.position + -transform.up * 0.04f, gameObject.name);
            UnityEditor.Handles.EndGUI();
        }
#endif

        //////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// PUBLIC API
        //////////////////////////////////////////////////////////////////////////////////////////////////////////
        
        public compiled.LightingComponentType GetType()
        {
            return LightParams.LightType;
        }
    }
}
