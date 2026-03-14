using UnityEngine;

namespace mtion.room.sdk.visualscripting
{
    public sealed class UVSSDKEntryPointBridgeAction : MonoBehaviour, action.IMActionInterfaceImpl
    {
        [SerializeField]
        private string _entryPointId;

        public string EntryPointId
        {
            get => _entryPointId;
            set => _entryPointId = value;
        }

        public void ActionEntryPoint(action.ActionMetadata metadata)
        {
            if (string.IsNullOrWhiteSpace(_entryPointId))
            {
                Debug.LogWarning("[UVSSDKEntryPointBridgeAction] Missing entry point id.", this);
                return;
            }

#if MTION_INTERNAL_BUILD
            UVSSDKEntryPointRuntimeBridge.Trigger(gameObject, _entryPointId);
#else
            Debug.LogWarning($"[UVSSDKEntryPointBridgeAction] UVS entry point '{_entryPointId}' triggered, but runtime bridge is only available in the application build.", this);
#endif
        }
    }
}
