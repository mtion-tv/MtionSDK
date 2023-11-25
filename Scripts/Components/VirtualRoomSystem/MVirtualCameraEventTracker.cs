using mtion.room.sdk.compiled;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk
{
    public class VirtualCameraComparer : IComparer
    {
        public int Compare(object x, object y)
        {
            MVirtualCameraEventTracker vc1 = (MVirtualCameraEventTracker)x;
            MVirtualCameraEventTracker vc2 = (MVirtualCameraEventTracker)y;

            if (vc1.OrderPrecedence < vc2.OrderPrecedence)
            {
                return -1;
            }

            if (vc1.OrderPrecedence > vc2.OrderPrecedence)
            {
                return 1;
            }

            return 0;
        }
    }

    public class MVirtualCameraEventTracker : VirtualComponentTracker
    {
        public override MTIONObjectType GetMTIONObjectType()
        {
            return MTIONObjectType.MTIONSDK_CAMERA;
        }

        public int OrderPrecedence = 0;
        public compiled.CameraParameters CameraParams = new compiled.CameraParameters();

#if UNITY_EDITOR
        public int UNITYEDITOR_KeyCodeNum = 2;
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(transform.position, 0.03f);

            Gizmos.color = Color.black;
            UnityEditor.Handles.BeginGUI();
            UnityEditor.Handles.Label(transform.position + -transform.up * 0.04f, gameObject.name);
            UnityEditor.Handles.EndGUI();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, 0.1f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawFrustum(Vector3.zero, CameraParams.VerticalFoV, CameraParams.NearPlane, CameraParams.FarPlane, CameraParams.AspectRatio);

        }
#endif
    }
}
