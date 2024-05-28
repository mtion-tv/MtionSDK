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
            AnimationClip firstNonNullClip = null;
            if (MoveForward != null) firstNonNullClip = MoveForward;
            else if (MoveBackward != null) firstNonNullClip = MoveBackward;
            else if (MoveLeft != null) firstNonNullClip = MoveLeft;
            else if (MoveRight != null) firstNonNullClip = MoveRight;

            if (firstNonNullClip == null)
            {
                return null;
            }

            List<AnimationClip> animations = new List<AnimationClip>(8);
            animations.Add(MoveForward != null ? MoveForward : firstNonNullClip);
            animations.Add(MoveBackward != null ? MoveBackward : firstNonNullClip);
            animations.Add(MoveLeft != null ? MoveLeft : firstNonNullClip);
            animations.Add(MoveRight != null ? MoveRight : firstNonNullClip);
            if (MoveForwardLeft && MoveForwardRight && MoveBackwardLeft && MoveBackwardRight)
            {
                animations.Add(MoveForwardLeft);
                animations.Add(MoveForwardRight);
                animations.Add(MoveBackwardLeft);
                animations.Add(MoveBackwardRight);
            }

            return animations.ToArray();
        }

        public void AutoPopulateAnimations()
        {
            if (MoveForward != null)
            {
                if (MoveBackward == null) MoveBackward = MoveForward;
                if (MoveLeft == null) MoveLeft = MoveForward;
                if (MoveRight == null) MoveRight = MoveForward;
            }
        }
    }
}
