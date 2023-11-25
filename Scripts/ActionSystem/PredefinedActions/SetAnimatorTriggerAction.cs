using UnityEngine;

namespace mtion.room.sdk.action
{
    [AddComponentMenu("mtion/Set Animator Trigger Action")]
    public sealed class SetAnimatorTriggerAction : MTIONComponent, IMActionInterfaceImpl
    {
        #region private attributes

        [SerializeField]
        private string _triggerName;

        [SerializeField]
        private Animator _animator;

        #endregion

        #region IMActionInterfaceImpl implementation

        public void ActionEntryPoint(ActionMetadata metadata)
        {
            _animator.SetTrigger(_triggerName);
        }

        #endregion
    }
}
