using mtion.room.sdk.action;
using UnityEngine;

namespace mtion.room.sdk.action
{
    public sealed class ParticlesCustomProperties : MTIONComponent
    {
        #region public variables

        [HideInInspector]
        public ParticleSystem ParticleSystem;

        #endregion

        #region private attributes

        private ParticleSystem[] _particleSystems;

        #endregion

        #region public properties

        [CustomProperty]
        public bool Loop
        {
            get
            {
                return ParticleSystem.main.loop;
            }
            set
            {
                foreach (ParticleSystem particleSystem in _particleSystems)
                {
                    ParticleSystem.MainModule main = particleSystem.main;
                    main.loop = value;
                }
            }
        }

        [CustomProperty]
        public bool Play
        {
            get
            {
                return ParticleSystem.isPlaying;
            }
            set
            {
                if (value)
                {
                    ParticleSystem.Play(true);
                }
                else
                {
                    ParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        #endregion

        #region MonoBehaviour implementation

        private void OnValidate()
        {
            if (ParticleSystem == null)
            {
                ParticleSystem = GetComponentInChildren<ParticleSystem>();
            }
        }

        private void Awake()
        {
            _particleSystems = ParticleSystem.transform.GetComponentsInChildren<ParticleSystem>();
        }

        #endregion
    }
}
