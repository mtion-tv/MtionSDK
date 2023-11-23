using System.Collections.Generic;
using System.Reflection;
using mtion.room.sdk;
using mtion.room.sdk.action;
using mtion.room.sdk.compiled;
using UnityEditor;
using UnityEngine;

public static class MTIONSDKToolsActionTab
{
    private static MActionBehaviourGroup _actionBehaviourContainer = null;
    private static Vector2 _scrollPos;

    public static void Refresh()
    {
        VerifySceneState();
    }

    private static void VerifySceneState()
    {
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
        actionBehaviour.ActionEntryPoints.Add(
            new ActionEntryPointInternal()
            {
                Target = action as MonoBehaviour,
                EntryPoint = new ActionEntryPointInfo()
                {
                    Guid = SDKUtil.GenerateNewGUID()
                }
            });
        actionBehaviour.BuildEntryMap();
        
        AddActionParameters(action, actionBehaviour);
    }
    
    private static void AddActionParameters(IAction action, MActionBehaviour actionBehaviour)
    {
        // Get entry point for Action
        MethodInfo method = action.GetType().GetMethod("ActionEntryPoint");
        if (method == null)
        {
            EditorGUILayout.LabelField("Object does not contain valid interface.");
        }
        else
        {
            ActionEntryPointInternal entryPoint = actionBehaviour.ActionEntryPoints[0];
            
            // Get Signature Information
            ParameterInfo[] parameterInfo = method.GetParameters();
            
            // Build Data Map based on parameter details
            for (int i = 0; i < parameterInfo.Length; i++)
            {
                if (parameterInfo[i].ParameterType == typeof(ActionMetadata))
                {
                    // Skip, as this is handled internal
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

    public static void GenerateActionList()
    {
#if MTION_ADVANCED_ACTION_UI
        bool guiEnabled;
#endif
        
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
            // Main object
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

                        // Header
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
                        
                        // Options
                        EditorGUILayout.BeginHorizontal(style);
                        
                        actionBh.Active = EditorGUILayout.Toggle(actionBh.Active,
                            new GUILayoutOption[] { GUILayout.Width(50) });

                        bool actionGuiEnabled = GUI.enabled;
                        GUI.enabled = actionBh.Active;
                        
                        // Name
                        actionBh.ActionName = EditorGUILayout.TextField(actionBh.ActionName,
                            MTIONSDKToolsWindow.TextFieldStyle, new GUILayoutOption[] { GUILayout.Width(100), });
                        
                        // Info
                        actionBh.ActionDescription = EditorGUILayout.TextField(actionBh.ActionDescription,
                            MTIONSDKToolsWindow.TextFieldStyle, new GUILayoutOption[] { GUILayout.Width(300), });
#if MTION_ADVANCED_ACTION_UI
                        actionBh.DefaultChatCommand = EditorGUILayout.TextField(actionBh.DefaultChatCommand, 
                            MTIONSDKToolsWindow.TextFieldStyle, new GUILayoutOption[] { GUILayout.Width(100), });
#endif

                        EditorGUILayout.EndHorizontal();
                        
                        // Build method mappings
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
                                // Get entry point for Action
                                MethodInfo method = entryPoint.Target.GetType().GetMethod("ActionEntryPoint");
                                if (method == null)
                                {
                                    EditorGUILayout.LabelField("Object does not contain valid interface.");
                                }
                                else
                                {
                                    // Get Signature Information
                                    ParameterInfo[] parameterInfo = method.GetParameters();
                                    
                                    if (parameterInfo.Length > 1)
                                    {
                                        EditorGUILayout.LabelField("Parameters", MTIONSDKToolsWindow.LabelStyle);
                                        
                                        EditorGUILayout.BeginHorizontal(style);
                                        // Divider
                                        // GUI.enabled = false;
                                        EditorGUILayout.LabelField("Name", MTIONSDKToolsWindow.LabelStyle,
                                            new GUILayoutOption[] { GUILayout.Width(100), });
                                        EditorGUILayout.LabelField("Type", MTIONSDKToolsWindow.LabelStyle,
                                            new GUILayoutOption[] { GUILayout.Width(75), });
                                        EditorGUILayout.LabelField("Description", MTIONSDKToolsWindow.LabelStyle,
                                            new GUILayoutOption[] { GUILayout.MaxWidth(250), });
                                        // GUI.enabled = true;
                                        EditorGUILayout.EndHorizontal();
                                    }

                                    // Build Data Map based on parameter details
                                    for (int i = 0; i < parameterInfo.Length; i++)
                                    {
                                        if (parameterInfo[i].ParameterType == typeof(ActionMetadata))
                                        {
                                            // Skip, as this is handled internal
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

                                        // TODO: Refactor this code for creating objects
                                        // Decouple UI from logic
                                        // Draw Interface
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

                                    while (parameterProvider.Parameters.Count > providerImplementation.Count)
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
                // UI Button Control
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

    ///////////////////////////////////////////////////////////////////////////////////////////////////
    /// UTILITY
    ///////////////////////////////////////////////////////////////////////////////////////////////////

    private static void StartBox(bool alt = false)
    {
        // Setup a new modified box skin for Unity's new GUI
        GUIStyle modifiedBox = GUI.skin.GetStyle("Box");

        // If we're not using the lighter UI skin, preserve the white backgrounds we used to have
        if (alt)
        {
            modifiedBox.normal.background = Texture2D.whiteTexture;
        }

        // Create the group normally using the modified box style
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

        // Add vertical space at the end of every box.
        GUILayout.Label(string.Empty, GUILayout.MaxHeight(5));
    }
}
