using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace mtion.room
{
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class NavMeshAgentMovement : MonoBehaviour
    {
        private Coroutine _moveCoroutine;
        private Vector3 _destinationOnPause;

        protected NavMeshAgent _agent;
        protected float _hyperactivity;

        public delegate IEnumerator OnPointReachedDelegate();
        public OnPointReachedDelegate OnPointReached;

        public bool Paused { get; private set; }
        
        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        public void StartMovement()
        {
            if (_moveCoroutine != null)
            {
                return;
            }

            _moveCoroutine = StartCoroutine(MoveCoroutine());
        }
        
        public void StopMovement()
        {
            if (_agent.isOnNavMesh)
            {
                _agent.ResetPath();
            }

            if (_moveCoroutine == null)
            {
                return;
            }

            StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;
        }

        public virtual void PauseMovement()
        {
            if (_agent.isOnOffMeshLink)
            {
                NavMeshUtility.ConvertLinkPosToNavMeshPos(_agent.currentOffMeshLinkData.endPos, out Vector3 endPos);
                _destinationOnPause = endPos;
            }
            else
            {
                _destinationOnPause = _agent.destination;
            }

            _agent.SetDestination(_agent.transform.position);
            Paused = true;
        }

        public virtual void ResumeMovement()
        {
            _agent.SetDestination(_destinationOnPause);
            Paused = false;
        }

        public void SetHyperactivity(float hyperactivity)
        {
            _hyperactivity = hyperactivity;
        }

        protected abstract IEnumerator MoveCoroutine();
    }
}
