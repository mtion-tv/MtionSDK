using System;
using System.Collections;
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
        private bool _autoCrossLink;

        public event Action<float, float> OnLinkJumpBegin;
        public event Action OnLinkJumpEnd;

        public event Action OnLinkWalkBegin;
        public event Action OnLinkWalkEnd;
        
        public bool Running { get; private set; }
        public Vector3 Velocity { get; private set; }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.autoTraverseOffMeshLink = false;
        }

        private IEnumerator Start()
        {
            while (true)
            {
                if (_autoCrossLink)
                {
                    yield return CrossOffMeshLinkCoroutine();
                }
                yield return null;
            }
        }

        public void SetMaxJumpDistance(float distance)
        {
            _maxJumpDistance = distance;
        }

        public void SetAutoCrossOffMeshLink(bool autoCross)
        {
            _autoCrossLink = autoCross;
        }

        public bool CanCrossOffMeshLink()
        {
            if (_agent.isOnNavMesh &&
                _agent.isOnOffMeshLink &&
                _agent.currentOffMeshLinkData.valid)
            {
                NavMeshUtility.ConvertLinkPosToNavMeshPos(_agent.currentOffMeshLinkData.startPos, out Vector3 startPos);
                NavMeshUtility.ConvertLinkPosToNavMeshPos(_agent.currentOffMeshLinkData.endPos, out Vector3 endPos);
                float distance = Vector3.Distance(startPos, endPos);

                if (distance <= _maxJumpDistance)
                {
                    return true;
                }
            }

            return false;
        }

        public void CrossOffMeshLink()
        {
            StartCoroutine(CrossOffMeshLinkCoroutine());
        }

        private IEnumerator CrossOffMeshLinkCoroutine()
        {
            if (!CanCrossOffMeshLink())
            {
                yield break;
            }

            NavMeshUtility.ConvertLinkPosToNavMeshPos(_agent.currentOffMeshLinkData.startPos, out Vector3 startPos);
            NavMeshUtility.ConvertLinkPosToNavMeshPos(_agent.currentOffMeshLinkData.endPos, out Vector3 endPos);
            float distance = Vector3.Distance(startPos, endPos);

            if (distance > _maxJumpDistance)
            {
                yield break;
            }

            yield return StartCoroutine(Walk(_agent.transform.position, startPos));

            var yDistance = Math.Abs(endPos.y - startPos.y);
            if (yDistance > JUMP_Y_THRESHOLD || distance > JUMP_DISTANCE_THRESHOLD)
            {
                float jumpDistance = (endPos - startPos).magnitude;
                float jumpDuration = jumpDistance / (_agent.speed * 1.5f);
                OnLinkJumpBegin?.Invoke(jumpDistance, jumpDuration);
                yield return StartCoroutine(Jump(startPos, endPos, jumpDuration));
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

        private IEnumerator Walk(Vector3 startPos, Vector3 endPos)
        {
            Vector3 direction = endPos - startPos;
            Running = true;
            Velocity = direction.normalized * _agent.speed;
            var distance = direction.magnitude;
            var duration = distance / _agent.speed;
            var normalizedTime = 0f;
            while (normalizedTime < 1f)
            {
                _agent.transform.position = Vector3.Lerp(startPos, endPos, normalizedTime);
                normalizedTime += Time.deltaTime / duration;
                RotateTowards(direction);
                yield return null;
            }
            Running = false;
        }

        private IEnumerator Jump(Vector3 startPos, Vector3 endPos, float duration)
        {
            Vector3 direction = endPos - startPos;
            Running = true;
            Velocity = direction / duration;
            
            float normalizedTime = 0f;
            while (normalizedTime < 1f)
            {
                Vector3 position = Vector3.Lerp(startPos, endPos, normalizedTime);
                position.y += Mathf.Sin(Mathf.PI * normalizedTime) * _agent.height * .25f;
                _agent.transform.position = position;
                normalizedTime += Time.deltaTime / duration;
                RotateTowards(direction);
                yield return null;
            }

            _agent.transform.position = endPos;
            Running = false;
        }

        private void RotateTowards(Vector3 direction)
        {
            direction.y = 0;
            float desiredAngle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);
            float sign = Mathf.Sign(desiredAngle);

            float angle = sign * Mathf.Min(MathF.Abs(desiredAngle), _agent.angularSpeed) * Time.deltaTime;
            
            transform.Rotate(Vector3.up, angle);
        }
    }
}
