using Unity.VisualScripting;

namespace mtion.room.sdk.visualscripting
{
    [UnitTitle("SDK Entry Point")]
    [UnitCategory(UVSSDKEntryPointConstants.EntryPointUnitCategory)]
    [TypeIcon(typeof(FlowGraph))]
    public sealed class UVSSDKEntryPointUnit : MachineEventUnit<string>
    {
        [Serialize]
        [Inspectable]
        [InspectorLabel("Display Name")]
        public string displayName = UVSSDKEntryPointConstants.DefaultDisplayName;

        [Serialize]
        public string entryPointId = UVSSDKEntryPointConstants.GenerateEntryPointId();

        protected override string hookName => UVSSDKEntryPointConstants.EventHookName;

        protected override bool ShouldTrigger(Flow flow, string triggeredEntryPointId)
        {
            if (string.IsNullOrWhiteSpace(entryPointId))
            {
                return false;
            }

            return string.Equals(triggeredEntryPointId, entryPointId, System.StringComparison.Ordinal);
        }
    }
}
