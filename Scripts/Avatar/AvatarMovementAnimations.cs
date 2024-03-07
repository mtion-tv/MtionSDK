using System;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk
{
    [Serializable]
    public struct AvatarMovementAnimations
    {
        [Header("Main movements")]
        [SerializeField] private AnimationClip MoveForward;
        [SerializeField] private AnimationClip MoveBackward;
        [SerializeField] private AnimationClip MoveLeft;
        [SerializeField] private AnimationClip MoveRight;
        [Header("Optional movements")]
        [SerializeField] private AnimationClip MoveForwardLeft;
        [SerializeField] private AnimationClip MoveForwardRight;
        [SerializeField] private AnimationClip MoveBackwardLeft;
        [SerializeField] private AnimationClip MoveBackwardRight;

        public AnimationClip[] GetAnimationClips()
        {
            if (MoveForward == null || MoveBackward == null 
                                    || MoveLeft == null || MoveRight == null)
            {
                return null;
            }
            List<AnimationClip> animations = new List<AnimationClip>(8);
            animations.Add(MoveForward);
            animations.Add(MoveBackward);
            animations.Add(MoveLeft);
            animations.Add(MoveRight);
            if (MoveForwardLeft && MoveForwardRight && MoveBackwardLeft && MoveBackwardRight)
            {
                animations.Add(MoveForwardLeft);
                animations.Add(MoveForwardRight);
                animations.Add(MoveBackwardLeft);
                animations.Add(MoveBackwardRight);
            }

            return animations.ToArray();
        }
    }
}
