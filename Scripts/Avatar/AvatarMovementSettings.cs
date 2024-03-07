using System;
using mtion.room.sdk.action;
using UnityEngine;

namespace mtion.room.sdk
{
    public sealed class AvatarMovementSettings : MTIONComponent
    {
        #region private attributes
        
        [SerializeField]
        [Min(0)]
        private float _walkingSpeed = 2f;
        [SerializeField]
        [Min(0)]
        private float _runningSpeed = 4f;
        [SerializeField]
        [Min(0)]
        private float _jumpHeight = 2f;

        #endregion
        
        #region public properties

        public float WalkingSpeed => _walkingSpeed;
        public float RunningSpeed => _runningSpeed;
        public float JumpHeight => _jumpHeight;

        #endregion
    }
}
