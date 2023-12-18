using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace mtion.room
{
    public static class NavMeshUtility
    {
        public static bool ConvertLinkPosToNavMeshPos(Vector3 linkPos, out Vector3 navMeshPos)
        {
            if (Physics.Raycast(linkPos + Vector3.up, Vector3.down, out var raycastHit, 10f, ~0) &&
                NavMesh.SamplePosition(raycastHit.point, out var navHit, 1f, ~0))
            {
                navMeshPos = navHit.position;
                return true;
            }

            navMeshPos = Vector3.zero;
            return false;
        }
    }
}
