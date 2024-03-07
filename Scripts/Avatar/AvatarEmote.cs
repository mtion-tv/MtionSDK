using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk
{
    [Serializable]
    public class AvatarEmote
    {
        [SerializeField] private string _name;
        [SerializeField] private AnimationClip _animation;

        public string Name => _name;
        public AnimationClip Animation => _animation;

        public AvatarEmote(string name, AnimationClip animation)
        {
            _name = name;
            _animation = animation;
        }
    }
}
