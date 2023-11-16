using mtion.room.sdk.compiled;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk
{
    [SelectionBase]
    public class MVirtualDisplayTracker : VirtualComponentTracker
    {
        public override MTIONObjectType GetMTIONObjectType()
        {
            return MTIONObjectType.MTIONSDK_DISPLAY;
        }

        public compiled.DisplayParameters DisplayParams = new compiled.DisplayParameters();

        // Previews
        [SerializeField]
        private GameObject visualizationQuadTemp = null;
        private int gizmoDisplayCurrent = -1;
        public int gizmoDisplaySelection = 0;

        public GameObject GetVisualizationQuad()
        {
            return visualizationQuadTemp;
        }

#if UNITY_EDITOR
        //////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// GIZMO
        //////////////////////////////////////////////////////////////////////////////////////////////////////////
        public int UNITYEDITOR_KeyCodeNum = 2;

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            if (gizmoDisplaySelection == 0 || gizmoDisplaySelection == 1)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(16.0f / 9.0f, 1, 0.01f));
            }

            if (gizmoDisplaySelection == 0 || gizmoDisplaySelection == 2)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(4.0f / 3.0f, 1, 0.01f));
            }
            if (gizmoDisplaySelection == 0 || gizmoDisplaySelection == 3)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(1, 16.0f / 9.0f, 0.01f));
            }
            if (gizmoDisplaySelection == 0 || gizmoDisplaySelection == 4)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(1, 4.0f / 3.0f, 0.01f));
            }
            Gizmos.matrix = Matrix4x4.identity;


            if (visualizationQuadTemp == null)
            {
                visualizationQuadTemp = GameObject.CreatePrimitive(PrimitiveType.Quad);
                visualizationQuadTemp.transform.parent = gameObject.transform;
                visualizationQuadTemp.transform.localPosition = new Vector3(0, 0, 0f);
                visualizationQuadTemp.transform.localRotation = Quaternion.Euler(0, 180, 0);
                visualizationQuadTemp.SetActive(true);
            }

            if (gizmoDisplayCurrent != gizmoDisplaySelection)
            {
                bool updated = false;

                // Default to 16:9 
                if (gizmoDisplaySelection == 0 || gizmoDisplaySelection == 1)
                {
                    updated = true;
                    visualizationQuadTemp.transform.localScale = new Vector3(16.0f / 9.0f, 1, 0.01f);
                    visualizationQuadTemp.SetActive(true);
                }

                if (gizmoDisplaySelection == 2)
                {
                    updated = true;
                    visualizationQuadTemp.transform.localScale = new Vector3(4.0f / 3.0f, 1, 0.01f);
                    visualizationQuadTemp.SetActive(true);
                }
                if (gizmoDisplaySelection == 3)
                {
                    updated = true;
                    visualizationQuadTemp.transform.localScale = new Vector3(1, 16.0f / 9.0f, 0.01f);
                    visualizationQuadTemp.SetActive(true);
                }
                if (gizmoDisplaySelection == 4)
                {
                    updated = true;
                    visualizationQuadTemp.transform.localScale = new Vector3(1, 4.0f / 3.0f, 0.01f);
                    visualizationQuadTemp.SetActive(true);
                }

                if (updated)
                {
                    gizmoDisplayCurrent = gizmoDisplaySelection;
                }
            }
        }
#endif

        // Start is called before the first frame update
        void Start()
        {

        }

        void Update()
        {
 
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// PUBLIC API
        //////////////////////////////////////////////////////////////////////////////////////////////////////////
        public compiled.DisplayComponentType GetDisplayType()
        {
            return DisplayParams.DisplayType;
        }
    }
}
