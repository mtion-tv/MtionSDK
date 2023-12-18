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

        protected NavMeshAgent _agent;

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
            if (_moveCoroutine == null)
            {
                return;
            }

            StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;
        }

        protected abstract IEnumerator MoveCoroutine();
    }
}
