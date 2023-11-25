using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk.action
{
    [ExecuteInEditMode]
    [AddComponentMenu("mtion/Particle Action Controller")]
    [RequireComponent(typeof(PlayParticlesAction))]
    [RequireComponent(typeof(StopParticlesAction))]
    [RequireComponent(typeof(ParticlesCustomProperties))]
    public class ParticleActionController : MTIONComponent
    {
        [SerializeField]
        private ParticleSystem _particleSystem;

        private PlayParticlesAction _playAction;
        private StopParticlesAction _stopAction;
        private ParticlesCustomProperties _particleProperties;

        private void Awake()
        {
            if (_particleSystem == null)
            {
                _particleSystem = GetComponentInChildren<ParticleSystem>();
                OnValidate();
            }
        }

        private void OnValidate()
        {
            _playAction = GetComponent<PlayParticlesAction>();
            _stopAction = GetComponent<StopParticlesAction>();
            _particleProperties = GetComponent<ParticlesCustomProperties>();

            _playAction.ParticleSystem = _particleSystem;
            _stopAction.ParticleSystem = _particleSystem;
            _particleProperties.ParticleSystem = _particleSystem;
        }
    }
}
