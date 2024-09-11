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
        public static ReorderableList cameraViewHints = null;
        public static List<MVirtualCameraEventTracker> virtualcameraEvents = new List<MVirtualCameraEventTracker>();

        public static ReorderableList displayComponentRList = null;
        public static List<MVirtualDisplayTracker> displayComponents = new List<MVirtualDisplayTracker>();

        public static List<CustomPropertiesContainer> allCustomPropertiesContainers = new List<CustomPropertiesContainer>();

        private static bool _buildErrorsExist;
        private static Vector2 _scrollPos;

        public static void Refresh()
        {
            allCustomPropertiesContainers.Clear();

            var _sceneDescriptorObject = GameObject.FindObjectOfType<MTIONSDKRoom>();
            if (_sceneDescriptorObject == null)
            {
                return;
            }

            SceneVerificationUtil.VerifySceneIntegrity(_sceneDescriptorObject);
            ComponentVerificationUtil.VerifyAllComponentsIntegrity(_sceneDescriptorObject,
                    MTIONObjectType.MTIONSDK_ASSET, false);

            var propContainers = GameObject.FindObjectsOfType<CustomPropertiesContainer>();
            foreach (var propContainer in propContainers)
            {
                if (propContainer.GetAllProperties().Count == 0)
                {
                    continue;
                }

                allCustomPropertiesContainers.Add(propContainer);
            }
        }

        public static void Draw()
        {
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
                GUIStyle headerLabelStyle = new GUIStyle();
                headerLabelStyle.alignment = TextAnchor.MiddleLeft;
                headerLabelStyle.fontStyle = FontStyle.Bold;
                headerLabelStyle.normal.textColor = Color.white;
                headerLabelStyle.fontSize = 16;

                var textFieldStyle = new GUIStyle(GUI.skin.textField);
                textFieldStyle.normal.background = MTIONSDKToolsWindow.CreateTextureForColor(1, 1, new Color(0.317f, 0.317f, 0.317f));
                textFieldStyle.normal.textColor = Color.white;
                textFieldStyle.fontSize = 14;
                textFieldStyle.fixedWidth = 450;
                textFieldStyle.fixedHeight = 22;

                var sizelessTextFieldStyle = new GUIStyle(GUI.skin.textField);
                sizelessTextFieldStyle.normal.background = MTIONSDKToolsWindow.CreateTextureForColor(1, 1, new Color(0.317f, 0.317f, 0.317f));
                sizelessTextFieldStyle.normal.textColor = Color.white;

                var textFieldLabelStyle = new GUIStyle();
                textFieldLabelStyle.fontSize = 14;
                textFieldLabelStyle.normal.textColor = Color.white;
                textFieldLabelStyle.fixedWidth = 150;
                textFieldLabelStyle.fixedHeight = 22;
                textFieldLabelStyle.alignment = TextAnchor.MiddleLeft;
                textFieldLabelStyle.margin = new RectOffset(2, 0, 0, 0);

                var popupStyle = new GUIStyle(EditorStyles.popup);
                popupStyle.fixedWidth = 450;
                popupStyle.fixedHeight = 22;
                popupStyle.fontSize = 14;

                var toggleStyle = new GUIStyle(GUI.skin.toggle);
                toggleStyle.normal.textColor = Color.white;
                toggleStyle.fontSize = 14;
                toggleStyle.fixedWidth = 22;
                toggleStyle.fixedHeight = 22;

                var descriptorGO = BuildManager.GetSceneDescriptor();
                var descriptor = descriptorGO.GetComponent<MTIONSDKDescriptorSceneBase>();

                MTIONSDKToolsWindow.StartBox();
                {
                    GUILayout.Label("Settings", headerLabelStyle);
                    GUILayout.Space(10);

                    bool changesMade = false;

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent("Show grid", "Enables grid visualization in the scene"), textFieldLabelStyle);
                    bool showGrid = GUILayout.Toggle(descriptor.ShowGrid, "", toggleStyle);
                    if (showGrid != descriptor.ShowGrid)
                    {
                        descriptor.ShowGrid = showGrid;
                        changesMade = true;
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent("Grid Size", "Adjusts the size of the grid"), textFieldLabelStyle);
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
                    GUILayout.Label(new GUIContent("Scale Reference", "Places an object in the scene that can be used to reference asset size"), textFieldLabelStyle);
                    SDKScaleReference scaleReference =
                        (SDKScaleReference)EditorGUILayout.EnumPopup("", descriptor.ScaleReference, popupStyle);
                    if (scaleReference != descriptor.ScaleReference)
                    {
                        descriptor.ScaleReference = scaleReference;
                        changesMade = true;
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent("Reference Position", "Adjusts the location of the reference object"), textFieldLabelStyle);

                    EditorGUIUtility.labelWidth = 10;
                    float xValue = EditorGUILayout.FloatField("X", descriptor.ScaleReferencePosition.x, sizelessTextFieldStyle, GUILayout.Height(22),
                        GUILayout.MaxWidth(148));
                    float yValue = EditorGUILayout.FloatField("Y", descriptor.ScaleReferencePosition.y, sizelessTextFieldStyle, GUILayout.Height(22),
                        GUILayout.MaxWidth(148));
                    float zValue = EditorGUILayout.FloatField("Z", descriptor.ScaleReferencePosition.z, sizelessTextFieldStyle, GUILayout.Height(22),
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

                MTIONSDKToolsWindow.StartBox();
                {
                    GUILayout.Label("Options", headerLabelStyle);
                    GUILayout.Space(10);

                    bool changesMade = false;

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent("Name", "Name of the asset, used in mtion studio"), textFieldLabelStyle);
                    var name = descriptor.Name;
                    descriptor.Name = GUILayout.TextField(descriptor.Name, textFieldStyle);
                    changesMade |= name != descriptor.Name;
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent("Description", "Description of the asset, used in mtion studio"), textFieldLabelStyle);
                    var desc = descriptor.Description;
                    descriptor.Description = GUILayout.TextField(descriptor.Description, textFieldStyle);
                    changesMade |= desc != descriptor.Description;
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent("Version", "Version of the asset, used when upgrading assets"), textFieldLabelStyle);
                    var version = descriptor.Version;
                    descriptor.Version = EditorGUILayout.FloatField(descriptor.Version, textFieldStyle);
                    changesMade |= version != descriptor.Version;
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);

#if SDK_INTERNAL_FEATURES
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent("Export Location", "PersistentStorage: stores assets to the AppData folder on C drive. \nStreamingAssets: stores built asset to the StreamingAssets folder in Unity project"), textFieldLabelStyle);
                    var locOpts = descriptor.LocationOption;
                    descriptor.LocationOption = (ExportLocationOptions)EditorGUILayout.Popup(
                        (int)descriptor.LocationOption, Enum.GetNames(typeof(ExportLocationOptions)), popupStyle);
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

                        var foundNavMesh = NavMesh.SamplePosition(Vector3.zero, out var navMeshHit, 1000f, ~0);

                        var labelText = "Nav Mesh";
                        var buttonText = "Modify Nav Mesh";
                        if (!foundNavMesh)
                        {
                            labelText = "Nav Mesh (Not found)";
                            buttonText = "Add Nav Mesh";
                        }

                        GUILayout.Label(new GUIContent(labelText), textFieldLabelStyle);
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
                        GUILayout.Label("Scenes", headerLabelStyle);
                        GUILayout.Space(10);

                        var blueprintSDKObject = GameObject.FindObjectOfType<MTIONSDKBlueprint>();
                        if (blueprintSDKObject != null)
                        {
                            bool SwitchScene = GUILayout.Button(new GUIContent("Switch to Environment Scene"), MTIONSDKToolsWindow.MediumButtonStyle);
                            if (SwitchScene)
                            {
                                var scene = EditorSceneManager.GetSceneByName(blueprintSDKObject.EnvironmentSceneName);
                                EditorSceneManager.SetActiveScene(scene);
                            }
                        }

                    }
                    MTIONSDKToolsWindow.EndBox();
                }


                if (descriptor.ObjectType == MTIONObjectType.MTIONSDK_ENVIRONMENT)
                {
                    MTIONSDKToolsWindow.StartBox();
                    {
                        GUILayout.Label("Scenes", headerLabelStyle);
                        GUILayout.Space(10);

                        var blueprintSDKObject = GameObject.FindObjectOfType<MTIONSDKBlueprint>();
                        if (blueprintSDKObject != null)
                        {
                            bool SwitchScene = GUILayout.Button(new GUIContent("Switch to Room Scene"), MTIONSDKToolsWindow.MediumButtonStyle);
                            if (SwitchScene)
                            {
                                var roomScene = EditorSceneManager.GetSceneByName(blueprintSDKObject.RoomSceneName);
                                EditorSceneManager.SetActiveScene(roomScene);
                            }
                        }

                    }
                    MTIONSDKToolsWindow.EndBox();
                    MTIONSDKToolsWindow.StartBox();
                    {
                        GUILayout.Label("Lighting", headerLabelStyle);
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

                GenerateWarningsAndErrors();

                GUILayout.BeginHorizontal();
                {
                    bool guiEnabled = GUI.enabled;

                    GUI.enabled = !Lightmapping.isRunning;

                    string buildTooltip = "Processes and bundles scene into package for mtion studio";
                    if (Lightmapping.isRunning)
                    {
                        buildTooltip = "Wait for the lighting baking proccess to end before building the scene.";
                    }
                    GUIContent buildButtonContent = new GUIContent("Build Scene", buildTooltip);

                    bool buildScene = GUILayout.Button(buildButtonContent, MTIONSDKToolsWindow.LargeButtonStyle);

                    GUI.enabled = guiEnabled;

                    if (buildScene && _buildErrorsExist)
                    {
                        EditorUtility.DisplayDialog("Build errors found", "Please fix all errors indicated in the \"Build\" tab before building.", "Close");
                    }
                    else if (buildScene)
                    {
                        var blueprintSDKObject = GameObject.FindObjectOfType<MTIONSDKBlueprint>();
                        if (descriptor.ObjectType == MTIONObjectType.MTIONSDK_ENVIRONMENT)
                        {
                            if (blueprintSDKObject != null)
                            {
                                var roomScene = EditorSceneManager.GetSceneByName(blueprintSDKObject.RoomSceneName);
                                EditorSceneManager.SetActiveScene(roomScene);
                            }
                            EditorGUILayout.EndHorizontal();
                            return;
                        }

                        if (blueprintSDKObject != null)
                        {
                            Debug.Assert(blueprintSDKObject.GUID != null);
                            blueprintSDKObject.ObjectType = MTIONObjectType.MTIONSDK_BLUEPRINT;
                            AssetDatabase.SaveAssets();
                        }

                        var roomSDKObject = GameObject.FindObjectOfType<MTIONSDKRoom>();
                        if (roomSDKObject != null)
                        {
                            Debug.Assert(roomSDKObject.GUID != null);
                            roomSDKObject.ObjectType = MTIONObjectType.MTIONSDK_ROOM;
                            AssetDatabase.SaveAssets();
                        }

                        var envSDKObject = GameObject.FindObjectOfType<MTIONSDKEnvironment>();
                        if (envSDKObject != null)
                        {
                            Debug.Assert(envSDKObject.GUID != null);
                            envSDKObject.ObjectType = MTIONObjectType.MTIONSDK_ENVIRONMENT;
                            AssetDatabase.SaveAssets();
                        }

                        var assetSDKObject = GameObject.FindObjectOfType<MTIONSDKAsset>();
                        if (assetSDKObject != null)
                        {
                            Debug.Assert(assetSDKObject.GUID != null);
                            assetSDKObject.ObjectType = MTIONObjectType.MTIONSDK_ASSET;
                            AssetDatabase.SaveAssets();
                        }

                        var avatarSDKObject = GameObject.FindObjectOfType<MTIONSDKAvatar>();
                        if (avatarSDKObject != null)
                        {
                            Debug.Assert(avatarSDKObject.GUID != null);
                            avatarSDKObject.ObjectType = MTIONObjectType.MTIONSDK_AVATAR;
                            AssetDatabase.SaveAssets();
                        }

                        var assets = GameObject.FindObjectsOfType<MTIONSDKAssetBase>();
                        foreach (var asset in assets)
                        {
                            asset.MigrateFromDescriptorSO();
                        }
                        AssetDatabase.SaveAssets();

                        BuildManager.BuildScene();
                    }
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(10);

                if (BuildManager.GetSceneDescriptor() != null)
                {
                    var buildPath = SDKUtil.GetSDKItemDirectory(descriptor, descriptor.LocationOption);
                    var unityPath = SDKUtil.GetSDKLocalUnityBuildPath(descriptor, descriptor.LocationOption);
                    var webglPath = SDKUtil.GetSDKLocalWebGLBuildPath(descriptor, descriptor.LocationOption);
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
            if (allCustomPropertiesContainers.Any(x => x == null))
            {
                Refresh();
            }

            if (allCustomPropertiesContainers.Count == 0)
            {
                return;
            }

            MTIONSDKToolsWindow.StartBox();
            {
                var headerLabelStyle = new GUIStyle(EditorStyles.label);
                headerLabelStyle.fontStyle = FontStyle.Bold;
                headerLabelStyle.fontSize = 16;
                headerLabelStyle.normal.textColor = Color.white;

                var entryLabelStyle = new GUIStyle(EditorStyles.label);
                entryLabelStyle.fontSize = 14;
                entryLabelStyle.normal.textColor = Color.white;

                var tableHeaderStyle = new GUIStyle();
                tableHeaderStyle.normal.background = MTIONSDKToolsWindow.CreateTextureForColor(1, 1, new Color(0.317f, 0.317f, 0.317f));

                var smallLabelMinWidth = 125;
                var largeLabelMaxWidth = 350;

                EditorGUILayout.LabelField("Custom Properties", headerLabelStyle);
                EditorGUILayout.Space(10);


                EditorGUILayout.BeginHorizontal(tableHeaderStyle);

                EditorGUILayout.LabelField(new GUIContent("Property Name"), entryLabelStyle, GUILayout.MaxWidth(largeLabelMaxWidth));
                EditorGUILayout.LabelField(new GUIContent("Type"), entryLabelStyle, GUILayout.MinWidth(smallLabelMinWidth));
                EditorGUILayout.LabelField(new GUIContent("Asset"), entryLabelStyle, GUILayout.MinWidth(smallLabelMinWidth));
                EditorGUILayout.LabelField(new GUIContent("Component"), entryLabelStyle, GUILayout.MaxWidth(largeLabelMaxWidth));
                EditorGUILayout.LabelField(new GUIContent("Default Value"), entryLabelStyle, GUILayout.MaxWidth(largeLabelMaxWidth));
                EditorGUILayout.LabelField(new GUIContent("Min Value"), entryLabelStyle, GUILayout.MinWidth(smallLabelMinWidth));
                EditorGUILayout.LabelField(new GUIContent("Max Value"), entryLabelStyle, GUILayout.MinWidth(smallLabelMinWidth));
                GUILayout.FlexibleSpace();

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(5);

                foreach (var customPropContainer in allCustomPropertiesContainers)
                {
                    foreach (var customProp in customPropContainer.GetAllProperties())
                    {
                        EditorGUILayout.BeginHorizontal();

                        var declaringType = Type.GetType(customProp.DeclaringTypeName);
                        var declaringTypeClassName = declaringType.Name;

                        EditorGUILayout.LabelField(customProp.PropertyName, entryLabelStyle, GUILayout.MaxWidth(largeLabelMaxWidth));
                        EditorGUILayout.LabelField(GetCustomPropertyTypeName(customProp), entryLabelStyle, GUILayout.MinWidth(smallLabelMinWidth));
                        EditorGUILayout.LabelField(customPropContainer.gameObject.name, entryLabelStyle, GUILayout.MinWidth(smallLabelMinWidth));
                        EditorGUILayout.LabelField(declaringTypeClassName, entryLabelStyle, GUILayout.MaxWidth(largeLabelMaxWidth));
                        EditorGUILayout.LabelField(GetCustomPropertyDefaultValue(customProp), entryLabelStyle, GUILayout.MaxWidth(largeLabelMaxWidth));
                        EditorGUILayout.LabelField(GetCustomPropertyMinValue(customProp), entryLabelStyle, GUILayout.MinWidth(smallLabelMinWidth));
                        EditorGUILayout.LabelField(GetCustomPropertyMaxValue(customProp), entryLabelStyle, GUILayout.MinWidth(smallLabelMinWidth));
                        GUILayout.FlexibleSpace();

                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.Space(5);
                    }
                }
            }
            MTIONSDKToolsWindow.EndBox();
        }


        private static void GenerateWarningsAndErrors()
        {
            _buildErrorsExist = false;
            _buildErrorsExist |= GenerateIncorrectNumCamerasError();
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
        }

        private static bool GenerateIncorrectNumCamerasError()
        {
            var numCameras = Camera.allCameras.Length;
            if (numCameras == 0)
            {
                var warningMessage = $"No enabled cameras found in scene! A camera is required to generate thumbnails.";

                MTIONSDKToolsWindow.StartBox();
                MTIONSDKToolsWindow.DrawWarning(warningMessage, MTIONSDKToolsWindow.WarningType.ERROR);
                MTIONSDKToolsWindow.EndBox();

                return true;
            }
            else if (numCameras == 2)
            {
                if (GameObject.FindAnyObjectByType<MTIONSDKEnvironment>() != null &&
                    GameObject.FindAnyObjectByType<MTIONSDKRoom>() != null)
                {
                    return false;
                }
            }
            else if (numCameras > 2)
            {
                var warningMessage = $"Multiple active cameras found in scene. Please disable or remove additional cameras";

                MTIONSDKToolsWindow.StartBox();
                MTIONSDKToolsWindow.DrawWarning(warningMessage, MTIONSDKToolsWindow.WarningType.ERROR);
                MTIONSDKToolsWindow.EndBox();

                return true;
            }

            return false;
        }

        private static bool GenerateMissingColliderOnRigidbodyError()
        {
            var gameObjectsMissingCollidersWithRb = SceneVerificationUtil.GetGameObjectsWithRigidbodyWithoutCollider();
            foreach (var go in gameObjectsMissingCollidersWithRb)
            {
                var warningMessage = $"GameObject <b>{go.name}</b> has a Rigidbody but does not have a collider. " +
                    $"Please add a collider or remove the Rigidbody component.";

                MTIONSDKToolsWindow.StartBox();
                MTIONSDKToolsWindow.DrawWarning(warningMessage, MTIONSDKToolsWindow.WarningType.ERROR);
                MTIONSDKToolsWindow.EndBox();
            }

            return gameObjectsMissingCollidersWithRb.Count > 0;
        }

        private static bool RemoveEventSystemsError()
        {
            EventSystem[] eventSystems = GameObject.FindObjectsOfType<EventSystem>();
            foreach (EventSystem eventSystem in eventSystems)
            {
                string errorMessage = $"GameObject <b>{eventSystem.name}</b> has an EventSystem. " +
                                     $"Please remove the GameObject or the EventSystem component.";

                MTIONSDKToolsWindow.StartBox();
                MTIONSDKToolsWindow.DrawWarning(errorMessage, MTIONSDKToolsWindow.WarningType.ERROR);
                MTIONSDKToolsWindow.EndBox();
            }

            return eventSystems.Length > 0;
        }

        private static bool GenerateNoBuildObjectsDetected()
        {
            int buildObjectCount = SceneVerificationUtil.GetBuildObjectCountInScene();
            if (buildObjectCount < 1)
            {
                var warningMessage = $"Nothing to build in scene. Add gameobjects and rebuild.";

                MTIONSDKToolsWindow.StartBox();
                MTIONSDKToolsWindow.DrawWarning(warningMessage, MTIONSDKToolsWindow.WarningType.ERROR);
                MTIONSDKToolsWindow.EndBox();

            }

            return buildObjectCount < 1;
        }

        private static bool GenerateMissingAnimatorAvatarError()
        {
            var avatarHasAnimator = SceneVerificationUtil.AvatarHasAnimator();
            if (!avatarHasAnimator)
            {
                var warningMessage = $"This avatar is missing an animator. Please add an animator component before exporting.";

                MTIONSDKToolsWindow.StartBox();
                MTIONSDKToolsWindow.DrawWarning(warningMessage, MTIONSDKToolsWindow.WarningType.ERROR);
                MTIONSDKToolsWindow.EndBox();
            }

            return !avatarHasAnimator;
        }

        private static void GenerateInvalidUnityEventActionWarnings()
        {
            var invalidUnityEventActions = SceneVerificationUtil.GetGameObjectsWithInvalidUnityEventActions();
            foreach (var kvp in invalidUnityEventActions)
            {
                var warningMessage = $"UnityEventAction on <b>{kvp.Key.name}</b> contains custom listeners. " +
                    "These listeners will not function once exported to mtion studio.\n";
                for (var i = 0; i < kvp.Value.Count; ++i)
                {
                    warningMessage += $"<b>{kvp.Value[i]}</b>";
                    if (i != kvp.Value.Count - 1)
                    {
                        warningMessage += ", ";
                    }
                }

                MTIONSDKToolsWindow.StartBox();
                MTIONSDKToolsWindow.DrawWarning(warningMessage);
                MTIONSDKToolsWindow.EndBox();
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

                MTIONSDKToolsWindow.StartBox();
                MTIONSDKToolsWindow.DrawWarning(warningMessage);
                MTIONSDKToolsWindow.EndBox();
            }
        }

        private static void GenerateMissingMetadataWarnings()
        {
            var missingMetadatas = ComponentVerificationUtil.GetSDKObjectsWithMissingMetadata();
            foreach (var sdkObject in missingMetadatas.Keys)
            {
                var warningMessage = $"<b>{(string.IsNullOrEmpty(sdkObject.Name) ? sdkObject.InternalID : sdkObject.Name)}</b> ({sdkObject.ObjectType}) has empty fields: ";
                for (int i = 0; i < missingMetadatas[sdkObject].Count; ++i)
                {
                    warningMessage += $"<b>{missingMetadatas[sdkObject][i]}</b>";
                    if (i != missingMetadatas[sdkObject].Count - 1)
                    {
                        warningMessage += ", ";
                    }
                }
                warningMessage += ".";

                MTIONSDKToolsWindow.StartBox();
                MTIONSDKToolsWindow.DrawWarning(warningMessage);
                MTIONSDKToolsWindow.EndBox();
            }
        }

        private static void GenerateMissingScriptsWarnings()
        {
            var objectsWithMissingScripts = SceneVerificationUtil.GetGameObjectsWithMissingScripts();
            foreach (var go in objectsWithMissingScripts)
            {
                var warningMessage = $"GameObject <b>{go.name}</b> has missing scripts";

                MTIONSDKToolsWindow.StartBox();
                MTIONSDKToolsWindow.DrawWarning(warningMessage);
                MTIONSDKToolsWindow.EndBox();
            }
        }

        private static void GenerateMissingColliderWarnings()
        {
            var objectsWithoutColliders = ComponentVerificationUtil.GetSDKObjectsWithoutColliders();
            foreach (var sdkObject in objectsWithoutColliders)
            {
                var warningMessage = $"<b>{(string.IsNullOrEmpty(sdkObject.Name) ? sdkObject.InternalID : sdkObject.Name)}</b> does not have a collider.";

                MTIONSDKToolsWindow.StartBox();
                MTIONSDKToolsWindow.DrawWarning(warningMessage);
                MTIONSDKToolsWindow.EndBox();
            }
        }

        private static void GenerateActionEmptyEntryPointWarnings()
        {
            var actionsWithEmptyEntryPoints = ComponentVerificationUtil.GetActionBehavioursEmptyEntryPoints();
            foreach (var action in actionsWithEmptyEntryPoints)
            {
                var warningMessage = $"Action <b>{action.ActionName}</b> has no entry points or empty entry points. Fix this in the \"Actions\" tab.";

                MTIONSDKToolsWindow.StartBox();
                MTIONSDKToolsWindow.DrawWarning(warningMessage);
                MTIONSDKToolsWindow.EndBox();
            }
        }

        private static void GenerateMissingRagdollAvatarWarning()
        {
            var ragdollConfigured = SceneVerificationUtil.IsRagdollConfiguredForAvatar();
            if (!ragdollConfigured)
            {
                var warningMessage = $"Ragdoll has not been configured for this avatar. Set up the ragdoll in the <b>Ragdoll</b> tab.";

                MTIONSDKToolsWindow.StartBox();
                MTIONSDKToolsWindow.DrawWarning(warningMessage);
                MTIONSDKToolsWindow.EndBox();
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
                        MTIONSDKToolsWindow.StartBox();
                        MTIONSDKToolsWindow.DrawWarning(
                            $"Asset <b>{tracker.gameObject.name}</b> contains Action {action.GetType().Name}. Please remove it. Actions are not allowed on objects inside clubhouse templates.");
                        MTIONSDKToolsWindow.EndBox();
                    }
                }
            }

            if (sceneDescriptorObject != null)
            {
                IAction[] actions = sceneDescriptorObject.ObjectReference.GetComponentsInChildren<IAction>();
                if (actions.Length > 0)
                {
                    MTIONSDKToolsWindow.StartBox();
                    MTIONSDKToolsWindow.DrawWarning($"{actions.Length} Action scripts found in the scene. Actions can only be added to assets. Fix this by removing any Action Components from the scene.");
                    MTIONSDKToolsWindow.EndBox();
                }
            }
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
