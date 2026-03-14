using UnityEngine;
using Unity.VisualScripting;

namespace mtion.room.sdk.visualscripting
{
    public static class UVSSDKEntryPointRuntimeBridge
    {
        public static void Trigger(GameObject target, string entryPointId)
        {
            if (target == null || string.IsNullOrWhiteSpace(entryPointId))
            {
                return;
            }

            ScriptMachine[] machines = target.GetComponents<ScriptMachine>();
            if (machines == null || machines.Length == 0)
            {
                return;
            }

            for (int i = 0; i < machines.Length; i++)
            {
                ScriptMachine machine = machines[i];
                if (machine == null)
                {
                    continue;
                }

                EventBus.Trigger(new EventHook(UVSSDKEntryPointConstants.EventHookName, machine), entryPointId);
            }
        }
    }
}
