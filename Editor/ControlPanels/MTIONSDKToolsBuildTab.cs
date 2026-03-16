using System;
using System.Collections.Generic;
using System.Linq;
using mtion.room.sdk.action;
using mtion.room.sdk.compiled;
using mtion.room.sdk.customproperties;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

namespace mtion.room.sdk
{
    public static class MTIONSDKToolsBuildTab
    {
        private struct BuildValidationMessage
        {
            public string Text;
            public MTIONSDKToolsWindow.WarningType Type;
        }

        private struct CustomPropertyRow
        {
            public string PropertyName;
            public string Type;
            public string AssetName;
            public string ComponentName;
            public string DefaultValue;
            public string MinValue;
            public string MaxValue;
        }

        public static ReorderableList cameraViewHints = null;
        public static List<MVirtualCameraEventTracker> virtualcameraEvents = new List<MVirtualCameraEventTracker>();

        public static ReorderableList displayComponentRList = null;
        public static List<MVirtualDisplayTracker> displayComponents = new List<MVirtualDisplayTracker>();

        public static List<CustomPropertiesContainer> allCustomPropertiesContainers = new List<CustomPropertiesContainer>();

        private static bool _buildErrorsExist;
        private static bool _buildStateDirty = true;
        private static Vector2 _scrollPos;
        private static bool _roomSceneExists = true;
        private static bool _environmentSceneExists = true;
        private static bool _navMeshExists;
        private static string _roomSceneResolutionError;
        private static string _environmentSceneResolutionError;
        private static string _descriptorValidationError;
        private static string _roomSceneName;
        private static string _environmentSceneName;
        private static readonly List<BuildValidationMessage> _validationMessages = new List<BuildValidationMessage>();
        private static readonly List<CustomPropertyRow> _customPropertyRows = new List<CustomPropertyRow>();
        private static readonly Dictionary<string, bool> _sceneAssetExistenceCache = new Dictionary<string, bool>();
        private static GUIStyle _headerLabelStyle;
        private static GUIStyle _textFieldStyle;
        private static GUIStyle _sizelessTextFieldStyle;
        private static GUIStyle _textFieldLabelStyle;
        private static GUIStyle _popupStyle;
        private static GUIStyle _toggleStyle;
        private static GUIStyle _customPropertyHeaderStyle;
        private static GUIStyle _customPropertyEntryStyle;
        private static GUIStyle _customPropertyTableHeaderStyle;
        private static GUISkin _cachedSkin;


        public static void Invalidate()
        {
            _buildStateDirty = true;
            _sceneAssetExistenceCache.Clear();
        }

        public static void Refresh()
        {
            _buildStateDirty = false;
            allCustomPropertiesContainers.Clear();
            _customPropertyRows.Clear();
            _validationMessages.Clear();
            _buildErrorsExist = false;

            var blueprintSDKObject = GameObject.FindObjectOfType<MTIONSDKBlueprint>();
            _roomSceneName = blueprintSDKObject?.RoomSceneName;
            _environmentSceneName = blueprintSDKObject?.EnvironmentSceneName;
            _roomSceneExists = blueprintSDKObject != null && blueprintSDKObject.TryResolveRoomScenePath(out _, out _roomSceneResolutionError);
            _environmentSceneExists = blueprintSDKObject != null && blueprintSDKObject.TryResolveEnvironmentScenePath(out _, out _environmentSceneResolutionError);
            if (blueprintSDKObject == null)
            {
                _roomSceneResolutionError = null;
                _environmentSceneResolutionError = null;
            }
            _descriptorValidationError = BuildManager.GetSceneDescriptorValidationError();

            var descriptor = BuildManager.GetSceneDescriptor()?.GetComponent<MTIONSDKDescriptorSceneBase>();
            _navMeshExists = descriptor != null &&
                (descriptor.ObjectType == MTIONObjectType.MTIONSDK_BLUEPRINT ||
                 descriptor.ObjectType == MTIONObjectType.MTIONSDK_ROOM ||
                 descriptor.ObjectType == MTIONObjectType.MTIONSDK_ENVIRONMENT) &&
                NavMesh.SamplePosition(Vector3.zero, out _, 1000f, ~0);

            var _sceneDescriptorObject = GameObject.FindObjectOfType<MTIONSDKRoom>();
            if (_sceneDescriptorObject != null)
            {
                SceneVerificationUtil.VerifySceneIntegrity(_sceneDescriptorObject);
                ComponentVerificationUtil.VerifyAllComponentsIntegrity(_sceneDescriptorObject,
                        MTIONObjectType.MTIONSDK_ASSET, false, _sceneDescriptorObject.gameObject.scene);

                var propContainers = GameObject.FindObjectsOfType<CustomPropertiesContainer>();
                foreach (var propContainer in propContainers)
                {
                    var properties = propContainer.GetAllProperties();
                    if (properties.Count == 0)
                    {
                        continue;
                    }

                    allCustomPropertiesContainers.Add(propContainer);

                    foreach (var customProp in properties)
                    {
                        var declaringType = Type.GetType(customProp.DeclaringTypeName);
                        _customPropertyRows.Add(new CustomPropertyRow
                        {
                            PropertyName = customProp.PropertyName,
                            Type = GetCustomPropertyTypeName(customProp),
                            AssetName = propContainer.gameObject.name,
                            ComponentName = declaringType?.Name ?? customProp.DeclaringTypeName,
                            DefaultValue = GetCustomPropertyDefaultValue(customProp),
                            MinValue = GetCustomPropertyMinValue(customProp),
                            MaxValue = GetCustomPropertyMaxValue(customProp)
                        });
                    }
                }
            }

            if (BuildManager.IsSceneValid())
            {
                GenerateWarningsAndErrors();
            }
        }

        public static void Draw()
        {
            EnsureStylesInitialized();
            EnsureBuildStateIsCurrent();

            using (var scrollView = new EditorGUILayout.ScrollViewScope(_scrollPos))
            {
                _scrollPos = scrollView.scrollPosition;
                DrawBuildOptions();
            }
        }

        private static void DrawBuildOptions()
        {
            if (!BuildManager.IsSceneValid())
            {
                CreateRoomInstantiationOptions();
            }
            else
            {
                var descriptorGO = BuildManager.GetSceneDescriptor();
                var descriptor = descriptorGO.GetComponent<MTIONSDKDescriptorSceneBase>();

                MTIONSDKToolsWindow.StartBox();
                {
                    GUILayout.Label("Settings", _headerLabelStyle);
                    GUILayout.Space(10);

                    bool changesMade = false;

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent("Show grid", "Enables grid visualization in the scene"), _textFieldLabelStyle);
                    bool showGrid = GUILayout.Toggle(descriptor.ShowGrid, "", _toggleStyle);
                    if (showGrid != descriptor.ShowGrid)
                    {
                        descriptor.ShowGrid = showGrid;
                        changesMade = true;
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent("Grid Size", "Adjusts the size of the grid"), _textFieldLabelStyle);
                    float gridSize = GUILayout.HorizontalSlider(descriptor.GridSize, 2f, 50f, GUILayout.Width(450));
                    if (gridSize != descriptor.GridSize)
                    {
                        descriptor.GridSize = gridSize;
                        changesMade = true;
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent("Scale Reference", "Places an object in the scene that can be used to reference asset size"), _textFieldLabelStyle);
                    SDKScaleReference scaleReference =
                        (SDKScaleReference)EditorGUILayout.EnumPopup("", descriptor.ScaleReference, _popupStyle);
                    if (scaleReference != descriptor.ScaleReference)
                    {
                        descriptor.ScaleReference = scaleReference;
                        changesMade = true;
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent("Reference Position", "Adjusts the location of the reference object"), _textFieldLabelStyle);

                    EditorGUIUtility.labelWidth = 10;
                    float xValue = EditorGUILayout.FloatField("X", descriptor.ScaleReferencePosition.x, _sizelessTextFieldStyle, GUILayout.Height(22),
                        GUILayout.MaxWidth(148));
                    float yValue = EditorGUILayout.FloatField("Y", descriptor.ScaleReferencePosition.y, _sizelessTextFieldStyle, GUILayout.Height(22),
                        GUILayout.MaxWidth(148));
                    float zValue = EditorGUILayout.FloatField("Z", descriptor.ScaleReferencePosition.z, _sizelessTextFieldStyle, GUILayout.Height(22),
                        GUILayout.MaxWidth(148));
                    EditorGUIUtility.labelWidth = 0;
                    Vector3 scaleReferencePosition = new Vector3(xValue, yValue, zValue);
                    if (scaleReferencePosition != descriptor.ScaleReferencePosition)
                    {
                        descriptor.ScaleReferencePosition = scaleReferencePosition;
                        changesMade = true;
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);
                }
                MTIONSDKToolsWindow.EndBox();


                bool shouldStopAfterRoomScenePrompt = false;
                MTIONSDKToolsWindow.StartBox();
                {
                    var blueprintSDKObject = GameObject.FindObjectOfType<MTIONSDKBlueprint>();
                    if (blueprintSDKObject != null)
                    {
                        if (!_roomSceneExists)
                        {
                            EditorGUILayout.HelpBox(string.IsNullOrWhiteSpace(_roomSceneResolutionError)
                                ? "Room scene not found. Please drag and drop the scene file."
                                : _roomSceneResolutionError,
                                MessageType.Warning);
                            SceneAsset newSceneAsset = EditorGUILayout.ObjectField("Room Scene", null, typeof(SceneAsset), false) as SceneAsset;
                            if (newSceneAsset != null)
                            {
                                blueprintSDKObject.SetRoomSceneReference(newSceneAsset);
                                Refresh();
                            }

                            shouldStopAfterRoomScenePrompt = true;
                        }
                    }
                }
                MTIONSDKToolsWindow.EndBox();

                if (shouldStopAfterRoomScenePrompt)
                {
                    return;
                }


                MTIONSDKToolsWindow.StartBox();
                {
                    GUILayout.Label("Options", _headerLabelStyle);
                    GUILayout.Space(10);

                    bool changesMade = false;

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent("Name", "Name of the asset, used in mxm studio"), _textFieldLabelStyle);
                    var name = descriptor.Name;
                    descriptor.Name = GUILayout.TextField(descriptor.Name, _textFieldStyle);
                    changesMade |= name != descriptor.Name;
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent("Description", "Description of the asset, used in mxm studio"), _textFieldLabelStyle);
                    var desc = descriptor.Description;
                    descriptor.Description = GUILayout.TextField(descriptor.Description, _textFieldStyle);
                    changesMade |= desc != descriptor.Description;
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent("Version", "Version of the asset, used when upgrading assets"), _textFieldLabelStyle);
                    var version = descriptor.Version;
                    descriptor.Version = EditorGUILayout.FloatField(descriptor.Version, _textFieldStyle);
                    changesMade |= version != descriptor.Version;
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);

#if SDK_INTERNAL_FEATURES
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent("Export Location", "PersistentStorage: stores assets to the AppData folder on C drive. \nStreamingAssets: stores built asset to the StreamingAssets folder in Unity project"), _textFieldLabelStyle);
                    var locOpts = descriptor.LocationOption;
                    descriptor.LocationOption = (ExportLocationOptions)EditorGUILayout.Popup(
                        (int)descriptor.LocationOption, Enum.GetNames(typeof(ExportLocationOptions)), _popupStyle);
                    changesMade |= locOpts != descriptor.LocationOption;
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);
#else
                    descriptor.LocationOption = ExportLocationOptions.PersistentStorage;
#endif









                    if (descriptor.ObjectType == MTIONObjectType.MTIONSDK_BLUEPRINT ||
                        descriptor.ObjectType == MTIONObjectType.MTIONSDK_ROOM ||
                        descriptor.ObjectType == MTIONObjectType.MTIONSDK_ENVIRONMENT)
                    {
                        GUILayout.BeginHorizontal();

                        var labelText = "Nav Mesh";
                        var buttonText = "Modify Nav Mesh";
                        if (!_navMeshExists)
                        {
                            labelText = "Nav Mesh (Not found)";
                            buttonText = "Add Nav Mesh";
                        }

                        GUILayout.Label(new GUIContent(labelText), _textFieldLabelStyle);
                        if (GUILayout.Button(buttonText, MTIONSDKToolsWindow.SmallButtonStyle, GUILayout.Width(450)))
                        {
                            EditorApplication.ExecuteMenuItem("Window/AI/Navigation");
                        }

                        GUILayout.EndHorizontal();
                    }



                    if (changesMade)
                    {
                        EditorUtility.SetDirty(descriptor);
                        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                    }
                }
                MTIONSDKToolsWindow.EndBox();

                if (descriptor.ObjectType == MTIONObjectType.MTIONSDK_BLUEPRINT)
                {
                    MTIONSDKToolsWindow.StartBox();
                    {
                        GUILayout.Label("Scenes", _headerLabelStyle);
                        GUILayout.Space(10);

                        var blueprintSDKObject = GameObject.FindObjectOfType<MTIONSDKBlueprint>();
                        if (blueprintSDKObject != null)
                        {
                            if (!_environmentSceneExists)
                            {
                                EditorGUILayout.HelpBox(string.IsNullOrWhiteSpace(_environmentSceneResolutionError)
                                    ? "Environment scene not found. Please drag and drop the scene file."
                                    : _environmentSceneResolutionError,
                                    MessageType.Warning);
                                SceneAsset newSceneAsset = EditorGUILayout.ObjectField("Environment Scene", null, typeof(SceneAsset), false) as SceneAsset;
                                if (newSceneAsset != null)
                                {
                                    blueprintSDKObject.SetEnvironmentSceneReference(newSceneAsset);
                                    Refresh();
                                }
                            }
                            else
                            {
                                bool SwitchScene = GUILayout.Button(new GUIContent("Switch to Environment Scene"), MTIONSDKToolsWindow.MediumButtonStyle);
                                if (SwitchScene)
                                {
                                    if (blueprintSDKObject.TryResolveEnvironmentScenePath(out string environmentScenePath, out _))
                                    {
                                        var scene = EditorSceneManager.GetSceneByPath(environmentScenePath);
                                        if (!scene.IsValid() || !scene.isLoaded)
                                        {
                                            scene = EditorSceneManager.OpenScene(environmentScenePath, OpenSceneMode.Additive);
                                        }
                                        EditorSceneManager.SetActiveScene(scene);
                                    }
                                }
                            }
                        }

                    }
                    MTIONSDKToolsWindow.EndBox();
                }


                if (descriptor.ObjectType == MTIONObjectType.MTIONSDK_ENVIRONMENT)
                {
                    MTIONSDKToolsWindow.StartBox();
                    {
                        GUILayout.Label("Scenes", _headerLabelStyle);
                        GUILayout.Space(10);

                        var blueprintSDKObject = GameObject.FindObjectOfType<MTIONSDKBlueprint>();
                        if (blueprintSDKObject != null)
                        {
                            bool SwitchScene = GUILayout.Button(new GUIContent("Switch to Room Scene"), MTIONSDKToolsWindow.MediumButtonStyle);
                            if (SwitchScene)
                            {
                                if (blueprintSDKObject.TryResolveRoomScenePath(out string roomScenePath, out _))
                                {
                                    var roomScene = EditorSceneManager.GetSceneByPath(roomScenePath);
                                    if (!roomScene.IsValid() || !roomScene.isLoaded)
                                    {
                                        roomScene = EditorSceneManager.OpenScene(roomScenePath, OpenSceneMode.Additive);
                                    }
                                    EditorSceneManager.SetActiveScene(roomScene);
                                }
                            }
                        }

                    }
                    MTIONSDKToolsWindow.EndBox();
                    MTIONSDKToolsWindow.StartBox();
                    {
                        GUILayout.Label("Lighting", _headerLabelStyle);
                        GUILayout.Space(10);

                        bool openLighting = GUILayout.Button(new GUIContent("Open Lighting Panel"), MTIONSDKToolsWindow.MediumButtonStyle);
                        if (openLighting)
                        {
                            EditorApplication.ExecuteMenuItem("Window/Rendering/Lighting");
                        }
                        GUILayout.Space(20);

                        bool guiEnabled = GUI.enabled;
                        GUI.enabled = !Lightmapping.isRunning;

                        bool bakeLighting = GUILayout.Button(new GUIContent("Bake Lighting"), MTIONSDKToolsWindow.MediumButtonStyle);
                        if (bakeLighting)
                        {
                            Lightmapping.BakeAsync();
                        }

                        GUI.enabled = guiEnabled;

                        if (Lightmapping.isRunning)
                        {
                            Rect lastRect = GUILayoutUtility.GetLastRect();

                            EditorGUI.ProgressBar(lastRect, Lightmapping.buildProgress, $"Baking Lighting... {(Lightmapping.buildProgress).ToString("P0")}");
                        }
                    }
                    MTIONSDKToolsWindow.EndBox();
                }

                CreateCustomPropertiesTable();
                DrawWarningsAndErrors();
                DrawExportStatus();

                GUILayout.BeginHorizontal();
                {
                    bool guiEnabled = GUI.enabled;
                    bool shouldProceedWithBuild = false;

                    GUI.enabled = !Lightmapping.isRunning && !BuildManager.IsExportTaskRunning();

                    string buildTooltip = "Processes and bundles scene into package for mxm studio";
                    if (Lightmapping.isRunning)
                    {
                        buildTooltip = "Wait for the lighting baking proccess to end before building the scene.";
                    }
                    else if (BuildManager.IsExportTaskRunning())
                    {
                        buildTooltip = "Wait for the current export to finish before starting another build.";
                    }
                    GUIContent buildButtonContent = new GUIContent("Build Scene", buildTooltip);

                    bool buildScene = GUILayout.Button(buildButtonContent, MTIONSDKToolsWindow.LargeButtonStyle);

                    GUI.enabled = guiEnabled;

                    if (buildScene)
                    {
                        Refresh();
                    }

                    if (buildScene && _buildErrorsExist)
                    {
                        EditorUtility.DisplayDialog("Build errors found", "Please fix all errors indicated in the \"Build\" tab before building.", "Close");
                    }
                    else if (buildScene)
                    {
                        shouldProceedWithBuild = true;
                        var blueprintSDKObject = GameObject.FindObjectOfType<MTIONSDKBlueprint>();
                        if (descriptor.ObjectType == MTIONObjectType.MTIONSDK_ENVIRONMENT)
                        {
                            if (blueprintSDKObject != null)
                            {
                                if (blueprintSDKObject.TryResolveRoomScenePath(out string roomScenePath, out string roomSceneError))
                                {
                                    var roomScene = EditorSceneManager.GetSceneByPath(roomScenePath);
                                    if (!roomScene.IsValid() || !roomScene.isLoaded)
                                    {
                                        roomScene = EditorSceneManager.OpenScene(roomScenePath, OpenSceneMode.Additive);
                                    }
                                    EditorSceneManager.SetActiveScene(roomScene);
                                }
                                else
                                {
                                    EditorUtility.DisplayDialog("Build errors found", roomSceneError, "Close");
                                    shouldProceedWithBuild = false;
                                }
                            }
                        }

                        if (shouldProceedWithBuild && blueprintSDKObject != null)
                        {
                            Debug.Assert(blueprintSDKObject.GUID != null);
                            blueprintSDKObject.ObjectType = MTIONObjectType.MTIONSDK_BLUEPRINT;
                            AssetDatabase.SaveAssets();
                        }

                        var roomSDKObject = GameObject.FindObjectOfType<MTIONSDKRoom>();
                        if (shouldProceedWithBuild && roomSDKObject != null)
                        {
                            Debug.Assert(roomSDKObject.GUID != null);
                            roomSDKObject.ObjectType = MTIONObjectType.MTIONSDK_ROOM;
                            AssetDatabase.SaveAssets();
                        }

                        var envSDKObject = GameObject.FindObjectOfType<MTIONSDKEnvironment>();
                        if (shouldProceedWithBuild && envSDKObject != null)
                        {
                            Debug.Assert(envSDKObject.GUID != null);
                            envSDKObject.ObjectType = MTIONObjectType.MTIONSDK_ENVIRONMENT;
                            AssetDatabase.SaveAssets();
                        }

                        var assetSDKObject = GameObject.FindObjectOfType<MTIONSDKAsset>();
                        if (shouldProceedWithBuild && assetSDKObject != null)
                        {
                            Debug.Assert(assetSDKObject.GUID != null);
                            assetSDKObject.ObjectType = MTIONObjectType.MTIONSDK_ASSET;
                            AssetDatabase.SaveAssets();
                        }

                        var avatarSDKObject = GameObject.FindObjectOfType<MTIONSDKAvatar>();
                        if (shouldProceedWithBuild && avatarSDKObject != null)
                        {
                            Debug.Assert(avatarSDKObject.GUID != null);
                            avatarSDKObject.ObjectType = MTIONObjectType.MTIONSDK_AVATAR;
                            AssetDatabase.SaveAssets();
                        }

                        var assets = GameObject.FindObjectsOfType<MTIONSDKAssetBase>();
                        foreach (var asset in assets)
                        {
                            if (shouldProceedWithBuild)
                            {
                                asset.MigrateFromDescriptorSO();
                            }
                        }

                        if (shouldProceedWithBuild)
                        {
                            AssetDatabase.SaveAssets();

                            BuildManager.BuildScene();
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(10);

                if (BuildManager.GetSceneDescriptor() != null)
                {
                    var buildPath = SDKUtil.GetSDKItemDirectory(descriptor, descriptor.LocationOption);
                    var unityPath = SDKUtil.GetSDKLocalUnityBuildPath(descriptor, descriptor.LocationOption);
                    var id = descriptor.InternalID;
                }
            }
        }

        private static void CreateRoomInstantiationOptions()
        {
            MTIONSDKToolsWindow.StartBox();
            {
                var warningMessage = "MTIONRoom or MTIONEnvironment prefab is not detected in scene.\n" +
                    "Ensure that you initialize the scene first before proceeding.\n" +
                    "<b>This will modfiy the currently opened scene.</b>";
                MTIONSDKToolsWindow.DrawWarning(warningMessage);
            }
            MTIONSDKToolsWindow.EndBox();

            MTIONSDKToolsWindow.StartBox();
            {
                {
                    if (GUILayout.Button(new GUIContent("Create Blueprint Scene", "Initializes current scene to build out a mtion blueprint"), MTIONSDKToolsWindow.LargeButtonStyle))
                    {
                        SDKEditorUtil.CreateBlueprintScene();
                    }
                }
                {
                    if (GUILayout.Button(new GUIContent("Create Asset Scene", "Initializes current scene to build out a mtion asset"), MTIONSDKToolsWindow.LargeButtonStyle))
                    {
                        SDKEditorUtil.CreateAssetScene();
                    }


                    if (GUILayout.Button(new GUIContent("Create Avatar Scene", "Initializes current scene to build out a mtion avatar"), MTIONSDKToolsWindow.LargeButtonStyle))
                    {
                        SDKEditorUtil.CreateAvatarScene();
                    }

                }
            }
            MTIONSDKToolsWindow.EndBox();
        }

        private static void CreateCustomPropertiesTable()
        {
            if (_customPropertyRows.Count == 0)
            {
                return;
            }

            MTIONSDKToolsWindow.StartBox();
            {
                var smallLabelMinWidth = 125;
                var largeLabelMaxWidth = 350;

                EditorGUILayout.LabelField("Custom Properties", _customPropertyHeaderStyle);
                EditorGUILayout.Space(10);


                EditorGUILayout.BeginHorizontal(_customPropertyTableHeaderStyle);

                EditorGUILayout.LabelField(new GUIContent("Property Name"), _customPropertyEntryStyle, GUILayout.MaxWidth(largeLabelMaxWidth));
                EditorGUILayout.LabelField(new GUIContent("Type"), _customPropertyEntryStyle, GUILayout.MinWidth(smallLabelMinWidth));
                EditorGUILayout.LabelField(new GUIContent("Asset"), _customPropertyEntryStyle, GUILayout.MinWidth(smallLabelMinWidth));
                EditorGUILayout.LabelField(new GUIContent("Component"), _customPropertyEntryStyle, GUILayout.MaxWidth(largeLabelMaxWidth));
                EditorGUILayout.LabelField(new GUIContent("Default Value"), _customPropertyEntryStyle, GUILayout.MaxWidth(largeLabelMaxWidth));
                EditorGUILayout.LabelField(new GUIContent("Min Value"), _customPropertyEntryStyle, GUILayout.MinWidth(smallLabelMinWidth));
                EditorGUILayout.LabelField(new GUIContent("Max Value"), _customPropertyEntryStyle, GUILayout.MinWidth(smallLabelMinWidth));
                GUILayout.FlexibleSpace();

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(5);

                foreach (var customPropertyRow in _customPropertyRows)
                {
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.LabelField(customPropertyRow.PropertyName, _customPropertyEntryStyle, GUILayout.MaxWidth(largeLabelMaxWidth));
                    EditorGUILayout.LabelField(customPropertyRow.Type, _customPropertyEntryStyle, GUILayout.MinWidth(smallLabelMinWidth));
                    EditorGUILayout.LabelField(customPropertyRow.AssetName, _customPropertyEntryStyle, GUILayout.MinWidth(smallLabelMinWidth));
                    EditorGUILayout.LabelField(customPropertyRow.ComponentName, _customPropertyEntryStyle, GUILayout.MaxWidth(largeLabelMaxWidth));
                    EditorGUILayout.LabelField(customPropertyRow.DefaultValue, _customPropertyEntryStyle, GUILayout.MaxWidth(largeLabelMaxWidth));
                    EditorGUILayout.LabelField(customPropertyRow.MinValue, _customPropertyEntryStyle, GUILayout.MinWidth(smallLabelMinWidth));
                    EditorGUILayout.LabelField(customPropertyRow.MaxValue, _customPropertyEntryStyle, GUILayout.MinWidth(smallLabelMinWidth));
                    GUILayout.FlexibleSpace();

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(5);
                }
            }
            MTIONSDKToolsWindow.EndBox();
        }

        private static void DrawWarningsAndErrors()
        {
            foreach (var validationMessage in _validationMessages)
            {
                MTIONSDKToolsWindow.StartBox();
                MTIONSDKToolsWindow.DrawWarning(validationMessage.Text, validationMessage.Type);
                MTIONSDKToolsWindow.EndBox();
            }
        }

        private static void DrawExportStatus()
        {
            SDKExportReport exportReport = BuildManager.GetLastExportReport();
            bool exportRunning = BuildManager.IsExportTaskRunning();

            if (!exportRunning && exportReport == null)
            {
                return;
            }

            MTIONSDKToolsWindow.StartBox();
            {
                GUILayout.Label("Export", _headerLabelStyle);
                GUILayout.Space(10);

                if (exportRunning)
                {
                    Rect progressRect = GUILayoutUtility.GetRect(450, 22);
                    EditorGUI.ProgressBar(
                        progressRect,
                        BuildManager.GetExportTaskPrecentageComplete(),
                        $"Exporting... {BuildManager.GetExportTaskPrecentageComplete():P0}");
                    GUILayout.Space(10);
                }

                if (exportReport != null)
                {
                    string summary = string.IsNullOrWhiteSpace(exportReport.Summary)
                        ? "No export summary available yet."
                        : exportReport.Summary;
                    MTIONSDKToolsWindow.DrawWarning(
                        summary,
                        exportReport.Succeeded ? MTIONSDKToolsWindow.WarningType.STANDARD : MTIONSDKToolsWindow.WarningType.ERROR);

                    if (!string.IsNullOrWhiteSpace(exportReport.OutputDirectory))
                    {
                        GUILayout.Space(10);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(new GUIContent("Output Folder"), _textFieldLabelStyle);
                        EditorGUILayout.SelectableLabel(exportReport.OutputDirectory, _sizelessTextFieldStyle, GUILayout.Height(22), GUILayout.Width(450));
                        GUILayout.EndHorizontal();

                        if (GUILayout.Button("Open Export Folder", MTIONSDKToolsWindow.MediumButtonStyle))
                        {
                            EditorUtility.RevealInFinder(exportReport.OutputDirectory);
                        }
                    }
                }
            }
            MTIONSDKToolsWindow.EndBox();
        }


        private static void GenerateWarningsAndErrors()
        {
            _buildErrorsExist = false;
            _buildErrorsExist |= GenerateDescriptorValidationError();
            _buildErrorsExist |= GenerateBlueprintSceneReferenceErrors();
            _buildErrorsExist |= GenerateExportLocationError();
            _buildErrorsExist |= GenerateMissingThumbnailCameraError();
            _buildErrorsExist |= GenerateMissingColliderOnRigidbodyError();
            _buildErrorsExist |= RemoveEventSystemsError();
            _buildErrorsExist |= GenerateNoBuildObjectsDetected();
            _buildErrorsExist |= GenerateMissingAnimatorAvatarError();
            GenerateMissingRagdollAvatarWarning();
            GenerateInvalidUnityEventActionWarnings();
            GenerateDuplicatePropertyNameWarnings();
            GenerateMissingMetadataWarnings();
            GenerateMissingScriptsWarnings();
            GenerateMissingColliderWarnings();
            GenerateActionEmptyEntryPointWarnings();
            GenerateActionOnRoomOrEnvironment();
            GenerateVisualScriptingProjectWarnings();
            GenerateVisualScriptingWarnings();
        }

        private static bool GenerateDescriptorValidationError()
        {
            if (string.IsNullOrWhiteSpace(_descriptorValidationError))
            {
                return false;
            }

            AddValidationMessage(_descriptorValidationError, MTIONSDKToolsWindow.WarningType.ERROR);
            return true;
        }

        private static bool GenerateBlueprintSceneReferenceErrors()
        {
            var blueprintSDKObject = GameObject.FindObjectOfType<MTIONSDKBlueprint>();
            if (blueprintSDKObject == null)
            {
                return false;
            }

            bool hasErrors = false;
            if (!_roomSceneExists)
            {
                AddValidationMessage(_roomSceneResolutionError ?? "Blueprint room scene could not be resolved.", MTIONSDKToolsWindow.WarningType.ERROR);
                hasErrors = true;
            }

            if (!_environmentSceneExists)
            {
                AddValidationMessage(_environmentSceneResolutionError ?? "Blueprint environment scene could not be resolved.", MTIONSDKToolsWindow.WarningType.ERROR);
                hasErrors = true;
            }

            return hasErrors;
        }

        private static bool GenerateExportLocationError()
        {
            var descriptor = BuildManager.GetSceneDescriptor()?.GetComponent<MTIONSDKDescriptorSceneBase>();
            if (descriptor == null)
            {
                return false;
            }

            string baseDirectory = SDKUtil.GetDefaultDirectory(descriptor.LocationOption);
            if (SDKUtil.CanAccessDirectory(baseDirectory, out string errorMessage))
            {
                return false;
            }

            AddValidationMessage($"Export location is not writable: <b>{baseDirectory}</b>. {errorMessage}", MTIONSDKToolsWindow.WarningType.ERROR);
            return true;
        }

        

        private static bool GenerateMissingThumbnailCameraError()
        {
            var descriptor = BuildManager.GetSceneDescriptor()?.GetComponent<MTIONSDKDescriptorSceneBase>();
            if (descriptor == null)
            {
                return false;
            }

            bool hasErrors = false;
            if (descriptor.ObjectType == MTIONObjectType.MTIONSDK_BLUEPRINT)
            {
                MTIONSDKBlueprint blueprint = descriptor as MTIONSDKBlueprint;
                MTIONSDKRoom roomDescriptor = blueprint != null ? blueprint.GetMTIONSDKRoom() : null;
                if (roomDescriptor != null && !ThumbnailGenerator.TryResolveThumbnailCamera(roomDescriptor, out _, out string roomDiagnostic))
                {
                    if (!ThumbnailGenerator.CanGenerateThumbnail(roomDescriptor, out string roomFallbackDiagnostic))
                    {
                        AddValidationMessage($"Room scene is missing a valid thumbnail camera. {roomDiagnostic} {roomFallbackDiagnostic}", MTIONSDKToolsWindow.WarningType.ERROR);
                        hasErrors = true;
                    }
                }

                MTIONSDKEnvironment environmentDescriptor = blueprint != null ? blueprint.GetMTIONSDKEnvironment() : null;
                if (environmentDescriptor != null && !ThumbnailGenerator.TryResolveThumbnailCamera(environmentDescriptor, out _, out string environmentDiagnostic))
                {
                    if (!ThumbnailGenerator.CanGenerateThumbnail(environmentDescriptor, out string environmentFallbackDiagnostic))
                    {
                        AddValidationMessage($"Environment scene is missing a valid thumbnail camera. {environmentDiagnostic} {environmentFallbackDiagnostic}", MTIONSDKToolsWindow.WarningType.ERROR);
                        hasErrors = true;
                    }
                }
            }
            else if ((descriptor.ObjectType == MTIONObjectType.MTIONSDK_ROOM || descriptor.ObjectType == MTIONObjectType.MTIONSDK_ENVIRONMENT) &&
                !ThumbnailGenerator.TryResolveThumbnailCamera(descriptor, out _, out string diagnostic))
            {
                if (!ThumbnailGenerator.CanGenerateThumbnail(descriptor, out string fallbackDiagnostic))
                {
                    AddValidationMessage($"This scene is missing a valid thumbnail camera. {diagnostic} {fallbackDiagnostic}", MTIONSDKToolsWindow.WarningType.ERROR);
                    hasErrors = true;
                }
            }

            return hasErrors;
        }

        private static bool GenerateMissingColliderOnRigidbodyError()
        {
            var gameObjectsMissingCollidersWithRb = SceneVerificationUtil.GetGameObjectsWithRigidbodyWithoutCollider();
            foreach (var go in gameObjectsMissingCollidersWithRb)
            {
                AddValidationMessage($"GameObject <b>{go.name}</b> has a Rigidbody but does not have a collider. Please add a collider or remove the Rigidbody component.", MTIONSDKToolsWindow.WarningType.ERROR);
            }

            return gameObjectsMissingCollidersWithRb.Count > 0;
        }

        private static bool RemoveEventSystemsError()
        {
            EventSystem[] eventSystems = GameObject.FindObjectsOfType<EventSystem>();
            foreach (EventSystem eventSystem in eventSystems)
            {
                string errorMessage = $"GameObject <b>{eventSystem.name}</b> has an EventSystem. Please remove the GameObject or the EventSystem component.";
                AddValidationMessage(errorMessage, MTIONSDKToolsWindow.WarningType.ERROR);
            }

            return eventSystems.Length > 0;
        }

        private static bool GenerateNoBuildObjectsDetected()
        {
            int buildObjectCount = SceneVerificationUtil.GetBuildObjectCountInScene();
            if (buildObjectCount < 1)
            {
                AddValidationMessage("Nothing to build in scene. Add gameobjects and rebuild.", MTIONSDKToolsWindow.WarningType.ERROR);
            }

            return buildObjectCount < 1;
        }

        private static bool GenerateMissingAnimatorAvatarError()
        {
            var avatarHasAnimator = SceneVerificationUtil.AvatarHasAnimator();
            if (!avatarHasAnimator)
            {
                AddValidationMessage("This avatar is missing an animator. Please add an animator component before exporting.", MTIONSDKToolsWindow.WarningType.ERROR);
            }

            return !avatarHasAnimator;
        }

        private static void GenerateInvalidUnityEventActionWarnings()
        {
            var invalidUnityEventActions = SceneVerificationUtil.GetGameObjectsWithInvalidUnityEventActions();
            foreach (var kvp in invalidUnityEventActions)
            {
                var warningMessage = $"UnityEventAction on <b>{kvp.Key.name}</b> contains custom listeners. " +
                    "These listeners will not function once exported to mxm studio.\n";
                for (var i = 0; i < kvp.Value.Count; ++i)
                {
                    warningMessage += $"<b>{kvp.Value[i]}</b>";
                    if (i != kvp.Value.Count - 1)
                    {
                        warningMessage += ", ";
                    }
                }

                AddValidationMessage(warningMessage);
            }
        }

        private static void GenerateDuplicatePropertyNameWarnings()
        {
            var duplicatePropNames = SceneVerificationUtil.GetCustomPropContainersWithDuplicatePropNames();
            foreach (var propContainer in duplicatePropNames.Keys)
            {
                if (duplicatePropNames[propContainer].Count == 0)
                {
                    continue;
                }

                var allPropNames = "";
                foreach (var duplicatePropName in duplicatePropNames[propContainer])
                {
                    allPropNames += $"<b>{duplicatePropName}</b>, ";
                }
                allPropNames = allPropNames.Remove(allPropNames.Length - 2);

                var warningMessage = $"Asset <b>{propContainer.gameObject.name}</b> has duplicate property names: {allPropNames}.";

                AddValidationMessage(warningMessage);
            }
        }

        private static void GenerateMissingMetadataWarnings()
        {
            var missingMetadatas = ComponentVerificationUtil.GetSDKObjectsWithMissingMetadata();
            foreach (var sdkObject in missingMetadatas.Keys)
            {
                var warningMessage = $"<b>{(string.IsNullOrEmpty(sdkObject.Name) ? sdkObject.InternalID : sdkObject.Name)}</b> ({sdkObject.ObjectType}) has empty fields: ";
                bool hasBlockingError = false;
                for (int i = 0; i < missingMetadatas[sdkObject].Count; ++i)
                {
                    if (missingMetadatas[sdkObject][i] == "Object Reference")
                    {
                        hasBlockingError = true;
                    }

                    warningMessage += $"<b>{missingMetadatas[sdkObject][i]}</b>";
                    if (i != missingMetadatas[sdkObject].Count - 1)
                    {
                        warningMessage += ", ";
                    }
                }
                warningMessage += ".";

                AddValidationMessage(warningMessage, hasBlockingError ? MTIONSDKToolsWindow.WarningType.ERROR : MTIONSDKToolsWindow.WarningType.STANDARD);
                _buildErrorsExist |= hasBlockingError;
            }
        }

        private static void GenerateMissingScriptsWarnings()
        {
            var objectsWithMissingScripts = SceneVerificationUtil.GetGameObjectsWithMissingScripts();
            foreach (var go in objectsWithMissingScripts)
            {
                var warningMessage = $"GameObject <b>{go.name}</b> has missing scripts";

                AddValidationMessage(warningMessage);
            }
        }

        private static void GenerateMissingColliderWarnings()
        {
            var objectsWithoutColliders = ComponentVerificationUtil.GetSDKObjectsWithoutColliders();
            foreach (var sdkObject in objectsWithoutColliders)
            {
                var warningMessage = $"<b>{(string.IsNullOrEmpty(sdkObject.Name) ? sdkObject.InternalID : sdkObject.Name)}</b> does not have a collider.";

                AddValidationMessage(warningMessage);
            }
        }

        private static void GenerateActionEmptyEntryPointWarnings()
        {
            var actionsWithEmptyEntryPoints = ComponentVerificationUtil.GetActionBehavioursEmptyEntryPoints();
            foreach (var action in actionsWithEmptyEntryPoints)
            {
                var warningMessage = $"Action <b>{action.ActionName}</b> has no entry points or empty entry points. Fix this in the \"Actions\" tab.";

                AddValidationMessage(warningMessage);
            }
        }

        private static void GenerateMissingRagdollAvatarWarning()
        {
            var ragdollConfigured = SceneVerificationUtil.IsRagdollConfiguredForAvatar();
            if (!ragdollConfigured)
            {
                AddValidationMessage("Ragdoll has not been configured for this avatar. Set up the ragdoll in the <b>Ragdoll</b> tab.");
            }
        }

        private static void GenerateActionOnRoomOrEnvironment()
        {
            MTIONSDKAssetBase sceneDescriptorObject = GameObject.FindObjectOfType<MTIONSDKRoom>();

            if (sceneDescriptorObject == null)
            {
                sceneDescriptorObject = GameObject.FindObjectOfType<MTIONSDKEnvironment>();
            }
            else
            {
                IAction[] actions = (sceneDescriptorObject as MTIONSDKRoom).SDKRoot.GetComponentsInChildren<IAction>();
                foreach (IAction action in actions)
                {
                    MVirtualAssetTracker tracker = (action as MonoBehaviour).GetComponentInParent<MVirtualAssetTracker>();
                    if (tracker != null)
                    {
                        AddValidationMessage(
                            $"Asset <b>{tracker.gameObject.name}</b> contains Action {action.GetType().Name}. Please remove it. Actions are not allowed on objects inside world templates.");
                    }
                }
            }

            if (sceneDescriptorObject != null)
            {
                IAction[] actions = sceneDescriptorObject.ObjectReference.GetComponentsInChildren<IAction>();
                if (actions.Length > 0)
                {
                    AddValidationMessage($"{actions.Length} Action scripts found in the scene. Actions can only be added to assets. Fix this by removing any Action Components from the scene.");
                }
            }
        }

        private static void GenerateVisualScriptingWarnings()
        {
            var descriptor = BuildManager.GetSceneDescriptor()?.GetComponent<MTIONSDKDescriptorSceneBase>();
            if (descriptor == null)
            {
                return;
            }

            if (descriptor.ObjectType == MTIONObjectType.MTIONSDK_BLUEPRINT)
            {
                var blueprint = descriptor as MTIONSDKBlueprint;
                if (blueprint != null)
                {
                    var roomDescriptor = blueprint.GetMTIONSDKRoom();
                    if (roomDescriptor != null)
                    {
                        AddVisualScriptingValidationMessages(
                            "Room visual scripting",
                            VisualScriptingSupportUtil.InspectSceneForExport(roomDescriptor.gameObject.scene, VisualScriptingExportTarget.RoomScene));
                        AddVisualScriptingPlacementValidationMessages("Room visual scripting", roomDescriptor.ObjectReferenceProp);
                    }

                    var environmentDescriptor = blueprint.GetMTIONSDKEnvironment();
                    if (environmentDescriptor != null)
                    {
                        AddVisualScriptingValidationMessages(
                            "Environment visual scripting",
                            VisualScriptingSupportUtil.InspectSceneForExport(environmentDescriptor.gameObject.scene, VisualScriptingExportTarget.EnvironmentScene));
                        AddVisualScriptingPlacementValidationMessages("Environment visual scripting", environmentDescriptor.ObjectReferenceProp);
                    }
                }

                return;
            }

            VisualScriptingInspectionReport report = null;
            string label = "Visual scripting";
            switch (descriptor.ObjectType)
            {
                case MTIONObjectType.MTIONSDK_ROOM:
                    label = "Room visual scripting";
                    report = VisualScriptingSupportUtil.InspectSceneForExport(descriptor.gameObject.scene, VisualScriptingExportTarget.RoomScene);
                    break;
                case MTIONObjectType.MTIONSDK_ENVIRONMENT:
                    label = "Environment visual scripting";
                    report = VisualScriptingSupportUtil.InspectSceneForExport(descriptor.gameObject.scene, VisualScriptingExportTarget.EnvironmentScene);
                    break;
                case MTIONObjectType.MTIONSDK_ASSET:
                case MTIONObjectType.MTIONSDK_AVATAR:
                    label = "Portable asset visual scripting";
                    report = VisualScriptingSupportUtil.InspectGameObjectForExport(descriptor.ObjectReferenceProp, VisualScriptingExportTarget.PortablePrefab);
                    break;
            }

            AddVisualScriptingValidationMessages(label, report);
            AddVisualScriptingPlacementValidationMessages(label, descriptor.ObjectReferenceProp);
        }

        private static void GenerateVisualScriptingProjectWarnings()
        {
            VisualScriptingGeneratedDataAuditResult auditResult = VisualScriptingProjectPreflight.AuditGeneratedData();
            if (auditResult.IsHealthy)
            {
                return;
            }

            AddValidationMessage(
                $"Unity Visual Scripting generated project data references missing types: {string.Join(", ", auditResult.MissingTypeNames)}. Re-exporting will auto-clean the stale generated database.",
                MTIONSDKToolsWindow.WarningType.STANDARD);
        }

        private static void AddVisualScriptingValidationMessages(string label, VisualScriptingInspectionReport report)
        {
            if (report == null || !report.HasVisualScripting)
            {
                return;
            }

            foreach (string error in report.Errors)
            {
                AddValidationMessage($"{label}: {error}", MTIONSDKToolsWindow.WarningType.ERROR);
                _buildErrorsExist = true;
            }

            foreach (string warning in report.Warnings)
            {
                AddValidationMessage($"{label}: {warning}");
            }
        }

        private static void AddVisualScriptingPlacementValidationMessages(string label, GameObject sdkRoot)
        {
            if (sdkRoot == null)
            {
                return;
            }

            List<Component> rootLevelComponents = VisualScriptingHostUtility.GetRootLevelVisualScriptingComponents(sdkRoot);
            if (rootLevelComponents.Count == 0)
            {
                return;
            }

            string componentNames = string.Join(", ", rootLevelComponents
                .Select(component => component.GetType().Name)
                .Distinct()
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase));

            AddValidationMessage(
                $"{label}: UVS components are attached directly to the SDK root ({componentNames}). Use Configure UVS to migrate them into {VisualScriptingHostUtility.HostObjectName}.",
                MTIONSDKToolsWindow.WarningType.ERROR);
            _buildErrorsExist = true;

            var registryType = Type.GetType("mtion.room.sdk.visualscripting.UVSSDKEntryPointRegistry, MTIONStudioSDK_Public_Core");
            Component registryComponent = registryType != null ? sdkRoot.GetComponentInChildren(registryType, true) : null;
            if (registryComponent != null && TryGetDuplicateEntryPointDisplayNames(registryComponent, out List<string> duplicateDisplayNames) && duplicateDisplayNames.Count > 0)
            {
                AddValidationMessage(
                    $"{label}: Duplicate SDK Entry Point display names are not allowed: {string.Join(", ", duplicateDisplayNames)}.",
                    MTIONSDKToolsWindow.WarningType.ERROR);
                _buildErrorsExist = true;
            }
        }

        private static bool TryGetDuplicateEntryPointDisplayNames(Component registryComponent, out List<string> duplicateDisplayNames)
        {
            duplicateDisplayNames = new List<string>();
            if (registryComponent == null)
            {
                return false;
            }

            var method = registryComponent.GetType().GetMethod("HasDuplicateDisplayNames");
            if (method == null)
            {
                return false;
            }

            object[] args = { null };
            bool hasDuplicates = (bool)method.Invoke(registryComponent, args);
            if (args[0] is List<string> duplicates)
            {
                duplicateDisplayNames = duplicates;
            }

            return hasDuplicates;
        }

        private static void AddValidationMessage(string text, MTIONSDKToolsWindow.WarningType type = MTIONSDKToolsWindow.WarningType.STANDARD)
        {
            _validationMessages.Add(new BuildValidationMessage
            {
                Text = text,
                Type = type
            });
        }


        private static void EnsureBuildStateIsCurrent()
        {
            if (_buildStateDirty)
            {
                Refresh();
            }
        }

        private static bool SceneAssetExists(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return false;
            }

            if (_sceneAssetExistenceCache.TryGetValue(sceneName, out bool sceneExists))
            {
                return sceneExists;
            }

            sceneExists = false;
            string[] guids = AssetDatabase.FindAssets("t:SceneAsset");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string candidateSceneName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (candidateSceneName == sceneName)
                {
                    sceneExists = true;
                    break;
                }
            }

            _sceneAssetExistenceCache[sceneName] = sceneExists;
            return sceneExists;
        }

        private static void EnsureStylesInitialized()
        {
            if (_cachedSkin == GUI.skin &&
                _headerLabelStyle != null &&
                _customPropertyTableHeaderStyle != null)
            {
                return;
            }

            _cachedSkin = GUI.skin;

            _headerLabelStyle = new GUIStyle(EditorStyles.label);
            _headerLabelStyle.alignment = TextAnchor.MiddleLeft;
            _headerLabelStyle.fontStyle = FontStyle.Bold;
            _headerLabelStyle.normal.textColor = Color.white;
            _headerLabelStyle.fontSize = 16;

            _textFieldStyle = new GUIStyle(GUI.skin.textField);
            _textFieldStyle.normal.background = MTIONSDKToolsWindow.CreateTextureForColor(1, 1, new Color(0.317f, 0.317f, 0.317f));
            _textFieldStyle.normal.textColor = Color.white;
            _textFieldStyle.fontSize = 14;
            _textFieldStyle.fixedWidth = 450;
            _textFieldStyle.fixedHeight = 22;

            _sizelessTextFieldStyle = new GUIStyle(GUI.skin.textField);
            _sizelessTextFieldStyle.normal.background = MTIONSDKToolsWindow.CreateTextureForColor(1, 1, new Color(0.317f, 0.317f, 0.317f));
            _sizelessTextFieldStyle.normal.textColor = Color.white;

            _textFieldLabelStyle = new GUIStyle(EditorStyles.label);
            _textFieldLabelStyle.fontSize = 14;
            _textFieldLabelStyle.normal.textColor = Color.white;
            _textFieldLabelStyle.fixedWidth = 150;
            _textFieldLabelStyle.fixedHeight = 22;
            _textFieldLabelStyle.alignment = TextAnchor.MiddleLeft;
            _textFieldLabelStyle.margin = new RectOffset(2, 0, 0, 0);

            _popupStyle = new GUIStyle(EditorStyles.popup);
            _popupStyle.fixedWidth = 450;
            _popupStyle.fixedHeight = 22;
            _popupStyle.fontSize = 14;

            _toggleStyle = new GUIStyle(GUI.skin.toggle);
            _toggleStyle.normal.textColor = Color.white;
            _toggleStyle.fontSize = 14;
            _toggleStyle.fixedWidth = 22;
            _toggleStyle.fixedHeight = 22;

            _customPropertyHeaderStyle = new GUIStyle(EditorStyles.label);
            _customPropertyHeaderStyle.fontStyle = FontStyle.Bold;
            _customPropertyHeaderStyle.fontSize = 16;
            _customPropertyHeaderStyle.normal.textColor = Color.white;

            _customPropertyEntryStyle = new GUIStyle(EditorStyles.label);
            _customPropertyEntryStyle.fontSize = 14;
            _customPropertyEntryStyle.normal.textColor = Color.white;

            _customPropertyTableHeaderStyle = new GUIStyle();
            _customPropertyTableHeaderStyle.normal.background = MTIONSDKToolsWindow.CreateTextureForColor(1, 1, new Color(0.317f, 0.317f, 0.317f));
        }


        private static Vector3 GetNewObjectPosition()
        {
            Camera sceneCamera = EditorWindow.GetWindow<SceneView>().camera;
            Ray ray = sceneCamera.ScreenPointToRay(new Vector3(sceneCamera.pixelWidth / 2, sceneCamera.pixelHeight / 2, 1));

            return ray.GetPoint(1.5f);
        }

        private static string GetCustomPropertyTypeName(ICustomProperty customProp)
        {
            switch (customProp)
            {
                case BoolCustomProperty:
                    return "Bool";
                case IntCustomProperty:
                    return "Int";
                case FloatCustomProperty:
                    return "Float";
                case StringCustomProperty:
                    return "String";
                case ListIntCustomProperty:
                    return "Int List";
                case ListStringCustomProperty:
                    return "String List";
            }

            return null;
        }

        private static string GetCustomPropertyDefaultValue(ICustomProperty customProp)
        {
            switch (customProp)
            {
                case BoolCustomProperty boolCustomProperty:
                    return $"{boolCustomProperty.DefaultValue}";
                case IntCustomProperty intCustomProperty:
                    return $"{intCustomProperty.DefaultValue}";
                case FloatCustomProperty floatCustomProperty:
                    return $"{floatCustomProperty.DefaultValue}";
                case StringCustomProperty stringCustomProperty:
                    return stringCustomProperty.DefaultValue;
                default:
                    return "N/A";
            }
        }

        private static string GetCustomPropertyMinValue(ICustomProperty customProp)
        {
            switch (customProp)
            {
                case IntCustomProperty intCustomProperty:
                    return intCustomProperty.MinValue == int.MinValue ? "" : $"{intCustomProperty.MinValue}";
                case FloatCustomProperty floatCustomProperty:
                    return floatCustomProperty.MinValue == float.MinValue ? "" : $"{floatCustomProperty.MinValue}";
                case ListIntCustomProperty listIntCustomProperty:
                    return listIntCustomProperty.MinValue == int.MinValue ? "" : $"{listIntCustomProperty.MinValue}";
                default:
                    return "";
            }
        }

        private static string GetCustomPropertyMaxValue(ICustomProperty customProp)
        {
            switch (customProp)
            {
                case IntCustomProperty intCustomProperty:
                    return intCustomProperty.MaxValue == int.MaxValue ? "" : $"{intCustomProperty.MaxValue}";
                case FloatCustomProperty floatCustomProperty:
                    return floatCustomProperty.MaxValue == float.MaxValue ? "" : $"{floatCustomProperty.MaxValue}";
                case ListIntCustomProperty listIntCustomProperty:
                    return listIntCustomProperty.MaxValue == int.MaxValue ? "" : $"{listIntCustomProperty.MaxValue}";
                default:
                    return "";
            }
        }

    }
}
