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
        private const float MaxWaitTime = 2f;
        private const float HyperactivityScale = 20f;
        private const float TargetPointMaxDistance = 10f;
        private const float MinTimeSearch = 3f;

        protected override IEnumerator MoveCoroutine()
        {
            while (true)
            {
                while (Paused)
                {
                    yield return null;
                }

                if (!_agent.isOnNavMesh)
                {
                    SnapToNavMesh();
                    yield return null;
                    continue;
                }

                NavMeshHit targetHit;
                while (!NavMesh.SamplePosition(
                    transform.position + Random.onUnitSphere * TargetPointMaxDistance * _agent.gameObject.transform.lossyScale.x, out targetHit, 10f, ~0))
                {
                    yield return null;
                }

                yield return new WaitUntil(() => { return !Paused; });
                _agent.SetDestination(targetHit.position);

                var curDistance = 0f;
                var prevDistance = 0f;
                var curTime = 0f;
                do
                {
                    while (Paused)
                    {
                        yield return null;
                    }

                    prevDistance = curDistance;
                    curDistance = Vector3.Distance(_agent.transform.position, targetHit.position);
                    curTime += Time.deltaTime;
                    yield return null;
                } while (curTime < MinTimeSearch || Mathf.Abs(curDistance - prevDistance) > 0.1f);

                if (_agent.isOnOffMeshLink)
                {
                    _agent.Warp(_agent.transform.position);
                    _agent.ResetPath();
                }

                float minWait = (MinWaitTime + (1f - _hyperactivity) * HyperactivityScale) / 2f;
                float maxWait = (MaxWaitTime + (1f - _hyperactivity) * HyperactivityScale) / 2f;
                
                {
                    float waitTime = Time.time + Random.Range(minWait, maxWait);
                    while (Time.time < waitTime)
                    {
                        yield return null;
                    }
                }

                while (Paused)
                {
                    yield return null;
                }

                yield return StartCoroutine(OnPointReached());
                
                while (Paused)
                {
                    yield return null;
                }
                
                {
                    float waitTime = Time.time + Random.Range(minWait, maxWait);
                    while (Time.time < waitTime)
                    {
                        yield return null;
                    }

                }
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
