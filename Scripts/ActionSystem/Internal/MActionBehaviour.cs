
using System;
using System.Collections.Generic;
using System.Reflection;
using mtion.room.sdk.compiled;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace mtion.room.sdk.action
{
    [Serializable]
    public sealed class ActionEntryPointInternal
    {
        public Object Target;
        public ActionEntryPointInfo EntryPoint = new ActionEntryPointInfo();
    }

    [Serializable]
    public sealed class ActionExitPointInternal
    {
        public Object Target;
        public ActionExitPointInfo ExitPoint;
    }

    [Serializable]
    public sealed class ActionExitParametersProviderInternal
    {
        public Object Target;
        public List<ActionExitParameterInfo> Parameters;
    }
    
    public sealed class MActionBehaviour : MonoBehaviour, IMActionInterfaceHandler
    {

        public string DefaultChatCommand;


        public string ActionName;
        public string ActionDescription;
        public bool Active = true;

        [HideInInspector]
        public string Guid = SDKUtil.GenerateNewGUID();

        public List<ActionEntryPointInternal> ActionEntryPoints = new List<ActionEntryPointInternal>();
        public List<ActionExitPointInternal> ActionExitPoints = new List<ActionExitPointInternal>();
        [FormerlySerializedAs("ActionExitParameters")] public List<ActionExitParametersProviderInternal> ActionExitParameterProviders = new List<ActionExitParametersProviderInternal>();
        
        private Dictionary<string, int> _actionEntryMap = new Dictionary<string, int>();
        private Dictionary<string, int> _actionExitMap = new Dictionary<string, int>();
        private Dictionary<string, int> _actionExitParameterMap = new Dictionary<string, int>();

        private void Awake()
        {
            BuildEntryMap();
            BuildExitMap();
            BuildExitParametersMap();

            if (string.IsNullOrEmpty(ActionName))
            {
                ActionName = gameObject.name;
            }
        }
        
        
        public void Invoke(ActionEventData actionData)
        {
            if (actionData == null) { return; }
            if (actionData.Components == null) { return; }
            if (actionData.Components.Count != ActionEntryPoints.Count) { return; }
            
            for (int i = 0; i < actionData.Components.Count; i++)
            {
                var guid = actionData.Components[i].Guid;
                var input = actionData.Components[i].Parameters;
                if (_actionEntryMap.ContainsKey(guid))
                {
                    int index = _actionEntryMap[guid];
                    ActionEntryPointInternal entry = ActionEntryPoints[index];
                    List<ActionEntryParameterInfo> paramdef = entry.EntryPoint.ParameterDefinitions;

                    MethodInfo method = entry.Target.GetType().GetMethod(ActionConstants.ACTION_ENTRY_POINT);
                    if (method == null)
                    {
                        Debug.LogError("No valid entry point for Action. Contact Asset developer for details.");
                        return;
                    }

                    List<object> parameters = TypeConversion.GenerateParameters(input, paramdef);

                    parameters.Add(actionData.Metadata);

                    
                    method.Invoke(entry.Target, parameters.ToArray());
                }
            }
        }
        
        public ActionInterfaceDescriptor GetInterfaceDescriptor()
        {
            var desc = new ActionInterfaceDescriptor();
            desc.Guid = Guid;
            desc.ActionName = ActionName;
            desc.ActionDescription = ActionDescription;
            
            
            desc.ValidEntryPoints = new List<ActionEntryPointInfo>();
            foreach (ActionEntryPointInternal entryPoint in ActionEntryPoints)
            {
                
                desc.ValidEntryPoints.Add(new ActionEntryPointInfo
                {
                    Guid = entryPoint.EntryPoint.Guid,
                    ParameterDefinitions = entryPoint.EntryPoint.ParameterDefinitions,
                });
            }

            desc.ValidExitPoints = new List<ActionExitPointInfo>();
            foreach (ActionExitPointInternal exitPoint in ActionExitPoints)
            {
                desc.ValidExitPoints.Add(new ActionExitPointInfo
                {
                    Guid = exitPoint.ExitPoint.Guid,
                    Name = exitPoint.ExitPoint.Name,
                    Description = exitPoint.ExitPoint.Description
                });
            }

            desc.ValidExitParameters = new List<ActionExitParameterInfo>();
            foreach (ActionExitParametersProviderInternal parameterProvider in ActionExitParameterProviders)
            {
                foreach (ActionExitParameterInfo parameterInfo in parameterProvider.Parameters)
                {
                    desc.ValidExitParameters.Add(new ActionExitParameterInfo
                    {
                        Guid = parameterInfo.Guid,
                        Name = parameterInfo.Name,
                        ParameterType = parameterInfo.ParameterType
                    });
                }
            }

            return desc;
        }


        public void BindToActionExit(string exitGuid, Action onExit)
        {
            if (_actionExitMap.ContainsKey(exitGuid))
            {
                (ActionExitPoints[_actionExitMap[exitGuid]].Target as IMActionExitEvent).BindToActionComplete(onExit);
            }
        }
        
        public void UnbindToActionExit(string exitGuid, Action onExit)
        {
            if (_actionExitMap.ContainsKey(exitGuid))
            {
                (ActionExitPoints[_actionExitMap[exitGuid]].Target as IMActionExitEvent).UnbindToActionComplete(onExit);
            }
        }

        public T GetParameterValue<T>(string parameterGuid)
        {
            if (_actionExitParameterMap.ContainsKey(parameterGuid))
            {
                ActionExitParametersProviderInternal internalProvider =
                    ActionExitParameterProviders[_actionExitParameterMap[parameterGuid]];

                foreach (ActionExitParameterInfo parameterInfo in internalProvider.Parameters)
                {
                    if (parameterInfo.Guid == parameterGuid)
                    {
                        return (internalProvider.Target as IMActionExitParameterProvider).GetParameterValue<T>(
                            parameterInfo.Name);
                    }
                }
            }

            return default;
        }
        
        public void BuildEntryMap()
        {
            _actionEntryMap.Clear();
            for (int i = 0; i < ActionEntryPoints.Count; i++)
            {
                _actionEntryMap.Add(ActionEntryPoints[i].EntryPoint.Guid, i);
            }
        }

        public void BuildExitMap()
        {
            _actionExitMap.Clear();
            for (int i = 0; i < ActionExitPoints.Count; i++)
            {
                _actionExitMap.Add(ActionExitPoints[i].ExitPoint.Guid, i);
            }
        }

        public void BuildExitParametersMap()
        {
            _actionExitParameterMap.Clear();
            for (int i = 0; i < ActionExitParameterProviders.Count; i++)
            {
                foreach (ActionExitParameterInfo parameterProvider in ActionExitParameterProviders[i].Parameters)
                {
                    _actionExitParameterMap.Add(parameterProvider.Guid, i);
                }
            }
        }
    }
}
