using mtion.room.sdk.action;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace mtion.room
{
    [AddComponentMenu("mtion/Nav Mesh Agent")]
    [RequireComponent(typeof(NavMeshAgentLinkMover))]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(NavMeshAgentRandomMovement))]
    [RequireComponent(typeof(NavMeshAgentTargetMovement))]
    [RequireComponent(typeof(NavMeshAgentPositionMovement))]
    [DisallowMultipleComponent]
    public sealed class MTIONNavMeshAgent : MTIONComponent
    {
        #region private attributes
        
        [SerializeField, HideInInspector]
        private int _version;

        private NavMeshAgentMovement _activeMovement;
        private NavMeshAgentRandomMovement _randomMovement;
        private NavMeshAgentTargetMovement _targetMovement;
        private NavMeshAgentPositionMovement _positionMovement;
        private NavMeshAgentLinkMover _linkMover;
        
        #endregion

        #region public properties
        
        public int Version => _version;
        public NavMeshAgent NavMeshAgent { get; private set; }

        public float MovingSpeed
        {
            get
            {
                return NavMeshAgent.speed;
            }
            set
            {
                NavMeshAgent.speed = value;
                NavMeshAgent.acceleration = 2 * value;
            }
        }
        
        #endregion

        #region Unity Events

        private void Awake()
        {
            _randomMovement = GetComponent<NavMeshAgentRandomMovement>();
            _targetMovement = GetComponent<NavMeshAgentTargetMovement>();
            _positionMovement = GetComponent<NavMeshAgentPositionMovement>();
            _linkMover = GetComponent<NavMeshAgentLinkMover>();
            NavMeshAgent = GetComponent<NavMeshAgent>();
        }

        private void OnDisable()
        {
            NavMeshAgent.enabled = false;
        }

        private void OnEnable()
        {
            NavMeshAgent.enabled = true;
        }

        #endregion

        #region Public Methods

        public void StartMovement()
        {
            _activeMovement?.StartMovement();
        }

        public void StopMovement()
        {
            _activeMovement?.StopMovement();
        }

        public void PauseMovement()
        {
            _activeMovement?.PauseMovement();
        }

        public void ResumeMovement()
        {
            _activeMovement?.ResumeMovement();
        }

        public void SetRandomMovement()
        {
            if (_randomMovement == null)
            {
                return;
            }

            StopMovement();
            _activeMovement = _randomMovement;
        }

        public void SetTargetMovement(Transform target)
        {
            if (_targetMovement == null)
            {
                return;
            }

            StopMovement();
            _activeMovement = _targetMovement;
            _targetMovement.SetTarget(target);
        }

        public void SetPositionMovement(Vector3 position)
        {
            if (_positionMovement == null)
            {
                return;
            }

            StopMovement();
            _activeMovement = _positionMovement;
            _positionMovement.SetPosition(position);
        }

        public void SetJumpHeight(float height)
        {
            _linkMover.SetMaxJumpDistance(height);
        }

        public void SetHyperactivity(float hyperactivity)
        {
            _randomMovement.SetHyperactivity(hyperactivity);
        }

        public void SetOnPointReachedAction(NavMeshAgentMovement.OnPointReachedDelegate action)
        {
            _randomMovement.OnPointReached = action;
        }

        #endregion
    }
}
