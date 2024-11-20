
using System;
using System.Collections.Generic;
using System.Reflection;
using mtion.room.sdk.compiled;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

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

#if UNITY_EDITOR
        public string DefaultChatCommand;
#endif

#if UNITY_EDITOR && SDK_INTERNAL_FEATURES


        [ContextMenu("mtion/Populate Entry Points")]
        public void PopulateEntryPoints()
        {

            foreach (var entryPoint in ActionEntryPoints)
            {
                if (entryPoint.Target == null) continue;

                var target = entryPoint.Target as Component;
                if (target == null) continue;

                var targetGo = target.gameObject;
                if (targetGo == null) continue;

                var components = targetGo.GetComponents<Component>();
                foreach (var component in components)
                {
                    if (component == null) continue;

                    var methods = component.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var method in methods)
                    {
                        if (method.Name == ActionConstants.ACTION_ENTRY_POINT)
                        {
                            entryPoint.EntryPoint = new ActionEntryPointInfo
                                {
                                    Guid = SDKUtil.GenerateNewGUID(),
                                    ParameterDefinitions = new List<ActionEntryParameterInfo>()
                                };

                            var parameters = method.GetParameters();
                            foreach (var param in parameters)
                            {
                                var paramInfo = new ActionEntryParameterInfo
                                {
                                    Name = param.Name,
                                    ParameterType = $"{param.ParameterType.FullName}, {param.ParameterType.Assembly.FullName}",
                                };

                                if (param.ParameterType == typeof(bool))
                                {
                                    paramInfo.Metadata = new BoolMetadata();
                                }
                                else if (param.ParameterType == typeof(string))
                                {
                                    paramInfo.Metadata = new StringMetadata();
                                }
                                else if (param.ParameterType == typeof(float) || param.ParameterType == typeof(int))
                                {
                                    paramInfo.Metadata = new NumericMetadata();
                                }

                                entryPoint.EntryPoint.ParameterDefinitions.Add(paramInfo);
                            }
                            break; // Assuming one entry point per component
                        }
                    }
                }
            }
            
            EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif



        public bool Deprecated = false;
        public string ActionName;
        public string ActionDescription;
        public ActionNodeType ActionNodeType = ActionNodeType.ACTION;
        public bool Active = true;

#if !SDK_INTERNAL_FEATURES
        [HideInInspector]
#endif
        public string Guid = SDKUtil.GenerateNewGUID();

        public List<ActionEntryPointInternal> ActionEntryPoints = new List<ActionEntryPointInternal>();
        public List<ActionExitPointInternal> ActionExitPoints = new List<ActionExitPointInternal>();
        public List<ActionExitParametersProviderInternal> ActionExitParameterProviders = new List<ActionExitParametersProviderInternal>();

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

            if (actionData.Components.Count > 0)
            {
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

                        List<object> parameters = new List<object>();
                        if (paramdef != null)
                        {
                            parameters = TypeConversion.GenerateParameters(input, paramdef);
                        }

                        parameters.Add(actionData.Metadata);


                        method.Invoke(entry.Target, parameters.ToArray());
                    }
                }
            }
            else // Node action does not have an entry connection
            {
                foreach (ActionExitPointInternal exitPoint in ActionExitPoints)
                {
                    MethodInfo method = exitPoint.Target.GetType().GetMethod(ActionConstants.SIMULATE_ACTION_COMPLETE);
                    if (method == null)
                    {
                        Debug.LogError("No valid exit point for Action. Contact Asset developer for details.");
                        return;
                    }

                    method.Invoke(exitPoint.Target, new object[0]);
                }
            }
        }

        public ActionInterfaceDescriptor GetInterfaceDescriptor()
        {
            var desc = new ActionInterfaceDescriptor();
            desc.Guid = Guid;
            desc.ActionName = ActionName;
            desc.ActionDescription = ActionDescription;
            desc.NodeType = ActionNodeType;
            desc.Deprecated = Deprecated;


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
                IMActionExitParameterProvider provider = parameterProvider.Target as IMActionExitParameterProvider;
                if (provider == null)
                {
                    continue;
                }

                int paramsCount = provider.Count == 0 ? parameterProvider.Parameters.Count : provider.Count;
                
                for (int i = 0; i < paramsCount; ++i)//(int i = 0; i < provider.Count; ++i)//for (int i = 0; i < parameterProvider.Parameters.Count; ++i)
                {
                    ActionExitParameterInfo parameterInfo = parameterProvider.Parameters[i];
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

        public void ForceInternalUpdate()
        {
            foreach (var exitPoint in ActionExitPoints)
            {
                (exitPoint.Target as IMActionForceInternalUpdate).ForceInternalUpdate();
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

        public Type GetParameterType(string parameterGuid)
        {
            if (_actionExitParameterMap.ContainsKey(parameterGuid))
            {
                ActionExitParametersProviderInternal internalProvider =
                    ActionExitParameterProviders[_actionExitParameterMap[parameterGuid]];

                foreach (ActionExitParameterInfo parameterInfo in internalProvider.Parameters)
                {
                    if (parameterInfo.Guid == parameterGuid)
                    {
                        return (internalProvider.Target as IMActionExitParameterProvider).GetParameterType(
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
                string guid = ActionEntryPoints[i].EntryPoint.Guid;
                if (!_actionEntryMap.ContainsKey(guid))
                {
                    _actionEntryMap.Add(guid, i);
                }
            }
        }

        public void BuildExitMap()
        {
            _actionExitMap.Clear();
            for (int i = 0; i < ActionExitPoints.Count; i++)
            {
                string guid = ActionExitPoints[i].ExitPoint.Guid;
                if (!_actionExitMap.ContainsKey(guid))
                {
                    _actionExitMap.Add(guid, i);
                }
            }
        }

        public void BuildExitParametersMap()
        {
            _actionExitParameterMap.Clear();
            for (int i = 0; i < ActionExitParameterProviders.Count; i++)
            {
                foreach (ActionExitParameterInfo parameterProvider in ActionExitParameterProviders[i].Parameters)
                {
                    string guid = parameterProvider.Guid;
                    if (!_actionExitParameterMap.ContainsKey(guid))
                    {
                        _actionExitParameterMap.Add(guid, i);
                    }
                }
            }
        }
    }
}
