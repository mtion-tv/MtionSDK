using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using mtion.room.sdk.compiled;
using mtion.room.sdk.visualscripting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace mtion.room.sdk
{
    public static class MTIONSDKToolsVisualScriptingTab
    {
        private enum VisualScriptingTargetMode
        {
            None,
            Asset,
            Avatar,
            Room,
            BlueprintRoom,
        }

        private sealed class VisualScriptingComponentRow
        {
            public Component Component;
            public string GameObjectPath;
            public string ComponentTypeName;
            public bool IsMachine;
            public bool HasGraphAsset;
            public Object GraphAsset;
        }

        private static readonly List<VisualScriptingComponentRow> ComponentRows = new List<VisualScriptingComponentRow>();
        private static readonly List<string> ReferencedGraphAssets = new List<string>();
        private static readonly List<UVSSDKEntryPointDefinition> EntryPointDefinitions = new List<UVSSDKEntryPointDefinition>();
        private static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>(StringComparer.Ordinal);

        private static bool _showAdvanced;
        private static bool _stateDirty = true;
        private static bool _hasMachineComponents;
        private static bool _hasDeepInspection;
        private static Vector2 _scrollPos;
        private static GameObject _targetHost;
        private static Scene _targetScene;
        private static GameObject _targetRoot;
        private static string _targetLabel;
        private static string _targetError;
        private static string _placementIssue;
        private static VisualScriptingTargetMode _targetMode;
        private static MTIONSDKDescriptorSceneBase _descriptor;
        private static VisualScriptingInspectionReport _inspectionReport;
        private static VisualScriptingGeneratedDataAuditResult _projectAudit;


        public static void Invalidate()
        {
            _stateDirty = true;
        }

        public static void Refresh()
        {
            _stateDirty = false;
            _descriptor = null;
            _targetScene = default(Scene);
            _targetHost = null;
            _targetRoot = null;
            _targetLabel = null;
            _targetError = null;
            _placementIssue = null;
            _targetMode = VisualScriptingTargetMode.None;
            _inspectionReport = null;
            _projectAudit = VisualScriptingProjectPreflight.AuditGeneratedData();
            _hasMachineComponents = false;
            _hasDeepInspection = false;
            ComponentRows.Clear();
            ReferencedGraphAssets.Clear();
            EntryPointDefinitions.Clear();

            GameObject descriptorObject = GetSceneDescriptorObject();
            if (descriptorObject == null)
            {
                _targetError = "Add a room, blueprint, asset, or avatar descriptor to configure Unity Visual Scripting.";
                return;
            }

            _descriptor = descriptorObject.GetComponent<MTIONSDKDescriptorSceneBase>();
            if (_descriptor == null)
            {
                _targetError = "The current scene is missing its SDK descriptor component.";
                return;
            }

            ResolveTarget();
            RefreshPlacementState();
            BuildComponentState();
        }

        public static void Draw()
        {
            EnsureStateIsCurrent();

            using (EditorGUILayout.ScrollViewScope scrollView = new EditorGUILayout.ScrollViewScope(_scrollPos))
            {
                _scrollPos = scrollView.scrollPosition;

                DrawStatusBox();
                DrawIssuesBox();
                DrawActionBox();
                DrawAdvancedBox();
            }
        }


        private static void EnsureStateIsCurrent()
        {
            if (_stateDirty)
            {
                Refresh();
            }
        }

        private static void ResolveTarget()
        {
            string objectTypeName = _descriptor.ObjectType.ToString();
            switch (objectTypeName)
            {
                case "MTIONSDK_ASSET":
                    _targetMode = VisualScriptingTargetMode.Asset;
                    _targetRoot = _descriptor.ObjectReferenceProp;
                    _targetLabel = _targetRoot != null ? _targetRoot.name : "Asset";
                    break;
                case "MTIONSDK_AVATAR":
                    _targetMode = VisualScriptingTargetMode.Avatar;
                    _targetRoot = _descriptor.ObjectReferenceProp;
                    _targetLabel = _targetRoot != null ? _targetRoot.name : "Avatar";
                    break;
                case "MTIONSDK_ROOM":
                    _targetMode = VisualScriptingTargetMode.Room;
                    _targetScene = _descriptor.gameObject.scene;
                    _targetRoot = _descriptor.ObjectReferenceProp;
                    _targetLabel = _targetScene.IsValid() ? _targetScene.name : "Room";
                    break;
                case "MTIONSDK_BLUEPRINT":
                    ResolveBlueprintRoomTarget(_descriptor as MTIONSDKBlueprint);
                    break;
                default:
                    _targetError = "Unity Visual Scripting setup is only available for assets, avatars, and rooms.";
                    break;
            }
        }

        private static void ResolveBlueprintRoomTarget(MTIONSDKBlueprint blueprint)
        {
            if (blueprint == null)
            {
                _targetError = "This blueprint could not resolve its linked room scene.";
                return;
            }

            _targetMode = VisualScriptingTargetMode.BlueprintRoom;
            _targetLabel = string.IsNullOrWhiteSpace(blueprint.RoomSceneName) ? "Linked Room" : blueprint.RoomSceneName;

            if (!blueprint.TryGetResolvedRoomScene(out _targetScene, out string sceneError))
            {
                _targetError = sceneError ?? "Open the linked room scene to finish UVS setup.";
                return;
            }

            MTIONSDKRoom roomDescriptor = FindRoomDescriptorInScene(_targetScene);
            if (roomDescriptor == null)
            {
                _targetError = "The linked room scene is loaded, but it does not contain an MTIONSDKRoom descriptor.";
                return;
            }

            _targetRoot = roomDescriptor.ObjectReferenceProp;
            _targetLabel = _targetScene.name;
        }

        private static void BuildComponentState()
        {
            if (!string.IsNullOrWhiteSpace(_targetError))
            {
                return;
            }

            AppendComponentRows(CollectMachineComponents());
        }

        private static void RefreshPlacementState()
        {
            _targetHost = null;
            _placementIssue = null;
            EntryPointDefinitions.Clear();
            if (_targetRoot == null)
            {
                return;
            }

            _targetHost = VisualScriptingHostUtility.GetHost(_targetRoot);
            UVSSDKEntryPointRegistry registry = _targetRoot.GetComponentInChildren<UVSSDKEntryPointRegistry>(true);
            if (registry != null)
            {
                EntryPointDefinitions.AddRange(registry.EntryPoints.Where(entryPoint => entryPoint != null));
            }

            List<Component> rootLevelComponents = VisualScriptingHostUtility.GetRootLevelVisualScriptingComponents(_targetRoot);
            if (rootLevelComponents.Count == 0)
            {
                return;
            }

            string componentNames = string.Join(", ", rootLevelComponents
                .Select(component => component.GetType().Name)
                .Distinct()
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
            _placementIssue = $"UVS components are attached to the SDK root. Configure UVS to move {componentNames} into {VisualScriptingHostUtility.HostObjectName}.";
        }

        private static void EnsureInspectionState()
        {
            if (_hasDeepInspection || !string.IsNullOrWhiteSpace(_targetError))
            {
                return;
            }

            BuildInspectionState();
            _hasDeepInspection = true;
        }

        private static void BuildInspectionState()
        {
            if (!string.IsNullOrWhiteSpace(_targetError))
            {
                return;
            }

            _inspectionReport = null;
            ReferencedGraphAssets.Clear();

            switch (_targetMode)
            {
                case VisualScriptingTargetMode.Asset:
                case VisualScriptingTargetMode.Avatar:
                    _inspectionReport = VisualScriptingSupportUtil.InspectGameObjectForExport(_targetRoot, VisualScriptingExportTarget.PortablePrefab);
                    break;
                case VisualScriptingTargetMode.Room:
                case VisualScriptingTargetMode.BlueprintRoom:
                    _inspectionReport = VisualScriptingSupportUtil.InspectSceneForExport(_targetScene, VisualScriptingExportTarget.RoomScene);
                    break;
            }

            if (_inspectionReport == null)
            {
                return;
            }

            HashSet<string> referencedAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string assetPath in _inspectionReport.ReferencedAssetPaths)
            {
                if (!string.IsNullOrWhiteSpace(assetPath) && assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                {
                    referencedAssets.Add(assetPath);
                }
            }

            ReferencedGraphAssets.AddRange(referencedAssets.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
        }

        private static void AppendComponentRows(IEnumerable<Component> components)
        {
            HashSet<int> seenInstanceIds = new HashSet<int>();
            foreach (Component component in components)
            {
                if (component == null || !seenInstanceIds.Add(component.GetInstanceID()))
                {
                    continue;
                }

                string componentTypeName = component.GetType().Name;
                VisualScriptingComponentRow row = new VisualScriptingComponentRow
                {
                    Component = component,
                    ComponentTypeName = componentTypeName,
                    GameObjectPath = BuildGameObjectPath(component.gameObject),
                };

                PopulateMachineState(row);
                ComponentRows.Add(row);

                if (row.IsMachine)
                {
                    _hasMachineComponents = true;
                }
            }

            ComponentRows.Sort((a, b) => string.Compare(a.GameObjectPath, b.GameObjectPath, StringComparison.OrdinalIgnoreCase));
        }

        private static MTIONSDKRoom FindRoomDescriptorInScene(Scene scene)
        {
            if (!scene.IsValid())
            {
                return null;
            }

            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                MTIONSDKRoom roomDescriptor = sceneRoot.GetComponentInChildren<MTIONSDKRoom>(true);
                if (roomDescriptor != null)
                {
                    return roomDescriptor;
                }
            }

            return null;
        }


        private static void DrawStatusBox()
        {
            MTIONSDKToolsWindow.StartBox();
            GUILayout.Label("Unity Visual Scripting", MTIONSDKToolsWindow.BoxHeaderStyle);
            GUILayout.Space(6);

            GUILayout.Label(GetCurrentTargetLabel(), MTIONSDKToolsWindow.ListHeaderStyle);
            GUILayout.Label(GetStatusSummary(), MTIONSDKToolsWindow.LabelStyle);

            string guidance = GetSimpleGuidance();
            if (!string.IsNullOrWhiteSpace(guidance))
            {
                GUILayout.Space(6);
                GUILayout.Label(guidance, MTIONSDKToolsWindow.LabelStyle);
            }

            MTIONSDKToolsWindow.EndBox();
        }

        private static void DrawIssuesBox()
        {
            bool hasIssues = NeedsProjectRepair() ||
                NeedsPlacementNormalization() ||
                !string.IsNullOrWhiteSpace(_targetError) ||
                (_inspectionReport != null && (_inspectionReport.Errors.Count > 0 || _inspectionReport.Warnings.Count > 0));

            if (!hasIssues)
            {
                if (IsConfigured())
                {
                    MTIONSDKToolsWindow.StartBox();
                    GUILayout.Label("Status", MTIONSDKToolsWindow.BoxHeaderStyle);
                    GUILayout.Space(6);
                    GUILayout.Label("UVS is configured and ready.", MTIONSDKToolsWindow.SuccessLabelStyle);
                    MTIONSDKToolsWindow.EndBox();
                }

                return;
            }

            MTIONSDKToolsWindow.StartBox();
            GUILayout.Label("Needs Attention", MTIONSDKToolsWindow.BoxHeaderStyle);
            GUILayout.Space(6);

            if (NeedsProjectRepair())
            {
                MTIONSDKToolsWindow.DrawWarning(
                    $"Generated UVS project data references missing types: {string.Join(", ", _projectAudit.MissingTypeNames)}.",
                    MTIONSDKToolsWindow.WarningType.ERROR);
            }

            if (NeedsPlacementNormalization())
            {
                MTIONSDKToolsWindow.DrawWarning(_placementIssue, MTIONSDKToolsWindow.WarningType.ERROR);
            }

            if (!string.IsNullOrWhiteSpace(_targetError))
            {
                MTIONSDKToolsWindow.DrawWarning(_targetError, MTIONSDKToolsWindow.WarningType.ERROR);
            }

            if (_inspectionReport != null)
            {
                foreach (string error in _inspectionReport.Errors)
                {
                    MTIONSDKToolsWindow.DrawWarning(error, MTIONSDKToolsWindow.WarningType.ERROR);
                }

                foreach (string warning in _inspectionReport.Warnings)
                {
                    MTIONSDKToolsWindow.DrawWarning(warning);
                }
            }

            MTIONSDKToolsWindow.EndBox();
        }

        private static void DrawActionBox()
        {
            if (!TryGetPrimaryAction(out string actionLabel, out string actionDescription))
            {
                return;
            }

            MTIONSDKToolsWindow.StartBox();
            GUILayout.Label("Next Step", MTIONSDKToolsWindow.BoxHeaderStyle);
            GUILayout.Space(6);
            GUILayout.Label(actionDescription, MTIONSDKToolsWindow.LabelStyle);
            GUILayout.Space(10);

            if (GUILayout.Button(actionLabel, MTIONSDKToolsWindow.LargeButtonStyle))
            {
                ExecutePrimaryAction(actionLabel);
            }

            MTIONSDKToolsWindow.EndBox();
        }

        private static void DrawAdvancedBox()
        {
            if (!HasAdvancedDetails())
            {
                return;
            }

            MTIONSDKToolsWindow.StartBox();
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced", true, MTIONSDKToolsWindow.FoldoutStyle);
            if (_showAdvanced)
            {
                EnsureInspectionState();
                GUILayout.Space(8);
                GUILayout.Label($"Scopes: {GetScopeSummary()}", MTIONSDKToolsWindow.LabelStyle);
                GUILayout.Label($"Detected UVS components: {ComponentRows.Count}", MTIONSDKToolsWindow.LabelStyle);
                GUILayout.Label($"SDK entry points: {EntryPointDefinitions.Count}", MTIONSDKToolsWindow.LabelStyle);

                GUILayout.Space(10);
                DrawComponentRows();

                if (EntryPointDefinitions.Count > 0)
                {
                    GUILayout.Space(10);
                    DrawEntryPoints();
                }

                if (ReferencedGraphAssets.Count > 0)
                {
                    GUILayout.Space(10);
                    DrawReferencedAssets();
                }
            }

            MTIONSDKToolsWindow.EndBox();
        }

        private static void DrawComponentRows()
        {
            GUILayout.Label("Detected Graph Components", MTIONSDKToolsWindow.ListHeaderStyle);
            if (ComponentRows.Count == 0)
            {
                GUILayout.Label("No ScriptMachine or StateMachine components detected on this target.", MTIONSDKToolsWindow.LabelStyle);
                return;
            }

            foreach (VisualScriptingComponentRow row in ComponentRows)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{row.GameObjectPath} ({row.ComponentTypeName})", MTIONSDKToolsWindow.LabelStyle);
                GUILayout.FlexibleSpace();

                if (row.IsMachine)
                {
                    string graphButtonLabel = row.HasGraphAsset ? "Open Graph" : "Create Graph";
                    if (GUILayout.Button(graphButtonLabel, MTIONSDKToolsWindow.SmallButtonStyle, GUILayout.Width(100)))
                    {
                        HandleGraphAction(row);
                    }

                    GUILayout.Space(6);
                }

                if (GUILayout.Button("Ping", MTIONSDKToolsWindow.SmallButtonStyle, GUILayout.Width(60)))
                {
                    Selection.activeObject = row.Component;
                    EditorGUIUtility.PingObject(row.Component);
                }
                GUILayout.EndHorizontal();
            }
        }

        private static void DrawReferencedAssets()
        {
            GUILayout.Label("Referenced Graph Assets", MTIONSDKToolsWindow.ListHeaderStyle);
            foreach (string assetPath in ReferencedGraphAssets)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(assetPath, MTIONSDKToolsWindow.LabelStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Ping", MTIONSDKToolsWindow.SmallButtonStyle, GUILayout.Width(60)))
                {
                    Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                    if (asset != null)
                    {
                        Selection.activeObject = asset;
                        EditorGUIUtility.PingObject(asset);
                    }
                }
                GUILayout.EndHorizontal();
            }
        }

        private static void DrawEntryPoints()
        {
            GUILayout.Label("SDK Entry Points", MTIONSDKToolsWindow.ListHeaderStyle);
            for (int i = 0; i < EntryPointDefinitions.Count; i++)
            {
                UVSSDKEntryPointDefinition entryPoint = EntryPointDefinitions[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{entryPoint.DisplayName} ({entryPoint.EntryPointId})", MTIONSDKToolsWindow.LabelStyle);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
        }


        private static void ConfigureUvs()
        {
            try
            {
                bool projectDataChanged = false;
                if (NeedsProjectRepair())
                {
                    VisualScriptingProjectPreflight.EnsureGeneratedDataIsHealthy(true);
                    projectDataChanged = true;
                }

                if (VisualScriptingProjectPreflight.EnsureSdkEntryPointNodeLibraryIsReady(true))
                {
                    projectDataChanged = true;
                }

                if (projectDataChanged)
                {
                    Refresh();
                }

                if (NeedsRoomSceneOpen())
                {
                    OpenLinkedRoomScene();
                }

                if (_targetRoot != null)
                {
                    bool shouldCreateHost = NeedsPlacementNormalization() || !_hasMachineComponents;
                    if (!VisualScriptingHostUtility.NormalizePlacement(_targetRoot, shouldCreateHost, out _, out _, out string normalizeError))
                    {
                        throw new InvalidOperationException(normalizeError);
                    }

                    if (!VisualScriptingReflectionUtility.SyncEntryPointRegistryFromVisualScripting(_targetRoot, out _, out List<string> syncErrors))
                    {
                        throw new InvalidOperationException(string.Join("\n", syncErrors));
                    }
                }

                Refresh();
                EnsureInspectionState();

                if (!string.IsNullOrWhiteSpace(_targetError))
                {
                    throw new InvalidOperationException(_targetError);
                }

                if (HasBlockingIssues())
                {
                    throw new InvalidOperationException(string.Join("\n", _inspectionReport.Errors));
                }

                if (ShouldAddDefaultScriptMachine())
                {
                    GameObject defaultTarget = GetPrimaryVisualScriptingHost(true);
                    if (defaultTarget == null)
                    {
                        throw new InvalidOperationException("The current UVS target could not be resolved.");
                    }

                    if (!TryAddMachineComponent(defaultTarget, "Unity.VisualScripting.ScriptMachine", out Component newComponent, out string addError))
                    {
                        throw new InvalidOperationException(addError);
                    }

                    Selection.activeObject = newComponent;
                    EditorGUIUtility.PingObject(newComponent);
                }

                Refresh();
            }
            catch (Exception ex)
            {
                Refresh();
                EditorUtility.DisplayDialog("UVS Setup Failed", ex.Message, "Close");
            }
        }

        private static void ExecutePrimaryAction(string actionLabel)
        {
            if ((actionLabel == "Open Graph" || actionLabel == "Create Graph") &&
                TryGetSingleMachineRow(out VisualScriptingComponentRow machineRow))
            {
                HandleGraphAction(machineRow);
                return;
            }

            ConfigureUvs();
        }

        private static bool TryAddMachineComponent(GameObject targetGameObject, string machineTypeName, out Component component, out string error)
        {
            component = null;
            error = null;

            if (targetGameObject == null)
            {
                error = "The current UVS target is missing.";
                return false;
            }

            Type machineType = FindType(machineTypeName);
            if (machineType == null)
            {
                error = $"Could not resolve Unity Visual Scripting type '{machineTypeName}'. Make sure com.unity.visualscripting is installed and compiled.";
                return false;
            }

            if (targetGameObject.GetComponent(machineType) != null)
            {
                error = $"{targetGameObject.name} already has a {machineType.Name} component.";
                return false;
            }

            component = Undo.AddComponent(targetGameObject, machineType);
            EditorUtility.SetDirty(targetGameObject);
            EditorSceneManager.MarkAllScenesDirty();
            return component != null;
        }

        private static void OpenLinkedRoomScene()
        {
            MTIONSDKBlueprint blueprint = _descriptor as MTIONSDKBlueprint;
            if (blueprint == null)
            {
                return;
            }

            if (!blueprint.TryResolveRoomScenePath(out string roomScenePath, out string roomSceneError))
            {
                throw new InvalidOperationException(roomSceneError ?? "The blueprint room scene could not be resolved.");
            }

            Scene roomScene = EditorSceneManager.GetSceneByPath(roomScenePath);
            if (!roomScene.IsValid() || !roomScene.isLoaded)
            {
                roomScene = EditorSceneManager.OpenScene(roomScenePath, OpenSceneMode.Additive);
            }

            EditorSceneManager.SetActiveScene(roomScene);
        }


        private static bool TryGetPrimaryAction(out string actionLabel, out string actionDescription)
        {
            actionLabel = null;
            actionDescription = null;

            if (HasBlockingIssues())
            {
                return false;
            }

            if (NeedsProjectRepair())
            {
                actionLabel = "Configure UVS";
                actionDescription = "Clean stale Unity Visual Scripting project data and prepare this target for UVS.";
                return true;
            }

            if (NeedsPlacementNormalization())
            {
                actionLabel = "Configure UVS";
                actionDescription = $"Move UVS components off the SDK root and into {VisualScriptingHostUtility.HostObjectName}.";
                return true;
            }

            if (NeedsRoomSceneOpen())
            {
                actionLabel = "Configure UVS";
                actionDescription = "Open the linked room scene and finish room UVS setup.";
                return true;
            }

            if (ShouldAddDefaultScriptMachine())
            {
                actionLabel = "Configure UVS";
                actionDescription = "Add a default ScriptMachine so you can start authoring UVS on this target immediately.";
                return true;
            }

            if (TryGetSingleMachineRow(out VisualScriptingComponentRow machineRow))
            {
                actionLabel = machineRow.HasGraphAsset ? "Open Graph" : "Create Graph";
                actionDescription = machineRow.HasGraphAsset
                    ? "Open the current graph so you can edit this target's UVS behavior."
                    : "Create a graph asset for this target so it is easy to reopen and edit.";
                return true;
            }

            return false;
        }

        private static bool NeedsProjectRepair()
        {
            return _projectAudit != null && !_projectAudit.IsHealthy;
        }

        private static bool NeedsPlacementNormalization()
        {
            return !string.IsNullOrWhiteSpace(_placementIssue);
        }

        private static bool NeedsRoomSceneOpen()
        {
            if (_targetMode != VisualScriptingTargetMode.BlueprintRoom || string.IsNullOrWhiteSpace(_targetError))
            {
                return false;
            }

            MTIONSDKBlueprint blueprint = _descriptor as MTIONSDKBlueprint;
            return blueprint != null && blueprint.TryResolveRoomScenePath(out _, out _);
        }

        private static bool HasBlockingIssues()
        {
            return _inspectionReport != null && _inspectionReport.Errors.Count > 0;
        }

        private static bool ShouldAddDefaultScriptMachine()
        {
            return string.IsNullOrWhiteSpace(_targetError) &&
                !NeedsPlacementNormalization() &&
                !HasBlockingIssues() &&
                !_hasMachineComponents;
        }

        private static bool IsConfigured()
        {
            return !NeedsProjectRepair() &&
                !NeedsPlacementNormalization() &&
                string.IsNullOrWhiteSpace(_targetError) &&
                !HasBlockingIssues() &&
                _hasMachineComponents;
        }

        private static bool HasAdvancedDetails()
        {
            return _inspectionReport != null || ComponentRows.Count > 0 || ReferencedGraphAssets.Count > 0;
        }


        private static string GetCurrentTargetLabel()
        {
            switch (_targetMode)
            {
                case VisualScriptingTargetMode.Asset:
                    return $"Current Asset: {_targetLabel}";
                case VisualScriptingTargetMode.Avatar:
                    return $"Current Avatar: {_targetLabel}";
                case VisualScriptingTargetMode.Room:
                    return $"Current Room: {_targetLabel}";
                case VisualScriptingTargetMode.BlueprintRoom:
                    return $"Current Room: {_targetLabel}";
                default:
                    return "Current Target";
            }
        }

        private static string GetStatusSummary()
        {
            if (NeedsProjectRepair())
            {
                return "This project has stale UVS setup data that needs repair.";
            }

            if (NeedsPlacementNormalization())
            {
                return "UVS components need to be moved off the SDK root into a child container.";
            }

            if (NeedsRoomSceneOpen())
            {
                return "Open the linked room scene to finish UVS setup.";
            }

            if (!string.IsNullOrWhiteSpace(_targetError))
            {
                return "This target is not ready for UVS setup yet.";
            }

            if (HasBlockingIssues())
            {
                return "This target has UVS issues that need manual fixes before export.";
            }

            if (_hasMachineComponents)
            {
                if (GetMachineRowCount() > 1)
                {
                    return "This target is configured. Expand Advanced to choose which graph to edit.";
                }

                return "This target is configured and ready for UVS.";
            }

            return "This target is ready for UVS setup.";
        }

        private static string GetSimpleGuidance()
        {
            switch (_targetMode)
            {
                case VisualScriptingTargetMode.Asset:
                case VisualScriptingTargetMode.Avatar:
                    return "Portable UVS should stay self-contained and use Object or Graph variables.";
                case VisualScriptingTargetMode.Room:
                case VisualScriptingTargetMode.BlueprintRoom:
                    return "Room UVS can use Scene variables for room-local logic and interactions.";
                default:
                    return null;
            }
        }

        private static string GetScopeSummary()
        {
            if (!_hasDeepInspection)
            {
                return "Expand Advanced to inspect scopes and referenced graph assets.";
            }

            if (_inspectionReport == null || _inspectionReport.ScopeKinds.Count == 0)
            {
                return "No explicit UVS variable scopes detected yet.";
            }

            return string.Join(", ", _inspectionReport.GetOrderedScopes());
        }


        private static GameObject GetPrimaryVisualScriptingHost(bool createIfMissing)
        {
            if (_targetRoot == null)
            {
                return null;
            }

            if (createIfMissing)
            {
                _targetHost = VisualScriptingHostUtility.GetOrCreateHost(_targetRoot);
            }
            else if (_targetHost == null)
            {
                _targetHost = VisualScriptingHostUtility.GetHost(_targetRoot);
            }

            return _targetHost;
        }

        private static List<Component> CollectMachineComponents()
        {
            List<Component> components = new List<Component>();
            GameObject defaultTarget = _targetRoot;
            Type scriptMachineType = FindType("Unity.VisualScripting.ScriptMachine");
            Type stateMachineType = FindType("Unity.VisualScripting.StateMachine");

            AddComponentsOfType(components, defaultTarget, scriptMachineType);
            AddComponentsOfType(components, defaultTarget, stateMachineType);

            if ((_targetMode == VisualScriptingTargetMode.Room || _targetMode == VisualScriptingTargetMode.BlueprintRoom) &&
                _targetScene.IsValid())
            {
                GameObject[] roots = _targetScene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    if (roots[i] == defaultTarget)
                    {
                        continue;
                    }

                    AddComponentsOfType(components, roots[i], scriptMachineType);
                    AddComponentsOfType(components, roots[i], stateMachineType);
                }
            }

            return components;
        }

        private static void AddComponentsOfType(List<Component> components, GameObject root, Type componentType)
        {
            if (components == null || root == null || componentType == null)
            {
                return;
            }

            Component[] foundComponents = root.GetComponentsInChildren(componentType, true);
            for (int i = 0; i < foundComponents.Length; i++)
            {
                if (foundComponents[i] != null)
                {
                    components.Add(foundComponents[i]);
                }
            }
        }

        private static void PopulateMachineState(VisualScriptingComponentRow row)
        {
            row.IsMachine = IsMachineComponent(row.Component);
            if (!row.IsMachine)
            {
                return;
            }

            TryGetMachineGraphAsset(row.Component, out Object graphAsset, out _);
            row.GraphAsset = graphAsset;
            row.HasGraphAsset = graphAsset != null;
        }

        private static bool IsMachineComponent(Component component)
        {
            return component != null &&
                (component.GetType().Name == "ScriptMachine" || component.GetType().Name == "StateMachine");
        }

        private static int GetMachineRowCount()
        {
            int count = 0;
            for (int i = 0; i < ComponentRows.Count; i++)
            {
                if (ComponentRows[i].IsMachine)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool TryGetSingleMachineRow(out VisualScriptingComponentRow machineRow)
        {
            machineRow = null;
            for (int i = 0; i < ComponentRows.Count; i++)
            {
                VisualScriptingComponentRow row = ComponentRows[i];
                if (!row.IsMachine)
                {
                    continue;
                }

                if (machineRow != null)
                {
                    machineRow = null;
                    return false;
                }

                machineRow = row;
            }

            return machineRow != null;
        }

        private static void HandleGraphAction(VisualScriptingComponentRow row)
        {
            try
            {
                if (row == null || row.Component == null)
                {
                    throw new InvalidOperationException("The selected UVS machine could not be resolved.");
                }

                if (NeedsProjectRepair())
                {
                    throw new InvalidOperationException("Repair the Unity Visual Scripting project setup first by using Configure UVS.");
                }

                if (row.HasGraphAsset)
                {
                    if (!TryOpenMachineGraph(row.Component, out string openError))
                    {
                        if (!TryOpenGraphAssetDirectly(row.GraphAsset))
                        {
                            throw new InvalidOperationException(openError);
                        }
                    }
                }
                else
                {
                    if (!TryCreateGraphAssetForMachine(row.Component, out Object graphAsset, out string createError))
                    {
                        throw new InvalidOperationException(createError);
                    }

                    if (graphAsset != null)
                    {
                        Selection.activeObject = graphAsset;
                        EditorGUIUtility.PingObject(graphAsset);
                    }
                }

                Refresh();
            }
            catch (Exception ex)
            {
                Refresh();
                EditorUtility.DisplayDialog("UVS Graph Action Failed", ex.Message, "Close");
            }
        }

        private static bool TryOpenMachineGraph(Component machineComponent, out string error)
        {
            error = null;
            if (machineComponent == null)
            {
                error = "The UVS machine is missing.";
                return false;
            }

            Type graphReferenceType = FindType("Unity.VisualScripting.GraphReference");
            Type graphWindowType = FindType("Unity.VisualScripting.GraphWindow");
            if (graphReferenceType == null || graphWindowType == null)
            {
                error = "Could not locate Unity Visual Scripting editor types needed to open the graph.";
                return false;
            }

            MethodInfo newReferenceMethod = graphReferenceType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method => method.Name == "New" && method.GetParameters().Length == 2);
            MethodInfo openActiveMethod = graphWindowType.GetMethod("OpenActive", BindingFlags.Public | BindingFlags.Static);
            if (newReferenceMethod == null || openActiveMethod == null)
            {
                error = "Could not locate the Unity Visual Scripting graph window API.";
                return false;
            }

            object graphReference = newReferenceMethod.Invoke(null, new object[] { machineComponent, false });
            openActiveMethod.Invoke(null, new[] { graphReference });
            return true;
        }

        private static bool TryCreateGraphAssetForMachine(Component machineComponent, out Object graphAsset, out string error)
        {
            graphAsset = null;
            error = null;

            if (machineComponent == null)
            {
                error = "The UVS machine is missing.";
                return false;
            }

            string machineTypeName = machineComponent.GetType().Name;
            string graphAssetTypeName;
            string graphTypeName;
            string defaultGraphMethodName;

            switch (machineTypeName)
            {
                case "ScriptMachine":
                    graphAssetTypeName = "Unity.VisualScripting.ScriptGraphAsset";
                    graphTypeName = "Unity.VisualScripting.FlowGraph";
                    defaultGraphMethodName = "WithStartUpdate";
                    break;
                case "StateMachine":
                    graphAssetTypeName = "Unity.VisualScripting.StateGraphAsset";
                    graphTypeName = "Unity.VisualScripting.StateGraph";
                    defaultGraphMethodName = "WithStart";
                    break;
                default:
                    error = $"Unsupported UVS machine type '{machineTypeName}'.";
                    return false;
            }

            Type graphAssetType = FindType(graphAssetTypeName);
            Type graphType = FindType(graphTypeName);
            if (graphAssetType == null || graphType == null)
            {
                error = "Could not locate Unity Visual Scripting graph asset types.";
                return false;
            }

            ScriptableObject graphAssetInstance = ScriptableObject.CreateInstance(graphAssetType);
            PropertyInfo graphProperty = graphAssetType.GetProperty("graph", BindingFlags.Public | BindingFlags.Instance);
            if (graphProperty == null || !graphProperty.CanWrite)
            {
                error = $"Could not configure graph data on '{graphAssetTypeName}'.";
                return false;
            }

            object defaultGraph = CreateDefaultGraph(graphType, defaultGraphMethodName);
            graphProperty.SetValue(graphAssetInstance, defaultGraph);

            string graphAssetPath = GenerateGraphAssetPath(GetGraphAssetTargetName(machineComponent), machineTypeName);
            if (string.IsNullOrWhiteSpace(graphAssetPath))
            {
                error = "Could not determine where to create the UVS graph asset.";
                return false;
            }

            AssetDatabase.CreateAsset(graphAssetInstance, graphAssetPath);
            AssetDatabase.SaveAssets();

            if (!TryAssignGraphAssetToMachine(machineComponent, graphAssetInstance, out error))
            {
                AssetDatabase.DeleteAsset(graphAssetPath);
                return false;
            }

            graphAsset = graphAssetInstance;
            EditorUtility.SetDirty(machineComponent);
            EditorUtility.SetDirty(graphAssetInstance);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkAllScenesDirty();

            if (!TryOpenMachineGraph(machineComponent, out string openError))
            {
                if (!TryOpenGraphAssetDirectly(graphAssetInstance))
                {
                    error = openError;
                    return false;
                }
            }

            return true;
        }

        private static bool TryAssignGraphAssetToMachine(Component machineComponent, Object graphAsset, out string error)
        {
            error = null;
            object nest = machineComponent.GetType().GetProperty("nest", BindingFlags.Public | BindingFlags.Instance)?.GetValue(machineComponent);
            if (nest == null)
            {
                error = "Could not access the UVS machine graph nest.";
                return false;
            }

            MethodInfo switchToMacroMethod = nest.GetType().GetMethod("SwitchToMacro", BindingFlags.Public | BindingFlags.Instance);
            if (switchToMacroMethod == null)
            {
                error = "Could not switch the UVS machine to use a graph asset.";
                return false;
            }

            switchToMacroMethod.Invoke(nest, new[] { graphAsset });
            return true;
        }

        private static bool TryGetMachineGraphAsset(Component machineComponent, out Object graphAsset, out string error)
        {
            graphAsset = null;
            error = null;
            if (machineComponent == null)
            {
                error = "The UVS machine is missing.";
                return false;
            }

            object nest = machineComponent.GetType().GetProperty("nest", BindingFlags.Public | BindingFlags.Instance)?.GetValue(machineComponent);
            if (nest == null)
            {
                error = "Could not inspect the UVS machine graph nest.";
                return false;
            }

            object sourceValue = nest.GetType().GetProperty("source", BindingFlags.Public | BindingFlags.Instance)?.GetValue(nest);
            string sourceName = sourceValue?.ToString();
            if (!string.Equals(sourceName, "Macro", StringComparison.Ordinal))
            {
                return true;
            }

            graphAsset = nest.GetType().GetProperty("macro", BindingFlags.Public | BindingFlags.Instance)?.GetValue(nest) as Object;
            return true;
        }

        private static object CreateDefaultGraph(Type graphType, string defaultMethodName)
        {
            MethodInfo defaultGraphMethod = graphType.GetMethod(defaultMethodName, BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (defaultGraphMethod != null)
            {
                return defaultGraphMethod.Invoke(null, null);
            }

            return Activator.CreateInstance(graphType);
        }

        private static bool TryOpenGraphAssetDirectly(Object graphAsset)
        {
            return graphAsset != null && AssetDatabase.OpenAsset(graphAsset);
        }

        private static string GenerateGraphAssetPath(string targetName, string machineTypeName)
        {
            string rootDirectory = GetGraphAssetRootDirectory();
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                return null;
            }

            EnsureFolderExists(rootDirectory);

            string sanitizedName = SanitizeFileName(string.IsNullOrWhiteSpace(targetName) ? "UVSGraph" : targetName);
            string assetName = $"{sanitizedName}_{machineTypeName}.asset";
            string desiredPath = Path.Combine(rootDirectory, assetName).Replace('\\', '/');
            return AssetDatabase.GenerateUniqueAssetPath(desiredPath);
        }

        private static string GetGraphAssetTargetName(Component machineComponent)
        {
            if (machineComponent != null &&
                machineComponent.gameObject != null &&
                machineComponent.gameObject.name != VisualScriptingHostUtility.HostObjectName)
            {
                return machineComponent.gameObject.name;
            }

            if (_targetRoot != null)
            {
                return _targetRoot.name;
            }

            return machineComponent != null && machineComponent.gameObject != null
                ? machineComponent.gameObject.name
                : "UVSGraph";
        }

        private static string GetGraphAssetRootDirectory()
        {
            if ((_targetMode == VisualScriptingTargetMode.Room || _targetMode == VisualScriptingTargetMode.BlueprintRoom) &&
                _targetScene.IsValid() &&
                !string.IsNullOrWhiteSpace(_targetScene.path))
            {
                return CombineAssetPath(Path.GetDirectoryName(_targetScene.path), "VisualScripting");
            }

            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid() && !string.IsNullOrWhiteSpace(activeScene.path))
            {
                return CombineAssetPath(Path.GetDirectoryName(activeScene.path), "VisualScripting");
            }

            return "Assets/VisualScripting";
        }

        private static void EnsureFolderExists(string assetFolderPath)
        {
            string normalizedPath = assetFolderPath.Replace('\\', '/');
            string[] parts = normalizedPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                return;
            }

            string currentPath = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string nextPath = $"{currentPath}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, parts[i]);
                }

                currentPath = nextPath;
            }
        }

        private static string CombineAssetPath(string left, string right)
        {
            string normalizedLeft = string.IsNullOrWhiteSpace(left) ? "Assets" : left.Replace('\\', '/');
            string normalizedRight = string.IsNullOrWhiteSpace(right) ? string.Empty : right.Replace('\\', '/');
            return string.IsNullOrWhiteSpace(normalizedRight)
                ? normalizedLeft
                : $"{normalizedLeft.TrimEnd('/')}/{normalizedRight.TrimStart('/')}";
        }

        private static string SanitizeFileName(string fileName)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            char[] output = fileName.ToCharArray();
            for (int i = 0; i < output.Length; i++)
            {
                if (invalidChars.Contains(output[i]))
                {
                    output[i] = '_';
                }
            }

            return new string(output);
        }

        private static GameObject GetSceneDescriptorObject()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return null;
            }

            GameObject fallback = null;
            foreach (GameObject sceneRoot in activeScene.GetRootGameObjects())
            {
                MTIONSDKDescriptorSceneBase descriptor = sceneRoot.GetComponent<MTIONSDKDescriptorSceneBase>();
                if (descriptor == null)
                {
                    continue;
                }

                if (descriptor.ObjectType.ToString() == "MTIONSDK_BLUEPRINT")
                {
                    return sceneRoot;
                }

                if (fallback == null)
                {
                    fallback = sceneRoot;
                }
            }

            return fallback;
        }

        private static string BuildGameObjectPath(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return string.Empty;
            }

            Stack<string> names = new Stack<string>();
            Transform current = gameObject.transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names.ToArray());
        }

        private static Type FindType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            if (TypeCache.TryGetValue(typeName, out Type cachedType))
            {
                return cachedType;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(typeName, false);
                if (type != null)
                {
                    TypeCache[typeName] = type;
                    return type;
                }
            }

            TypeCache[typeName] = null;
            return null;
        }
    }
}
