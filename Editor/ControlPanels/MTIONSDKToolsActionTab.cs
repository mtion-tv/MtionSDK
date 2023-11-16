using mtion.room.sdk;
using mtion.room.sdk.action;
using mtion.room.sdk.compiled;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MTIONSDKToolsActionTab
{
    private static MActionBehaviourGroup _actionBehaviourContainer = null;
    private static Dictionary<string, int> _editorGuidMappingCache = new Dictionary<string, int>();
    private static Vector2 _scrollPos;

    public static void Refresh()
    {
        if (_actionBehaviourContainer == null)
        {
            VerifySceneState();
        }

        if (_actionBehaviourContainer == null)
        {
            return;
        }

        MActionBehaviour[] inSceneABs = GameObject.FindObjectsOfType<MActionBehaviour>();
        HashSet<string> inSceneGuids = new HashSet<string>();
        foreach (var actionBehaviour in inSceneABs)
        {
            inSceneGuids.Add(actionBehaviour.Guid);
        }

        List<int> actionsToDelete = new List<int>();
        List<MActionBehaviour> inObjectABs = _actionBehaviourContainer.GetActions();
        HashSet<string> inObjectGuids = new HashSet<string>();
        foreach (var actionBehaviour in inObjectABs)
        {
            inObjectGuids.Add(actionBehaviour.Guid);
        }

        for (int i = 0; i < inObjectABs.Count; ++i)
        {
            var action = inObjectABs[i];
            if (!inSceneGuids.Contains(action.Guid))
            {
                actionsToDelete.Add(i);
            }
        }

        foreach (var actionIndex in actionsToDelete)
        {
            inObjectABs.RemoveAt(actionIndex);
        }

        int index = 0;
        _editorGuidMappingCache.Clear();
        foreach (var action in _actionBehaviourContainer.GetActions())
        {
            if (!_editorGuidMappingCache.ContainsKey(action.Guid))
            {
                _editorGuidMappingCache.Add(action.Guid, index);
            }
            else
            {
                _editorGuidMappingCache[action.Guid] = index;
            }
            index++;
        }
    }

    private static void VerifySceneState()
    {
        if (_actionBehaviourContainer == null)
        {
            _actionBehaviourContainer = GameObject.FindAnyObjectByType<MActionBehaviourGroup>();
            if (_actionBehaviourContainer == null)
            {
                var descriptor = BuildManager.GetSceneDescriptor();
                if (descriptor == null)
                {
                    return;
                }

                var rootObject = BuildManager.GetSceneDescriptor().GetComponent<MTIONSDKDescriptorSceneBase>();
                if (rootObject == null)
                {
                    return;
                }

                var actionGo = new GameObject("ActionBehaviourContainer");
                actionGo.transform.parent = rootObject.ObjectReference.transform;
                _actionBehaviourContainer = actionGo.AddComponent<MActionBehaviourGroup>();
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
                        EditorGUILayout.LabelField("Name", MTIONSDKToolsWindow.ListHeaderStyle, new GUILayoutOption[] { GUILayout.Width(100) });
                        
                        // Divider
                        GUI.enabled = false;
                        EditorGUILayout.LabelField(" ", new GUILayoutOption[] { GUILayout.Width(3), });
                        EditorGUILayout.TextField(" ", new GUILayoutOption[] { GUILayout.Width(2), });
                        EditorGUILayout.LabelField(" ", new GUILayoutOption[] { GUILayout.Width(3), });
                        GUI.enabled = true;

                        EditorGUILayout.LabelField("Description", MTIONSDKToolsWindow.ListHeaderStyle);
                        EditorGUILayout.LabelField("Chat Command (Testing Only)", MTIONSDKToolsWindow.ListHeaderStyle);
                        EditorGUILayout.EndHorizontal();
                        
                        // Options
                        EditorGUILayout.BeginHorizontal(style);

                        // Name
                        actionBh.ActionName = EditorGUILayout.TextField(actionBh.ActionName, MTIONSDKToolsWindow.TextFieldStyle, new GUILayoutOption[] { GUILayout.Width(100), });
                        
                        // Divider
                        GUI.enabled = false;
                        EditorGUILayout.LabelField(" ", new GUILayoutOption[] { GUILayout.Width(3), });
                        EditorGUILayout.TextField(" ", new GUILayoutOption[] { GUILayout.Width(2), });
                        EditorGUILayout.LabelField(" ", new GUILayoutOption[] { GUILayout.Width(3), });
                        GUI.enabled = true;

                        // Info
                        actionBh.ActionDescription = EditorGUILayout.TextField(actionBh.ActionDescription, MTIONSDKToolsWindow.TextFieldStyle);
                        actionBh.DefaultChatCommand = EditorGUILayout.TextField(actionBh.DefaultChatCommand, MTIONSDKToolsWindow.TextFieldStyle);

                        EditorGUILayout.EndHorizontal();
                        
                        // Build method mappings

                        EditorGUILayout.Space(10);
                        EditorGUILayout.LabelField("Action Entries", MTIONSDKToolsWindow.ListHeaderStyle);
                        
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
                                        EditorGUILayout.LabelField(actionparams.Name, MTIONSDKToolsWindow.LabelStyle,
                                            new GUILayoutOption[] { GUILayout.Width(55), });
                                        actionparams.ParameterType =
                                            $"{parameterInfo[i].ParameterType.FullName}, {parameterInfo[i].ParameterType.Assembly.FullName}";
                                        EditorGUILayout.LabelField(actionparams.ParameterType,
                                            MTIONSDKToolsWindow.LabelStyle,
                                            new GUILayoutOption[] { GUILayout.Width(200), });

                                        string prev = actionparams.Description;
                                        actionparams.Description = EditorGUILayout.TextField(actionparams.Description,
                                            MTIONSDKToolsWindow.TextFieldStyle,
                                            new GUILayoutOption[] { GUILayout.MaxWidth(250), });
                                        ActionUpdated |= prev != actionparams.Description;

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

                                            actionparams.Metadata.Cast<NumericMetadata>().Min =
                                                EditorGUILayout.FloatField(min, MTIONSDKToolsWindow.TextFieldStyle,
                                                    new GUILayoutOption[] { GUILayout.Width(55), });
                                            actionparams.Metadata.Cast<NumericMetadata>().Max =
                                                EditorGUILayout.FloatField(max, MTIONSDKToolsWindow.TextFieldStyle,
                                                    new GUILayoutOption[] { GUILayout.Width(55), });
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
                                            actionparams.Metadata.Cast<StringMetadata>().TotalLength =
                                                EditorGUILayout.IntField(maxLen, MTIONSDKToolsWindow.TextFieldStyle,
                                                    new GUILayoutOption[] { GUILayout.Width(55), });
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
                        GUILayout.FlexibleSpace(); // Pushs to right
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
                                    exitPoint.ExitPoint.Name =
                                        EditorGUILayout.TextField("Name", exitPoint.ExitPoint.Name,
                                            MTIONSDKToolsWindow.TextFieldStyle,
                                            new GUILayoutOption[] { GUILayout.MaxWidth(250) });
                                    exitPoint.ExitPoint.Description =
                                        EditorGUILayout.TextField("Description", exitPoint.ExitPoint.Description,
                                            MTIONSDKToolsWindow.TextFieldStyle);//,
                                            //new GUILayoutOption[] { GUILayout.MaxWidth(250) });
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
                        GUILayout.FlexibleSpace(); // Pushs to right

                        bool guiEnabled = GUI.enabled;
                        GUI.enabled = actionBh.ActionExitPoints.Count == 0;
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

                                    foreach (var parameterInfo in parameterProvider.Parameters)
                                    {
                                        EditorGUILayout.BeginHorizontal();
                                        EditorGUILayout.LabelField(parameterInfo.Name,
                                            MTIONSDKToolsWindow.LabelStyle,
                                            new GUILayoutOption[] { GUILayout.Width(200), });
                                        EditorGUILayout.LabelField(parameterInfo.ParameterType,
                                            MTIONSDKToolsWindow.LabelStyle,
                                            new GUILayoutOption[] { GUILayout.Width(200), });
                                        EditorGUILayout.EndHorizontal();
                                    }
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
                        GUILayout.FlexibleSpace(); // Pushs to right
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
                    }
                    EndBox();

                    if (actionBh.ActionName != dftNameCache)
                    {
                        EditorUtility.SetDirty(_actionBehaviourContainer);
                    }                           
                }

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
