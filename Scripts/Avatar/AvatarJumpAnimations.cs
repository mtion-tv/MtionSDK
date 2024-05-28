using System;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk
{
    [Serializable]
    public struct AvatarJumpAnimations
    {
        [SerializeField] private AnimationClip JumpStart;
        [SerializeField] private AnimationClip JumpLoop;
        [SerializeField] private AnimationClip JumpEnd;

        public AnimationClip[] GetAnimationClips()
        {
            AnimationClip firstNonNullClip = null;
            if (JumpStart != null) firstNonNullClip = JumpStart;
            else if (JumpLoop != null) firstNonNullClip = JumpLoop;
            else if (JumpEnd != null) firstNonNullClip = JumpEnd;

            if (firstNonNullClip == null)
            {
                return null;
            }

            List<AnimationClip> animations = new List<AnimationClip>(3);
            animations.Add(JumpStart != null ? JumpStart : firstNonNullClip);
            animations.Add(JumpLoop != null ? JumpLoop : firstNonNullClip);
            animations.Add(JumpEnd != null ? JumpEnd : firstNonNullClip);

            return animations.ToArray();
        }

        public void AutoPopulateAnimations()
        {
            if (JumpStart != null)
            {
                if (JumpLoop == null) JumpLoop = JumpStart;
                if (JumpEnd == null) JumpEnd = JumpStart;
            }
        }
    }
}
