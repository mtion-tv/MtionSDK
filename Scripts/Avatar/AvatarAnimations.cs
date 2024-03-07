using System.Collections.Generic;
using mtion.room.sdk.action;
using UnityEngine;
using UnityEngine.Serialization;

namespace mtion.room.sdk
{
    public sealed class AvatarAnimations : MTIONComponent
    {
        [SerializeField] private List<AnimationClip> IdleAnimations;

        [Header("Walk Animation Overrides")]
        [SerializeField] private AvatarMovementAnimations WalkAnimations;

        [Space(5)]
        [Header("Run Animation Overrides")]
        [SerializeField] private AvatarMovementAnimations RunAnimations;

        [FormerlySerializedAs("JumpAnimation")]
        [Space(5)]
        [Header("Jump Animation Override")]
        [SerializeField] private AvatarJumpAnimations IdleJumpAnimations;
        [SerializeField] private AvatarJumpAnimations WalkJumpAnimations;
        [SerializeField] private AvatarJumpAnimations RunJumpAnimations;


        [Header("Custom Emotes")]
        [SerializeField] public List<AvatarEmote> Emotes = new List<AvatarEmote>();

        public void AddEmote(AvatarEmote emote)
        {
            Emotes.Add(emote);
        }

        public AnimationClip[] GetIdleAnimations()
        {
            if (IdleAnimations.Count > 0)
            {
                return IdleAnimations.ToArray();
            }

            return null;
        }

        public AnimationClip[] GetWalkAnimations()
        {
            return WalkAnimations.GetAnimationClips();
        }

        public AnimationClip[] GetRunAnimations()
        {
            return RunAnimations.GetAnimationClips();
        }

        public AnimationClip[] GetJumpIdleAnimations()
        {
            return IdleJumpAnimations.GetAnimationClips();
        }

        public AnimationClip[] GetJumpWalkAnimations()
        {
            return WalkJumpAnimations.GetAnimationClips();
        }

        public AnimationClip[] GetJumpRunAnimations()
        {
            return RunJumpAnimations.GetAnimationClips();
        }
    }
}
