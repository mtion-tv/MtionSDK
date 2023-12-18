using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace mtion.room
{
    [DisallowMultipleComponent]
    public sealed class NavMeshAgentPositionMovement : NavMeshAgentMovement
    {
        private Vector3 _position;

        public void SetPosition(Vector3 position)
        {
            _position = position;
        }

        protected override IEnumerator MoveCoroutine()
        {
            while (true)
            {
                if (!_agent.isOnNavMesh)
                {
                    yield return null;
                    continue;
                }

                _agent.SetDestination(_position);
                yield return null;
            }
        }
    }
}
