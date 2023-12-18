using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace mtion.room
{
    [DisallowMultipleComponent]
    public sealed class NavMeshAgentRandomMovement : NavMeshAgentMovement
    {
        private const float MinWaitTime = 0f;
        private const float MaxWaitTime = 5f;
        private const float TargetPointMaxDistance = 25f;
        private const float MinTimeSearch = 3f;

        protected override IEnumerator MoveCoroutine()
        {
            while (true)
            {
                if (!_agent.isOnNavMesh)
                {
                    SnapToNavMesh();
                    yield return null;
                    continue;
                }

                NavMeshHit targetHit;
                while (!NavMesh.SamplePosition(
                    transform.position + Random.insideUnitSphere * TargetPointMaxDistance, out targetHit, 10f, ~0))
                {
                    yield return null;
                }
                _agent.SetDestination(targetHit.position);

                var curDistance = 0f;
                var prevDistance = 0f;
                var curTime = 0f;
                do
                {
                    prevDistance = curDistance;
                    curDistance = Vector3.Distance(_agent.transform.position, targetHit.position);
                    curTime += Time.deltaTime;
                    yield return null;
                } while (curTime < MinTimeSearch || Mathf.Abs(curDistance - prevDistance) > 0.1f);

                yield return new WaitForSeconds(Random.Range(MinWaitTime, MaxWaitTime));
            }
        }

        private void SnapToNavMesh()
        {
            var found = NavMesh.SamplePosition(_agent.transform.position, out var navMeshHit, 100f, ~0);
            if (found)
            {
                _agent.Warp(navMeshHit.position);
            }
        }
    }
}
