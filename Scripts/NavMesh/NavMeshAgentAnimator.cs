using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room
{
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public sealed class NavMeshAgentAnimator : MonoBehaviour
    {
        private const string AnimatorOverrideControllerPath = "NavMesh/MTIONNavMeshAgentAnimOverride";
        private const string EmptyIdleAnimName = "EmptyIdleAnim";
        private const string EmptyWalkAnimName = "EmptyWalkAnim";
        private const string EmptyJumpAnimName = "EmptyJumpAnim";
        private const string JumpTriggerParam = "Jumping";
        private const string WalkTriggerParam = "Walking";

        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                _animator = gameObject.AddComponent<Animator>();
            }
        }

        public void SetAnimationClips(AnimationClip idleAnimation, AnimationClip jumpAnimation, AnimationClip walkAnimation)
        {
            if (_animator != null)
            {
                var animOverride = new AnimatorOverrideController(
                    Resources.Load<AnimatorOverrideController>(AnimatorOverrideControllerPath));
                animOverride[EmptyIdleAnimName] = idleAnimation;
                animOverride[EmptyWalkAnimName] = walkAnimation;
                animOverride[EmptyJumpAnimName] = jumpAnimation;
                _animator.runtimeAnimatorController = animOverride;
            }
        }

        public void PlayJumpAnimation(bool play)
        {
            if (_animator != null)
            {
                _animator.SetBool(JumpTriggerParam, play);
            }
        }

        public void PlayWalkAnimation(bool play)
        {
            if (_animator != null)
            {
                _animator.SetBool(WalkTriggerParam, play);
            }
        }

        public void PlayIdleAnimation()
        {
            if (_animator != null)
            {
                _animator.SetBool(JumpTriggerParam, false);
                _animator.SetBool(WalkTriggerParam, false);
            }
        }
    }
}
