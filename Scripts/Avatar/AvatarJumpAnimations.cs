using System;
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
            if (JumpStart && JumpLoop && JumpEnd)
            {
                return new[] { JumpStart, JumpLoop, JumpEnd };
            }

            return null;
        }
    }
}
