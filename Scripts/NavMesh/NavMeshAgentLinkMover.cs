using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace mtion.room
{
    [RequireComponent(typeof(NavMeshAgent))]
    [DisallowMultipleComponent]
    public sealed class NavMeshAgentLinkMover : MonoBehaviour
    {
        private const float JUMP_Y_THRESHOLD = 0.1f;
        private const float JUMP_DISTANCE_THRESHOLD = 2f;

        private NavMeshAgent _agent;
        private float _maxJumpDistance;

        public event Action OnLinkJumpBegin;
        public event Action OnLinkJumpEnd;

        public event Action OnLinkWalkBegin;
        public event Action OnLinkWalkEnd;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.autoTraverseOffMeshLink = false;
        }

        private IEnumerator Start()
        {
            while (true)
            {
                yield return CrossOffMeshLink();
                yield return null;
            }
        }

        public void SetMaxJumpDistance(float distance)
        {
            _maxJumpDistance = distance;
        }

        private IEnumerator CrossOffMeshLink()
        {
            if (_agent.isOnNavMesh &&
                _agent.isOnOffMeshLink &&
                _agent.currentOffMeshLinkData.valid)
            {
                var validStart = NavMeshUtility.ConvertLinkPosToNavMeshPos(_agent.currentOffMeshLinkData.startPos, out var startPos);
                var validEnd = NavMeshUtility.ConvertLinkPosToNavMeshPos(_agent.currentOffMeshLinkData.endPos, out var endPos);
                var distance = Vector3.Distance(startPos, endPos);

                if (distance > _maxJumpDistance)
                {
                    yield break;
                }

                yield return StartCoroutine(Walk(_agent.transform.position, startPos));

                var yDistance = Math.Abs(endPos.y - startPos.y);
                if (yDistance > JUMP_Y_THRESHOLD || 
                    distance > JUMP_DISTANCE_THRESHOLD)
                {
                    OnLinkJumpBegin?.Invoke();
                    yield return StartCoroutine(Jump(startPos, endPos));
                    OnLinkJumpEnd?.Invoke();
                }
                else
                {
                    OnLinkWalkBegin?.Invoke();
                    yield return StartCoroutine(Walk(startPos, endPos));
                    OnLinkWalkEnd?.Invoke();
                }

                _agent.CompleteOffMeshLink();
            }
        }

        private IEnumerator Walk(Vector3 startPos, Vector3 endPos)
        {
            var distance = Vector3.Distance(startPos, endPos);
            var duration = distance / _agent.speed;
            var normalizedTime = 0f;
            while (normalizedTime < 1f)
            {
                _agent.transform.position = Vector3.Lerp(startPos, endPos, normalizedTime);
                normalizedTime += Time.deltaTime / duration;
                yield return null;
            }
        }

        private IEnumerator Jump(Vector3 startPos, Vector3 endPos)
        {
            var duration = 1f;
            yield return new WaitForSeconds(0.25f);

            var lowerPoint = endPos.y < startPos.y
                ? endPos
                : startPos; ;
            var higherPoint = endPos.y > startPos.y
                ? endPos
                : startPos;

            var midPos = new Vector3(lowerPoint.x, higherPoint.y + _agent.height * 2f, lowerPoint.z);

            var normalizedTime = 0f;
            while (normalizedTime < 1f)
            {
                Vector3 m1 = Vector3.Lerp(startPos, midPos, normalizedTime);
                Vector3 m2 = Vector3.Lerp(midPos, endPos, normalizedTime);
                _agent.transform.position = Vector3.Lerp(m1, m2, normalizedTime);
                normalizedTime += Time.deltaTime / duration;
                yield return null;
            }
        }
    }
}
