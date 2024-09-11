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
        private const float TargetPointMaxDistance = 5f;
        private const float MinTimeSearch = 3f;

        [SerializeField] private float SearchAngleDeg = 45;

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
                float mutlipler = Random.Range(0.5f, 2.0f);
                while (!NavMesh.SamplePosition(RandomDirection(mutlipler), out targetHit, TargetPointMaxDistance * mutlipler, ~0))
                {
                    yield return null;
                }
                ShowDebug(targetHit.position, Color.red);

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

        private Vector3 RandomDirectionWithinAngle(Vector3 baseDirection, float maxAngle)
        {
            baseDirection.Normalize();

            Quaternion randomRotation = Quaternion.AngleAxis(Random.Range(0, maxAngle * Mathf.Deg2Rad), Vector3.up) * Quaternion.AngleAxis(Random.Range(0, 360), baseDirection);

            return randomRotation * baseDirection;
        }

        private Vector3 RandomDirection(float multiplier = 1.0f)
        {
            var pos = _agent.transform.position + Random.onUnitSphere * TargetPointMaxDistance * _agent.gameObject.transform.lossyScale.x * multiplier;
            ShowDebug(pos, Color.green);

            return pos;
        }

        private void ShowDebug(Vector3 pos, Color c)
        {
#if UNITY_EDITOR
            Debug.Log($"NavMeshAgentRandomMovement::Position: {pos}");
            Debug.DrawLine(this.transform.position, pos, c, 5f);
            Debug.DrawLine(pos, pos + Vector3.up, c, 5f);
#endif
        }
    }
}
