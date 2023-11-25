using UnityEngine;

namespace mtion.room.sdk.action
{
    public sealed class PlayParticlesAction : MTIONComponent, IMActionInterfaceImpl
    {
        #region public variables

        [HideInInspector]
        public ParticleSystem ParticleSystem;

        #endregion

        #region MonoBehaviour implementation

        private void OnValidate()
        {
            if (ParticleSystem == null)
            {
                ParticleSystem = GetComponentInChildren<ParticleSystem>();
            }
        }

        #endregion

        #region IMActionInterfaceImpl implementation

        public void ActionEntryPoint(ActionMetadata metadata)
        {
            ParticleSystem.Play(true);
        }

        #endregion
    }
}
