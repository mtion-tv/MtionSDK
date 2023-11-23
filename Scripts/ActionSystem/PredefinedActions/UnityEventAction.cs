using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace mtion.room.sdk.action
{
    [AddComponentMenu("mtion/Unity Event Action")]
    public sealed class UnityEventAction : MTIONComponent, IMActionInterfaceImpl
    {
        #region private attributes

        [SerializeField]
        private UnityEvent _unityEvent;

        #endregion

        #region public functions

        public List<string> GetNonUnityEventTargets()
        {
            var output = new List<string>();

            for (var i = 0; i < _unityEvent.GetPersistentEventCount(); ++i)
            {
                var targetType = _unityEvent.GetPersistentTarget(i).GetType();
                if (!targetType.Namespace.Equals("UnityEngine"))
                {
                    output.Add($"{targetType.Name} ({_unityEvent.GetPersistentMethodName(i)})");
                }
            }

            return output;
        }

        #endregion

        #region IMActionInterfaceImpl implementation

        public void ActionEntryPoint(ActionMetadata metadata)
        {
            _unityEvent?.Invoke();
        }

        #endregion
    }
}
