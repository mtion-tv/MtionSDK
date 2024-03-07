using mtion.room;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room
{
    [DisallowMultipleComponent]
    public sealed class NavMeshAgentTargetMovement : NavMeshAgentMovement
    {
        private Transform _target;

        public void SetTarget(Transform target)
        {
            _target = target;
        }

        protected override IEnumerator MoveCoroutine()
        {
            while (true)
            {
                if (Paused)
                {
                    yield return null;
                    continue;
                }
                
                if (!_agent.isOnNavMesh)
                {
                    yield return null;
                    continue;
                }

                _agent.SetDestination(_target.transform.position);
                yield return null;
            }
        }
    }
}
