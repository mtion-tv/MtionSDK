using System;
using System.Collections.Generic;

namespace mtion.room.sdk.action
{
    [Serializable]
    public class ActionEntryPointData
    {
        public string Guid;
        public List<object> Parameters = new List<object>();
    }

    [Serializable]
    public class ActionEventData
    {
        public List<ActionEntryPointData> Components = new List<ActionEntryPointData>();
        public ActionMetadata Metadata = null;
        public Action<bool> OnComplete = null;
    }

    [Serializable]
    public sealed class ActionEntryPointInfo
    {
        public string Guid;
        public List<ActionEntryParameterInfo> ParameterDefinitions = new List<ActionEntryParameterInfo>();
    }

    [Serializable]
    public sealed class ActionExitPointInfo
    {
        public string Guid;
        public string Name;
        public string Description;
    }

    [Serializable]
    public sealed class ActionExitParameterInfo
    {
        public string Guid;
        public string Name;
        public string ParameterType;
    }

    [Serializable]
    public enum ActionNodeType
    {
        ACTION,
        SETTER,
        GETTER,
        FLOW,
        CONSTANT,
        NOTE,
        GROUP,
    }
    
    [Serializable]
    public sealed class ActionInterfaceDescriptor
    {
        public bool Deprecated;
        public bool ByPassFinishFilter;
        
        public string Guid;
        public string ActionName;
        public string ActionDescription;
        public ActionNodeType NodeType;
        public List<ActionEntryPointInfo> ValidEntryPoints = new List<ActionEntryPointInfo>();
        public List<ActionExitPointInfo> ValidExitPoints = new List<ActionExitPointInfo>();
        public List<ActionExitParameterInfo> ValidExitParameters = new List<ActionExitParameterInfo>();
    }

    public interface IMActionInterfaceHandler
    {
        public void Invoke(ActionEventData actionData);
        public ActionInterfaceDescriptor GetInterfaceDescriptor(bool ignoreCache);
    }
}
