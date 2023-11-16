using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace mtion.room.sdk
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public class CenterTargetInFrame : MonoBehaviour
    {
        public void CenterOnTarget(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            List<MeshFilter> meshFilters = new List<MeshFilter>();
            meshFilters.AddRange(target.GetComponentsInChildren<MeshFilter>());
            List<MeshRenderer> meshRenderers = new List<MeshRenderer>();
            meshRenderers.AddRange(target.GetComponentsInChildren<MeshRenderer>());

            Bounds bounds;
            if (MeshFiltersAreReadable(meshFilters))
            {
                //Calculate the target bounds using the vertices provided they are readable
                bounds = GetBoundsUsingMeshFilters(meshFilters, target.transform);
            }
            else
            {
                //Otherwise do it using the renderer bounds which doesn't account for world space rotation
                bounds = GetBoundsUsingMeshRenderers(meshRenderers);
            }

            Camera camera = GetComponent<Camera>();
            float marginPercentage = 1.25f;

            Vector3 centerAtFront = new Vector3(bounds.center.x, bounds.center.y, -bounds.max.z);
            Vector3 centerAtFrontTop = new Vector3(bounds.center.x, bounds.max.y, -bounds.max.z);
            float centerToTopDist = (centerAtFrontTop - centerAtFront).magnitude;
            float minDistance = (centerToTopDist * marginPercentage) / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -minDistance - bounds.max.z);
            camera.transform.LookAt(bounds.center);
            camera.nearClipPlane = minDistance * 0.5f;
        }

        private bool MeshFiltersAreReadable(List<MeshFilter> meshFilters)
        {
            foreach (MeshFilter meshFilter in meshFilters)
            {
                if (!meshFilter.sharedMesh.isReadable)
                {
                    return false;
                }
            }

            return true;
        }

        private Bounds GetBoundsUsingMeshFilters(List<MeshFilter> meshFilters, Transform targetTransform)
        {
            List<Vector3> verticesList = new List<Vector3>();
            foreach (MeshFilter meshFilter in meshFilters)
            {
                verticesList.AddRange(meshFilter.sharedMesh.vertices);
            }

            //Find the max and min x,y,z values using all the vertices in order to calculate the bounds
            var vertices = verticesList.ToArray();
            var min = targetTransform.TransformPoint(vertices[0]);
            var max = min;
            for (var i = 1; i < vertices.Length; ++i)
            {
                var vertex = targetTransform.TransformPoint(vertices[i]);
                for (var n = 0; n < 3; ++n)
                {
                    max[n] = Mathf.Max(vertex[n], max[n]);
                    min[n] = Mathf.Min(vertex[n], min[n]);
                }
            }
            
            Bounds bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        private Bounds GetBoundsUsingMeshRenderers(List<MeshRenderer> meshRenderers)
        {
            Bounds bounds = new Bounds();
            foreach (MeshRenderer meshRenderer in meshRenderers)
            {
                bounds.Encapsulate(meshRenderer.bounds);
            }

            return bounds;
        }
    }
}
