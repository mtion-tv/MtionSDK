using System.Collections.Generic;
using System.Reflection;
using mtion.room.sdk;
using mtion.room.sdk.action;
using mtion.room.sdk.compiled;
using mtion.room.sdk.visualscripting;
using UnityEditor;
using UnityEngine;

public static class MTIONSDKToolsActionTab
{
    private sealed class SDKEntryPointRow
    {
        public string DisplayName;
        public string EntryPointId;
        public string TargetLabel;
        public GameObject TargetObject;
    }

    private static MActionBehaviourGroup _actionBehaviourContainer = null;
    private static readonly List<SDKEntryPointRow> _sdkEntryPointRows = new List<SDKEntryPointRow>();
    private static Vector2 _scrollPos;
    private static string _sdkEntryPointMessage;
    private static bool _sdkEntryPointMessageIsWarning;

    public static void Refresh()
    {
        VerifySceneState();
    }

    private static void VerifySceneState()
    {
        RefreshSdkEntryPointState();

        GameObject descriptor = null;
        
        if (_actionBehaviourContainer == null)
        {
            _actionBehaviourContainer = GameObject.FindAnyObjectByType<MActionBehaviourGroup>();
            if (_actionBehaviourContainer == null)
            {
                descriptor = BuildManager.GetSceneDescriptor();
                if (descriptor == null)
                {
                    return;
                }
        
                MTIONSDKDescriptorSceneBase rootObject = descriptor.GetComponent<MTIONSDKDescriptorSceneBase>();
                if (rootObject == null)
                {
                    return;
                }
                
                GameObject actionGo = new GameObject("ActionBehaviourContainer");
                actionGo.transform.parent = rootObject.ObjectReferenceProp.transform;
                _actionBehaviourContainer = actionGo.AddComponent<MActionBehaviourGroup>();
                _actionBehaviourContainer.Version = 2;
            }
        }

        if (_actionBehaviourContainer.Version < 2)
        {
            return;
        }
        
        descriptor = BuildManager.GetSceneDescriptor();
        if (descriptor == null)
        {
            return;
        }

        MTIONSDKAssetBase descriptorComponent = descriptor.GetComponent<MTIONSDKAssetBase>();
        if (descriptorComponent == null || descriptorComponent.ObjectReferenceProp == null)
        {
            return;
        }

        List<IAction> actionComponents =
            new List<IAction>(descriptorComponent.ObjectReferenceProp.GetComponentsInChildren<IAction>());
        List<MActionBehaviour> actions = _actionBehaviourContainer.GetActions();
        for (int i = actions.Count - 1; i >= 0; i--)
        {
            if (actions[i].ActionEntryPoints.Count > 0)
            {
                IAction action = actions[i].ActionEntryPoints[0].Target as IAction;
                if (actionComponents.Contains(action))
                {
                    actionComponents.Remove(action);
                }
                else
                {
                    GameObject.DestroyImmediate(actions[i]);
                    actions.RemoveAt(i);
                }
            }
            else if (actions[i].ActionExitPoints.Count > 0)
            {
                IAction action = actions[i].ActionExitPoints[0].Target as IAction;
                if (actionComponents.Contains(action))
                {
                    actionComponents.Remove(action);
                }
                else
                {
                    GameObject.DestroyImmediate(actions[i]);
                    actions.RemoveAt(i);
                }
            }
            else if (actions[i].ActionExitParameterProviders.Count > 0)
            {
                IAction action = actions[i].ActionExitParameterProviders[0].Target as IAction;
                if (actionComponents.Contains(action))
                {
                    actionComponents.Remove(action);
                }
                else
                {
                    GameObject.DestroyImmediate(actions[i]);
                    actions.RemoveAt(i);
                }
            }
            else
            {
                GameObject.DestroyImmediate(actions[i]);
                actions.RemoveAt(i);
            }
        }
        
        for (int i = 0; i < actionComponents.Count; i++)
        {
            AddPredefinedAction(actionComponents[i], _actionBehaviourContainer, actionComponents[i].GetType().Name, "");
        }
    }

    private static void AddPredefinedAction(IAction action, MActionBehaviourGroup actionGroup, string actionName,
        string actionDescription)
    {
        MActionBehaviour actionBehaviour = actionGroup.CreateAction();
        actionBehaviour.ActionName = actionName;
        actionBehaviour.ActionDescription = actionDescription;
        if (action is IMActionInterfaceImpl)
        {
            actionBehaviour.ActionEntryPoints.Add(
                new ActionEntryPointInternal()
                {
                    Target = action as MonoBehaviour,
                    EntryPoint = new ActionEntryPointInfo()
                    {
                        Guid = SDKUtil.GenerateNewGUID()
                    }
                });
        }

        if (action is IMActionExitEvent)
        {
            actionBehaviour.ActionExitPoints.Add(
                new ActionExitPointInternal()
                {
                    Target = action as MonoBehaviour,
                    ExitPoint = new ActionExitPointInfo()
                    {
                        Guid = SDKUtil.GenerateNewGUID()
                    }
                });
        }

        if (action is IMActionExitParameterProvider outputParametersProvider)
        {
            List<ActionExitParameterInfo> parametersInfo =
                new List<ActionExitParameterInfo>(outputParametersProvider.TotalExitParameterConnectors);
            for(int i = 0; i <= 0; i++)
            {
                parametersInfo.Add(new ActionExitParameterInfo()
                {
                    Guid = SDKUtil.GenerateNewGUID(),
                    Name = outputParametersProvider.GetParameterName(i),
                    ParameterType = 
                        $"{outputParametersProvider.GetParameterType(i).FullName}, " +
                        $"{outputParametersProvider.GetParameterType(i).Assembly.FullName}"
                });
            }
            
            actionBehaviour.ActionExitParameterProviders.Add(
                new ActionExitParametersProviderInternal()
                {
                    Target = action as MonoBehaviour,
                    Parameters = parametersInfo,
                });
        }

        actionBehaviour.BuildEntryMap();
        
        
        if (action is IMActionInterfaceImpl)
        {
            AddActionParameters(action, actionBehaviour);
        }
    }
    
    private static void AddActionParameters(IAction action, MActionBehaviour actionBehaviour)
    {
        MethodInfo method = action.GetType().GetMethod("ActionEntryPoint");
        if (method == null)
        {
            EditorGUILayout.LabelField("Object does not contain valid interface.");
        }
        else
        {
            ActionEntryPointInternal entryPoint = actionBehaviour.ActionEntryPoints[0];
            
            ParameterInfo[] parameterInfo = method.GetParameters();
            
            for (int i = 0; i < parameterInfo.Length; i++)
            {
                if (parameterInfo[i].ParameterType == typeof(ActionMetadata))
                {
                    continue;
                }

                if (entryPoint.EntryPoint.ParameterDefinitions.Count <= i)
                {
                    entryPoint.EntryPoint.ParameterDefinitions.Add(
                        new ActionEntryParameterInfo());
                }

                ActionEntryParameterInfo actionparams =
                    entryPoint.EntryPoint.ParameterDefinitions[i];

                actionparams.Name = parameterInfo[i].Name;
                
                actionparams.ParameterType =
                    $"{parameterInfo[i].ParameterType.FullName}, {parameterInfo[i].ParameterType.Assembly.FullName}";
                
                if (TypeConversion.NumericConverter.IsNumericType(
                        parameterInfo[i].ParameterType))
                {
                    if (actionparams.Metadata == null ||
                        actionparams.Metadata.GetType() != typeof(NumericMetadata))
                    {
                        actionparams.Metadata = new NumericMetadata();
                    }
                }
                else if (parameterInfo[i].ParameterType == typeof(string))
                {
                    if (actionparams.Metadata == null ||
                        actionparams.Metadata.GetType() != typeof(StringMetadata))
                    {
                        actionparams.Metadata = new StringMetadata();
                    }
                }
                else if (parameterInfo[i].ParameterType == typeof(bool))
                {
                    if (actionparams.Metadata == null ||
                        actionparams.Metadata.GetType() != typeof(BoolMetadata))
                    {
                        actionparams.Metadata = new BoolMetadata();
                    }
                }
                else if (TypeConversion.ContainerConverter.IsContainer(parameterInfo[i]
                             .ParameterType))
                {
                    if (actionparams.Metadata == null ||
                        actionparams.Metadata.GetType() != typeof(ContainerMetadata))
                    {
                        actionparams.Metadata = new ContainerMetadata();
                    }
                }
                else if (parameterInfo[i].ParameterType == typeof(Object) ||
                         parameterInfo[i].ParameterType == typeof(GameObject))
                {
                    if (actionparams.Metadata == null ||
                        actionparams.Metadata.GetType() != typeof(ObjectMetadata))
                    {
                        actionparams.Metadata = new ObjectMetadata();
                    }
                }
                else
                {
                    if (actionparams.Metadata == null ||
                        actionparams.Metadata.GetType() != typeof(TypeMetadata))
                    {
                        actionparams.Metadata = new TypeMetadata();
                    }
                }
            }
        }
    }

    public static void Draw()
    {
        using (var scrollView = new EditorGUILayout.ScrollViewScope(_scrollPos))
        {
            _scrollPos = scrollView.scrollPosition;
            GenerateActionList();
        }
    }

    private static void RefreshSdkEntryPointsFromUvs()
    {
        try
        {
            GameObject descriptor = BuildManager.GetSceneDescriptor();
            if (descriptor == null)
            {
                throw new System.InvalidOperationException("Initialize the scene under the Build tab before refreshing SDK entry points.");
            }

            MTIONSDKDescriptorSceneBase descriptorComponent = descriptor.GetComponent<MTIONSDKDescriptorSceneBase>();
            if (descriptorComponent == null || descriptorComponent.ObjectReferenceProp == null)
            {
                throw new System.InvalidOperationException("The current SDK descriptor is missing its target object reference.");
            }

            GameObject sdkRoot = descriptorComponent.ObjectReferenceProp;
            if (!VisualScriptingReflectionUtility.SyncEntryPointRegistryFromVisualScripting(sdkRoot, out _, out List<string> errors))
            {
                throw new System.InvalidOperationException(string.Join("\n", errors));
            }

            Refresh();
        }
        catch (System.Exception ex)
        {
            Refresh();
            EditorUtility.DisplayDialog("SDK Entry Point Refresh Failed", ex.Message, "Close");
        }
    }

    public static void GenerateActionList()
    {
#if MTION_ADVANCED_ACTION_UI
        bool guiEnabled;
#endif

        DrawSdkEntryPointsSection();
        
        if (!BuildManager.IsSceneValid() || _actionBehaviourContainer == null)
        {
            StartBox();
            {
                MTIONSDKToolsWindow.DrawWarning("Disabled. Initialize the scene under the \"Build\" tab to use the actions tool.");
            }
            EndBox();
        }
        else
        {
            StartBox();
            {
                List<MActionBehaviour> actions = _actionBehaviourContainer.GetActions();
                if (actions.Count == 0)
                {
                    EditorGUILayout.LabelField("Add Action Components to the asset to see them listed here",
                        MTIONSDKToolsWindow.ListHeaderStyle);
                }
                foreach (var actionBh in _actionBehaviourContainer.GetActions())
                {
                    string dftNameCache = actionBh.ActionName;

                    if (string.IsNullOrEmpty(actionBh.ActionName))
                    {
                        actionBh.ActionName = actionBh.name;
                    }

                    StartBox();
                    {
                        GUIStyle style = new GUIStyle();
                        style.padding = new RectOffset(0, 0, 0, 0);

                        EditorGUILayout.BeginHorizontal(style);
                        
                        EditorGUILayout.LabelField("Active", MTIONSDKToolsWindow.ListHeaderStyle,
                            GUILayout.Width(50));
                        
                        EditorGUILayout.LabelField("Name", MTIONSDKToolsWindow.ListHeaderStyle,
                            GUILayout.Width(100));
                        
                        EditorGUILayout.LabelField("Description", MTIONSDKToolsWindow.ListHeaderStyle,
                            GUILayout.Width(300));
#if MTION_ADVANCED_ACTION_UI
                        EditorGUILayout.LabelField("Chat Command (Testing Only)", MTIONSDKToolsWindow.ListHeaderStyle,
                            new GUILayoutOption[] { GUILayout.Width(100) });
#endif
                        
                        EditorGUILayout.EndHorizontal();
                        
                        EditorGUILayout.BeginHorizontal(style);
                        
                        actionBh.Active = EditorGUILayout.Toggle(actionBh.Active,
                            new GUILayoutOption[] { GUILayout.Width(50) });

                        bool actionGuiEnabled = GUI.enabled;
                        GUI.enabled = actionBh.Active;
                        
                        actionBh.ActionName = EditorGUILayout.TextField(actionBh.ActionName,
                            MTIONSDKToolsWindow.TextFieldStyle, new GUILayoutOption[] { GUILayout.Width(100), });
                        
                        actionBh.ActionDescription = EditorGUILayout.TextField(actionBh.ActionDescription,
                            MTIONSDKToolsWindow.TextFieldStyle, new GUILayoutOption[] { GUILayout.Width(300), });
#if MTION_ADVANCED_ACTION_UI
                        actionBh.DefaultChatCommand = EditorGUILayout.TextField(actionBh.DefaultChatCommand, 
                            MTIONSDKToolsWindow.TextFieldStyle, new GUILayoutOption[] { GUILayout.Width(100), });
#endif

                        EditorGUILayout.EndHorizontal();
                        
#if MTION_ADVANCED_ACTION_UI
                        EditorGUILayout.Space(10);
                        EditorGUILayout.LabelField("Action Entry", MTIONSDKToolsWindow.ListHeaderStyle);
                        
                        foreach (var entryPoint in actionBh.ActionEntryPoints)
                        {
                            StartBox();

                            EditorGUILayout.BeginHorizontal(style);

                            int prefId = entryPoint.Target == null ? -1 : entryPoint.Target.GetInstanceID();
                            entryPoint.Target = (UnityEngine.Object)EditorGUILayout.ObjectField(entryPoint.Target,
                                typeof(MonoBehaviour), true, new GUILayoutOption[] { GUILayout.MaxWidth(250), });
                            int newId = entryPoint.Target == null ? -1 : entryPoint.Target.GetInstanceID();
                            
                            EditorGUILayout.EndHorizontal();

                            bool ActionUpdated = false;
                            if (prefId != newId)
                            {
                                ActionUpdated = true;
                            }

                            if (entryPoint.Target != null)
                            {
                                MethodInfo method = entryPoint.Target.GetType().GetMethod("ActionEntryPoint");
                                if (method == null)
                                {
                                    EditorGUILayout.LabelField("Object does not contain valid interface.");
                                }
                                else
                                {
                                    ParameterInfo[] parameterInfo = method.GetParameters();
                                    
                                    if (parameterInfo.Length > 1)
                                    {
                                        EditorGUILayout.LabelField("Parameters", MTIONSDKToolsWindow.LabelStyle);
                                        
                                        EditorGUILayout.BeginHorizontal(style);
                                        EditorGUILayout.LabelField("Name", MTIONSDKToolsWindow.LabelStyle,
                                            new GUILayoutOption[] { GUILayout.Width(100), });
                                        EditorGUILayout.LabelField("Type", MTIONSDKToolsWindow.LabelStyle,
                                            new GUILayoutOption[] { GUILayout.Width(75), });
                                        EditorGUILayout.LabelField("Description", MTIONSDKToolsWindow.LabelStyle,
                                            new GUILayoutOption[] { GUILayout.MaxWidth(250), });
                                        EditorGUILayout.EndHorizontal();
                                    }

                                    for (int i = 0; i < parameterInfo.Length; i++)
                                    {
                                        if (parameterInfo[i].ParameterType == typeof(ActionMetadata))
                                        {
                                            continue;
                                        }

                                        EditorGUILayout.BeginHorizontal(style);
                                        if (entryPoint.EntryPoint.ParameterDefinitions.Count <= i)
                                        {
                                            entryPoint.EntryPoint.ParameterDefinitions.Add(
                                                new ActionEntryParameterInfo());
                                        }

                                        ActionEntryParameterInfo actionparams =
                                            entryPoint.EntryPoint.ParameterDefinitions[i];

                                        actionparams.Name = parameterInfo[i].Name;
                                        guiEnabled = GUI.enabled;
                                        GUI.enabled = false;
                                        EditorGUILayout.TextField(actionparams.Name, MTIONSDKToolsWindow.TextFieldStyle,
                                            new GUILayoutOption[] { GUILayout.Width(100), });
                                        actionparams.ParameterType =
                                            $"{parameterInfo[i].ParameterType.FullName}, {parameterInfo[i].ParameterType.Assembly.FullName}";

                                        EditorGUILayout.TextField($"{parameterInfo[i].ParameterType.FullName}".Split('.')[^1],
                                            MTIONSDKToolsWindow.TextFieldStyle,
                                            new GUILayoutOption[] { GUILayout.Width(75), });
                                        GUI.enabled = guiEnabled;

                                        string prev = actionparams.Description;
                                        actionparams.Description = EditorGUILayout.TextField(actionparams.Description,
                                            MTIONSDKToolsWindow.TextFieldStyle,
                                            new GUILayoutOption[] { GUILayout.MaxWidth(250), });
                                        ActionUpdated |= prev != actionparams.Description;

                                        guiEnabled = GUI.enabled;
                                        GUI.enabled = false;
                                        EditorGUILayout.LabelField(" ", MTIONSDKToolsWindow.LabelStyle,
                                            new GUILayoutOption[] { GUILayout.Width(5) });
                                        GUI.enabled = guiEnabled;

                                        if (TypeConversion.NumericConverter.IsNumericType(
                                                parameterInfo[i].ParameterType))
                                        {
                                            if (actionparams.Metadata == null ||
                                                actionparams.Metadata.GetType() != typeof(NumericMetadata))
                                            {
                                                actionparams.Metadata = new NumericMetadata();
                                            }

                                            var min = actionparams.Metadata.Cast<NumericMetadata>().Min;
                                            var max = actionparams.Metadata.Cast<NumericMetadata>().Max;
                                            var def = actionparams.Metadata.Cast<NumericMetadata>().Default;

                                            EditorGUILayout.LabelField("Min", MTIONSDKToolsWindow.LabelStyle,
                                                new GUILayoutOption[] { GUILayout.Width(30) });
                                            actionparams.Metadata.Cast<NumericMetadata>().Min =
                                                EditorGUILayout.FloatField(min, MTIONSDKToolsWindow.TextFieldStyle,
                                                    new GUILayoutOption[] { GUILayout.Width(55), });
                                            EditorGUILayout.LabelField("Max", MTIONSDKToolsWindow.LabelStyle,
                                                new GUILayoutOption[] { GUILayout.Width(30) });
                                            actionparams.Metadata.Cast<NumericMetadata>().Max =
                                                EditorGUILayout.FloatField(max, MTIONSDKToolsWindow.TextFieldStyle,
                                                    new GUILayoutOption[] { GUILayout.Width(55), });
                                            EditorGUILayout.LabelField("Default", MTIONSDKToolsWindow.LabelStyle,
                                                new GUILayoutOption[] { GUILayout.Width(50) });
                                            actionparams.Metadata.Cast<NumericMetadata>().Default =
                                                EditorGUILayout.FloatField(def, MTIONSDKToolsWindow.TextFieldStyle,
                                                    new GUILayoutOption[] { GUILayout.Width(55), });
                                            ActionUpdated |= min != actionparams.Metadata.Cast<NumericMetadata>().Min;
                                            ActionUpdated |= max != actionparams.Metadata.Cast<NumericMetadata>().Max;
                                            ActionUpdated |=
                                                def != actionparams.Metadata.Cast<NumericMetadata>().Default;

                                        }
                                        else if (parameterInfo[i].ParameterType == typeof(string))
                                        {
                                            if (actionparams.Metadata == null ||
                                                actionparams.Metadata.GetType() != typeof(StringMetadata))
                                            {
                                                actionparams.Metadata = new StringMetadata();
                                            }

                                            var maxLen = actionparams.Metadata.Cast<StringMetadata>().TotalLength;
                                            var def = actionparams.Metadata.Cast<StringMetadata>().Default;
                                            EditorGUILayout.LabelField(new GUIContent("Max Length", "-1 for infinity"), MTIONSDKToolsWindow.LabelStyle,
                                                new GUILayoutOption[] { GUILayout.Width(75) });
                                            actionparams.Metadata.Cast<StringMetadata>().TotalLength =
                                                EditorGUILayout.IntField(maxLen, MTIONSDKToolsWindow.TextFieldStyle,
                                                    new GUILayoutOption[] { GUILayout.Width(55), });
                                            EditorGUILayout.LabelField("Default", MTIONSDKToolsWindow.LabelStyle,
                                                new GUILayoutOption[] { GUILayout.Width(50) });
                                            actionparams.Metadata.Cast<StringMetadata>().Default =
                                                EditorGUILayout.TextField(def, MTIONSDKToolsWindow.TextFieldStyle,
                                                    new GUILayoutOption[] { GUILayout.Width(55), });
                                            ActionUpdated |= maxLen != actionparams.Metadata.Cast<StringMetadata>()
                                                .TotalLength;
                                            ActionUpdated |=
                                                def != actionparams.Metadata.Cast<StringMetadata>().Default;

                                        }
                                        else if (parameterInfo[i].ParameterType == typeof(bool))
                                        {
                                            if (actionparams.Metadata == null ||
                                                actionparams.Metadata.GetType() != typeof(BoolMetadata))
                                            {
                                                actionparams.Metadata = new BoolMetadata();
                                            }
                                        }
                                        else if (TypeConversion.ContainerConverter.IsContainer(parameterInfo[i]
                                                     .ParameterType))
                                        {
                                            if (actionparams.Metadata == null ||
                                                actionparams.Metadata.GetType() != typeof(ContainerMetadata))
                                            {
                                                actionparams.Metadata = new ContainerMetadata();
                                            }

                                            var maxCount = actionparams.Metadata.Cast<ContainerMetadata>().MaxCount;
                                            EditorGUILayout.LabelField("Max count", MTIONSDKToolsWindow.LabelStyle,
                                                new GUILayoutOption[] { GUILayout.Width(75) });
                                            actionparams.Metadata.Cast<ContainerMetadata>().MaxCount =
                                                EditorGUILayout.IntField(maxCount, MTIONSDKToolsWindow.TextFieldStyle,
                                                    new GUILayoutOption[] { GUILayout.Width(55), });
                                            ActionUpdated |= maxCount != actionparams.Metadata.Cast<ContainerMetadata>()
                                                .MaxCount;

                                        }
                                        else if (parameterInfo[i].ParameterType == typeof(Object))
                                        {
                                            if (actionparams.Metadata == null ||
                                                actionparams.Metadata.GetType() != typeof(ObjectMetadata))
                                            {
                                                actionparams.Metadata = new ObjectMetadata();
                                            }
                                        }
                                        else
                                        {
                                            if (actionparams.Metadata == null ||
                                                actionparams.Metadata.GetType() != typeof(TypeMetadata))
                                            {
                                                actionparams.Metadata = new TypeMetadata();
                                            }
                                        }

                                        EditorGUILayout.EndHorizontal();

                                    }
                                }
                            }

                            if (ActionUpdated)
                            {
                                EditorUtility.SetDirty(_actionBehaviourContainer);
                                EditorUtility.SetDirty(actionBh);
                            }
                            EndBox();
                        }
                        
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace(); // Push to right

                        guiEnabled = GUI.enabled;
                        GUI.enabled = actionBh.ActionEntryPoints.Count == 0 && guiEnabled;
                        if (GUILayout.Button("+", MTIONSDKToolsWindow.SmallButtonStyle, new GUILayoutOption[] { GUILayout.Width(30), }))
                        {
                            actionBh.ActionEntryPoints.Add(new ActionEntryPointInternal
                            {
                                EntryPoint = new ActionEntryPointInfo
                                {
                                    Guid = SDKUtil.GenerateNewGUID()
                                }
                            });
                            EditorUtility.SetDirty(_actionBehaviourContainer);

                        }
                        GUI.enabled = guiEnabled;
                        
                        if (GUILayout.Button("-", MTIONSDKToolsWindow.SmallButtonStyle, new GUILayoutOption[] { GUILayout.Width(30) }))
                        {
                            if (actionBh.ActionEntryPoints.Count > 0)
                            {
                                actionBh.ActionEntryPoints.RemoveAt(actionBh.ActionEntryPoints.Count - 1);
                                EditorUtility.SetDirty(_actionBehaviourContainer);
                            }
                        }
                        GUILayout.EndHorizontal();
                        
                        EditorGUILayout.Space(10);
                        EditorGUILayout.LabelField("Action Output", MTIONSDKToolsWindow.ListHeaderStyle);
                        
                        foreach (var exitPoint in actionBh.ActionExitPoints)
                        {
                            StartBox();

                            EditorGUILayout.BeginHorizontal(style);

                            int prefId = exitPoint.Target == null ? -1 : exitPoint.Target.GetInstanceID();
                            exitPoint.Target = (UnityEngine.Object)EditorGUILayout.ObjectField(exitPoint.Target,
                                typeof(MonoBehaviour), true, new GUILayoutOption[] { GUILayout.MaxWidth(250), });
                            int newId = exitPoint.Target == null ? -1 : exitPoint.Target.GetInstanceID();
                            
                            EditorGUILayout.EndHorizontal();

                            bool ActionUpdated = false;
                            if (prefId != newId)
                            {
                                ActionUpdated = true;
                            }

                            if (exitPoint.Target != null)
                            {
                                if (!(exitPoint.Target is IMActionExitEvent))
                                {
                                    EditorGUILayout.LabelField("Object does not contain valid interface.");
                                }
                                else
                                {
                                    EditorGUILayout.BeginHorizontal(style);
                                    
                                    EditorGUILayout.LabelField("Name", MTIONSDKToolsWindow.LabelStyle,
                                        new GUILayoutOption[] { GUILayout.Width(100), });
                                    EditorGUILayout.LabelField("Description", MTIONSDKToolsWindow.LabelStyle,
                                        new GUILayoutOption[] { GUILayout.MaxWidth(250), });
                                    
                                    EditorGUILayout.EndHorizontal();
                                    
                                    EditorGUILayout.BeginHorizontal(style);
                                    exitPoint.ExitPoint.Name =
                                        EditorGUILayout.TextField(exitPoint.ExitPoint.Name,
                                            MTIONSDKToolsWindow.TextFieldStyle,
                                            new GUILayoutOption[] { GUILayout.MaxWidth(100) });
                                    exitPoint.ExitPoint.Description =
                                        EditorGUILayout.TextField(exitPoint.ExitPoint.Description,
                                            MTIONSDKToolsWindow.TextFieldStyle);
                                    EditorGUILayout.EndHorizontal();
                                }
                            }

                            if (ActionUpdated)
                            {
                                EditorUtility.SetDirty(_actionBehaviourContainer);
                                EditorUtility.SetDirty(actionBh);
                            }
                            EndBox();
                        }
                        
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace(); // Push to right

                        guiEnabled = GUI.enabled;
                        GUI.enabled = actionBh.ActionExitPoints.Count == 0 && guiEnabled;
                        if (GUILayout.Button("+", MTIONSDKToolsWindow.SmallButtonStyle, new GUILayoutOption[] { GUILayout.Width(30), }))
                        {
                            actionBh.ActionExitPoints.Add(new ActionExitPointInternal
                            {
                                ExitPoint = new ActionExitPointInfo
                                {
                                    Guid = SDKUtil.GenerateNewGUID()
                                }
                            });
                            EditorUtility.SetDirty(_actionBehaviourContainer);
                        }
                        GUI.enabled = guiEnabled;
                        
                        if (GUILayout.Button("-", MTIONSDKToolsWindow.SmallButtonStyle, new GUILayoutOption[] { GUILayout.Width(30) }))
                        {
                            if (actionBh.ActionExitPoints.Count > 0)
                            {
                                actionBh.ActionExitPoints.RemoveAt(actionBh.ActionExitPoints.Count - 1);
                                EditorUtility.SetDirty(_actionBehaviourContainer);
                            }
                        }
                        GUILayout.EndHorizontal();
                        
                        EditorGUILayout.Space(10);
                        EditorGUILayout.LabelField("Action Output Parameters", MTIONSDKToolsWindow.ListHeaderStyle);
                        
                        foreach (var parameterProvider in actionBh.ActionExitParameterProviders)
                        {
                            StartBox();

                            EditorGUILayout.BeginHorizontal(style);

                            int prefId = parameterProvider.Target == null ? -1 : parameterProvider.Target.GetInstanceID();
                            parameterProvider.Target = (UnityEngine.Object)EditorGUILayout.ObjectField(parameterProvider.Target,
                                typeof(MonoBehaviour), true, new GUILayoutOption[] { GUILayout.MaxWidth(250), });
                            int newId = parameterProvider.Target == null ? -1 : parameterProvider.Target.GetInstanceID();
                            
                            EditorGUILayout.EndHorizontal();

                            bool ActionUpdated = false;
                            if (prefId != newId)
                            {
                                ActionUpdated = true;
                            }

                            if (parameterProvider.Target != null)
                            {
                                if (parameterProvider.Target is IMActionExitParameterProvider providerImplementation)
                                {
                                    IReadOnlyList<string> parameterNames = providerImplementation.GetParameterNames();
                                    for(int i = 0; i < parameterNames.Count; i++)
                                    foreach (string parameterName in providerImplementation.GetParameterNames())
                                    {
                                        if (parameterProvider.Parameters.Count == i)
                                        {
                                            parameterProvider.Parameters.Add(new ActionExitParameterInfo
                                            {
                                                Guid = SDKUtil.GenerateNewGUID(),
                                                Name = parameterName,
                                                ParameterType = 
                                                    $"{providerImplementation.GetParameterType(parameterName).FullName}, " +
                                                    $"{providerImplementation.GetParameterType(parameterName).Assembly.FullName}"
                                            });
                                        }
                                        else
                                        {
                                            ActionExitParameterInfo parameterInfo = parameterProvider.Parameters[i];
                                            if (parameterInfo.Name != providerImplementation.GetParameterName(i) ||
                                                parameterInfo.ParameterType !=
                                                $"{providerImplementation.GetParameterType(i).FullName}, " +
                                                $"{providerImplementation.GetParameterType(i).Assembly.FullName}")
                                            {
                                                parameterProvider.Parameters[i] = new ActionExitParameterInfo
                                                {
                                                    Guid = SDKUtil.GenerateNewGUID(),
                                                    Name = parameterName,
                                                    ParameterType =
                                                        $"{providerImplementation.GetParameterType(parameterName).FullName}, " +
                                                        $"{providerImplementation.GetParameterType(parameterName).Assembly.FullName}"
                                                };
                                            }
                                        }
                                    }

                                    while (parameterProvider.Parameters.Count > providerImplementation.TotalExitParameterConnectors)
                                    {
                                        parameterProvider.Parameters.RemoveAt(parameterProvider.Parameters.Count - 1);
                                    }
                                    
                                    EditorGUILayout.BeginHorizontal(style);
                            
                                    EditorGUILayout.LabelField("Name", MTIONSDKToolsWindow.LabelStyle,
                                        new GUILayoutOption[] { GUILayout.Width(100), });
                                    EditorGUILayout.LabelField("Type", MTIONSDKToolsWindow.LabelStyle,
                                        new GUILayoutOption[] { GUILayout.Width(75), });
                            
                                    EditorGUILayout.EndHorizontal();

                                    guiEnabled = GUI.enabled;
                                    GUI.enabled = false;
                                    foreach (var parameterInfo in parameterProvider.Parameters)
                                    {
                                        EditorGUILayout.BeginHorizontal();
                                        EditorGUILayout.TextField(parameterInfo.Name,
                                            MTIONSDKToolsWindow.TextFieldStyle,
                                            new GUILayoutOption[] { GUILayout.Width(100), });
                                        EditorGUILayout.TextField($"{parameterInfo.ParameterType}".Split(',')[0].Split('.')[^1],
                                            MTIONSDKToolsWindow.TextFieldStyle,
                                            new GUILayoutOption[] { GUILayout.Width(75), });
                                        EditorGUILayout.EndHorizontal();
                                    }

                                    GUI.enabled = guiEnabled;
                                }
                                else
                                {
                                    EditorGUILayout.LabelField("Object does not contain valid interface.");
                                }
                            }

                            if (ActionUpdated)
                            {
                                EditorUtility.SetDirty(_actionBehaviourContainer);
                                EditorUtility.SetDirty(actionBh);
                            }
                            EndBox();
                        }
                        
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace(); // Push to right
                        if (GUILayout.Button("+", MTIONSDKToolsWindow.SmallButtonStyle, new GUILayoutOption[] { GUILayout.Width(30), }))
                        {
                            actionBh.ActionExitParameterProviders.Add(new ActionExitParametersProviderInternal
                            {
                                Parameters = new List<ActionExitParameterInfo>()
                            });
                            EditorUtility.SetDirty(_actionBehaviourContainer);

                        }
                        if (GUILayout.Button("-", MTIONSDKToolsWindow.SmallButtonStyle, new GUILayoutOption[] { GUILayout.Width(30) }))
                        {
                            if (actionBh.ActionExitParameterProviders.Count > 0)
                            {
                                actionBh.ActionExitParameterProviders.RemoveAt(actionBh.ActionExitParameterProviders.Count - 1);
                                EditorUtility.SetDirty(_actionBehaviourContainer);
                            }
                        }
                        GUILayout.EndHorizontal();
#endif
                        GUI.enabled = actionGuiEnabled;
                    }
                    EndBox();

                    if (actionBh.ActionName != dftNameCache)
                    {
                        EditorUtility.SetDirty(_actionBehaviourContainer);
                    }                           
                }
#if MTION_ADVANCED_ACTION_UI
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("+", MTIONSDKToolsWindow.SmallButtonStyle, new GUILayoutOption[] { GUILayout.Width(30) }))
                        {
                            var ab = _actionBehaviourContainer.CreateAction();
                            EditorUtility.SetDirty(_actionBehaviourContainer);
                            EditorUtility.SetDirty(ab);
                        }
                        if (GUILayout.Button("-", MTIONSDKToolsWindow.SmallButtonStyle, new GUILayoutOption[] { GUILayout.Width(30) }))
                        {
                            _actionBehaviourContainer.DeleteLast();
                            EditorUtility.SetDirty(_actionBehaviourContainer);
                        }
                    }
                    GUILayout.EndHorizontal();
                }
#endif
            }
            EndBox();
        }
    }

    private static void RefreshSdkEntryPointState()
    {
        _sdkEntryPointRows.Clear();
        _sdkEntryPointMessage = null;
        _sdkEntryPointMessageIsWarning = false;

        GameObject descriptor = BuildManager.GetSceneDescriptor();
        if (descriptor == null)
        {
            _sdkEntryPointMessage = "Initialize the scene under the Build tab to review SDK entry points.";
            return;
        }

        MTIONSDKDescriptorSceneBase descriptorComponent = descriptor.GetComponent<MTIONSDKDescriptorSceneBase>();
        if (descriptorComponent == null || descriptorComponent.ObjectReferenceProp == null)
        {
            _sdkEntryPointMessage = "The current SDK descriptor is missing its target object reference.";
            _sdkEntryPointMessageIsWarning = true;
            return;
        }

        GameObject sdkRoot = descriptorComponent.ObjectReferenceProp;
        UVSSDKEntryPointRegistry registry = sdkRoot.GetComponentInChildren<UVSSDKEntryPointRegistry>(true);
        if (registry == null)
        {
            _sdkEntryPointMessage = "No SDK entry points are currently synced. Add SDK Entry Point nodes in the graph, then click Refresh in the UVS or Actions tab.";
            return;
        }

        if (registry.HasDuplicateDisplayNames(out List<string> duplicateDisplayNames))
        {
            _sdkEntryPointMessage = $"Duplicate SDK entry point display names detected: {string.Join(", ", duplicateDisplayNames)}. Fix the graph entries, then click Refresh.";
            _sdkEntryPointMessageIsWarning = true;
        }

        IReadOnlyList<UVSSDKEntryPointDefinition> entryPoints = registry.EntryPoints;
        for (int i = 0; i < entryPoints.Count; i++)
        {
            UVSSDKEntryPointDefinition entryPoint = entryPoints[i];
            if (entryPoint == null || string.IsNullOrWhiteSpace(entryPoint.EntryPointId))
            {
                continue;
            }

            GameObject targetObject = registry.ResolveTargetGameObject(sdkRoot, entryPoint.EntryPointId);
            _sdkEntryPointRows.Add(new SDKEntryPointRow
            {
                DisplayName = string.IsNullOrWhiteSpace(entryPoint.DisplayName)
                    ? UVSSDKEntryPointConstants.DefaultDisplayName
                    : entryPoint.DisplayName,
                EntryPointId = entryPoint.EntryPointId,
                TargetObject = targetObject,
                TargetLabel = BuildTargetLabel(targetObject, entryPoint),
            });
        }

        if (_sdkEntryPointRows.Count == 0 && string.IsNullOrWhiteSpace(_sdkEntryPointMessage))
        {
            _sdkEntryPointMessage = "No SDK entry points are currently synced. Add SDK Entry Point nodes in the graph, then click Refresh in the UVS or Actions tab.";
        }
    }

    private static string BuildTargetLabel(GameObject targetObject, UVSSDKEntryPointDefinition entryPoint)
    {
        if (targetObject != null)
        {
            return targetObject.name;
        }

        if (!string.IsNullOrWhiteSpace(entryPoint.TargetRelativePathFromRoot))
        {
            return $"Missing target ({entryPoint.TargetRelativePathFromRoot})";
        }

        return "Root target";
    }

    private static void DrawSdkEntryPointsSection()
    {
        StartBox();
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("SDK Entry Points", MTIONSDKToolsWindow.BoxHeaderStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", MTIONSDKToolsWindow.SmallButtonStyle, GUILayout.Width(80)))
            {
                RefreshSdkEntryPointsFromUvs();
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(_sdkEntryPointMessage))
            {
                GUILayout.Space(6);
                if (_sdkEntryPointMessageIsWarning)
                {
                    MTIONSDKToolsWindow.DrawWarning(_sdkEntryPointMessage);
                }
                else
                {
                    EditorGUILayout.LabelField(_sdkEntryPointMessage, MTIONSDKToolsWindow.LabelStyle);
                }
            }

            if (_sdkEntryPointRows.Count > 0)
            {
                GUILayout.Space(8);
                EditorGUILayout.LabelField($"Synced UVS entry points: {_sdkEntryPointRows.Count}", MTIONSDKToolsWindow.LabelStyle);
                GUILayout.Space(6);

                for (int i = 0; i < _sdkEntryPointRows.Count; i++)
                {
                    SDKEntryPointRow row = _sdkEntryPointRows[i];
                    StartBox();
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(row.DisplayName, MTIONSDKToolsWindow.ListHeaderStyle);
                        GUILayout.FlexibleSpace();
                        if (row.TargetObject != null && GUILayout.Button("Ping", MTIONSDKToolsWindow.SmallButtonStyle, GUILayout.Width(60)))
                        {
                            Selection.activeObject = row.TargetObject;
                            EditorGUIUtility.PingObject(row.TargetObject);
                        }
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.LabelField($"Target: {row.TargetLabel}", MTIONSDKToolsWindow.LabelStyle);
                        EditorGUILayout.LabelField($"Entry ID: {row.EntryPointId}", MTIONSDKToolsWindow.LabelStyle);
                    }
                    EndBox();
                }
            }
        }
        EndBox();
    }


    private static void StartBox(bool alt = false)
    {
        GUIStyle modifiedBox = GUI.skin.GetStyle("Box");

        if (alt)
        {
            modifiedBox.normal.background = Texture2D.whiteTexture;
        }

        EditorGUILayout.BeginHorizontal(modifiedBox);
        GUILayout.Label(string.Empty, GUILayout.MaxWidth(5));

        EditorGUILayout.BeginVertical();
        GUILayout.Label(string.Empty, GUILayout.MaxHeight(5));
    }

    private static void EndBox()
    {
        GUILayout.Label(string.Empty, GUILayout.MaxHeight(5));
        EditorGUILayout.EndVertical();

        GUILayout.Label(string.Empty, GUILayout.MaxWidth(5));
        EditorGUILayout.EndHorizontal();

        GUILayout.Label(string.Empty, GUILayout.MaxHeight(5));
    }
}
