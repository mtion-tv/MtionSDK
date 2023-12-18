using mtion.room.sdk.action;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace mtion.room
{
    [AddComponentMenu("mtion/Nav Mesh Agent")]
    [RequireComponent(typeof(NavMeshAgentLinkMover))]
    [RequireComponent(typeof(NavMeshAgentAnimator))]
    [RequireComponent(typeof(NavMeshAgent))]
    [DisallowMultipleComponent]
    public sealed class MTIONNavMeshAgent : MTIONComponent
    {
        [Header("Movement")]
        [SerializeField] private float _maxJumpDistance = 5f;

        [Header("Animations")]
        [SerializeField] private AnimationClip _idleAnimation;
        [SerializeField] private AnimationClip _jumpAnimation;
        [SerializeField] private AnimationClip _walkAnimation;

        [SerializeField, HideInInspector]
        private int _version;

        private NavMeshAgentMovement _activeMovement;
        private NavMeshAgentRandomMovement _randomMovement;
        private NavMeshAgentTargetMovement _targetMovement;
        private NavMeshAgentPositionMovement _positionMovement;
        private NavMeshAgentLinkMover _linkMover;
        private NavMeshAgentAnimator _navMeshAnimator;
        private NavMeshAgent _agent;

        private bool _walkAnimationOverride;

        public int Version => _version;

        private void Awake()
        {
            _randomMovement = GetComponent<NavMeshAgentRandomMovement>();
            _targetMovement = GetComponent<NavMeshAgentTargetMovement>();
            _positionMovement = GetComponent<NavMeshAgentPositionMovement>();
            _linkMover = GetComponent<NavMeshAgentLinkMover>();
            _navMeshAnimator = GetComponent<NavMeshAgentAnimator>();
            _agent = GetComponent<NavMeshAgent>();
        }

        private void Start()
        {
            _activeMovement = _randomMovement;

            _navMeshAnimator.SetAnimationClips(_idleAnimation, _jumpAnimation, _walkAnimation);

            _linkMover.SetMaxJumpDistance(_maxJumpDistance);

            _linkMover.OnLinkJumpBegin += () =>
            {
                _navMeshAnimator.PlayJumpAnimation(true);
            };

            _linkMover.OnLinkJumpEnd += () =>
            {
                _navMeshAnimator.PlayJumpAnimation(false);
            };

            _linkMover.OnLinkWalkBegin += () =>
            {
                _navMeshAnimator.PlayWalkAnimation(true);
                _walkAnimationOverride = true;
            };

            _linkMover.OnLinkWalkEnd += () =>
            {
                _navMeshAnimator.PlayWalkAnimation(false);
                _walkAnimationOverride = false;
            };
        }

        private void Update()
        {
            if (_agent.velocity.magnitude > 0f)
            {
                _navMeshAnimator.PlayWalkAnimation(true);
            }
            else if (!_walkAnimationOverride)
            {
                _navMeshAnimator.PlayWalkAnimation(false);
            }
        }

        public void StartMovement()
        {
        }

        public void StopMovement()
        {
            _activeMovement?.StopMovement();
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
    }
}
