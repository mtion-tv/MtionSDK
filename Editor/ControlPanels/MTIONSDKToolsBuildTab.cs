using mtion.room.sdk.compiled;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using Asyncoroutine;
using mtion.room.sdk.customproperties;
using System.Linq;
using mtion.room.sdk.action;
using UnityEngine.AI;

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

        private static bool uploadErrors;
        private static bool uploadInProgress;
        private static DateTime uploadStartTime;
        private static float uploadProgress = 1;
        private static int uploadProgressId;

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
                    MTIONObjectType.MTIONSDK_ASSET);

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
            if (uploadInProgress)
            {
                int timeRemaining = Mathf.Max(1, Mathf.RoundToInt((DateTime.Now.Subtract(uploadStartTime).Seconds / uploadProgress) * (1 - uploadProgress)));
                Progress.SetRemainingTime(uploadProgressId, timeRemaining);
                Progress.Report(uploadProgressId, uploadProgress, "Uploading room scene to server...");
            }

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

                StartBox();
                {
                    GUILayout.Label("Settings", headerLabelStyle);
                    GUILayout.Space(10);

                    bool changesMade = false;
                    var descriptorGO = BuildManager.GetSceneDescriptor();
                    var descriptor = descriptorGO.GetComponent<MTIONSDKDescriptorSceneBase>();

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
                EndBox();

                StartBox();
                {
                    GUILayout.Label("Options", headerLabelStyle);
                    GUILayout.Space(10);

                    bool changesMade = false;
                    var descriptorGO = BuildManager.GetSceneDescriptor();
                    var descriptor = descriptorGO.GetComponent<MTIONSDKDescriptorSceneBase>();

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

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent("Export Location", "PersistentStorage: stores assets to the AppData folder on C drive. \nStreamingAssets: stores built asset to the StreamingAssets folder in Unity project"), textFieldLabelStyle);
                    var locOpts = descriptor.LocationOption;
                    descriptor.LocationOption = (ExportLocationOptions)EditorGUILayout.Popup(
                        (int)descriptor.LocationOption, Enum.GetNames(typeof(ExportLocationOptions)), popupStyle);
                    changesMade |= locOpts != descriptor.LocationOption;
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);

                    if (descriptor.ObjectType == MTIONObjectType.MTIONSDK_ROOM)
                    {
                        var roomSDKObject = GameObject.FindObjectOfType<MTIONSDKRoom>();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label(new GUIContent("Environment ID", "Place the environmet ID here to link an environment to the current Room asset."), textFieldLabelStyle);
                        var defaultEnv = roomSDKObject.EnvironmentInternalID;
                        roomSDKObject.EnvironmentInternalID = EditorGUILayout.TextField(roomSDKObject.EnvironmentInternalID, textFieldStyle);
                        changesMade |= defaultEnv != roomSDKObject.EnvironmentInternalID;
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.Space(10);
                    }
                    else if (descriptor.ObjectType == MTIONObjectType.MTIONSDK_ENVIRONMENT)
                    {
                        var environmentSDKObject = GameObject.FindObjectOfType<MTIONSDKEnvironment>();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label(new GUIContent("Environment ID", "Use this ID to link a room to this scene environment"), textFieldLabelStyle);
                        EditorGUILayout.TextField(environmentSDKObject.InternalID, sizelessTextFieldStyle,
                            GUILayout.Height(22), GUILayout.Width(450 - 22));

                        Texture duplicateIcon = Resources.Load<Texture>("Icons/duplicate-icon");

                        if (GUILayout.Button(duplicateIcon, MTIONSDKToolsWindow.SmallButtonStyle, GUILayout.Width(22)))
                        {
                            EditorGUIUtility.systemCopyBuffer = environmentSDKObject.InternalID;
                            Debug.Log($"\"{environmentSDKObject.InternalID}\" copied to clipboard");
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.Space(10);
                    }


#if ENABLE_SDK_AVATAR_FEATURE
                    if (descriptor.ObjectType == MTIONObjectType.MTIONSDK_ROOM ||
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
#endif

                    if (changesMade)
                    {
                        EditorUtility.SetDirty(descriptor);
                        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                    }
                }
                EndBox();


                CreateCustomPropertiesTable();

                GenerateWarningsAndErrors();

                GUILayout.BeginHorizontal();
                {
                    var buildScene = GUILayout.Button(new GUIContent("Build Scene", "Processes and bundles scene into package for mtion studio"), MTIONSDKToolsWindow.LargeButtonStyle);
                    if (buildScene && _buildErrorsExist)
                    {
                        EditorUtility.DisplayDialog("Build errors found", "Please fix all errors indicated in the \"Build\" tab before building.", "Close");
                    }
                    else if (buildScene)
                    {
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
                    var descriptor = BuildManager.GetSceneDescriptor().GetComponent<MTIONSDKDescriptorSceneBase>();
                    var buildPath = SDKUtil.GetSDKItemDirectory(descriptor, descriptor.LocationOption);
                    var unityPath = SDKUtil.GetSDKLocalUnityBuildPath(descriptor, descriptor.LocationOption);
                    var webglPath = SDKUtil.GetSDKLocalWebGLBuildPath(descriptor, descriptor.LocationOption);
                    var id = descriptor.InternalID;
                }
            }
        }

        private static void CreateRoomInstantiationOptions()
        {
            StartBox();
            {
                var warningMessage = "MTIONRoom or MTIONEnvironment prefab is not detected in scene.\n" +
                    "Ensure that you initialize the scene first before proceeding.\n" +
                    "<b>This will modfiy the currently opened scene.</b>";
                MTIONSDKToolsWindow.DrawWarning(warningMessage);
            }
            EndBox();

            StartBox();
            {
                {
                    if (GUILayout.Button(new GUIContent("Create Room Scene", "Initializes current scene to build out a mtion room"), MTIONSDKToolsWindow.LargeButtonStyle))
                    {
                        CreateRoomScene();
                    }

                    if (GUILayout.Button(new GUIContent("Create Environment Scene", "Initializes current scene build out a mtion environment"), MTIONSDKToolsWindow.LargeButtonStyle))
                    {
                        CreateEnvironmentScene();
                    }
                }
                {
                    if (GUILayout.Button(new GUIContent("Create Asset Scene", "Initializes current scene to build out a mtion asset"), MTIONSDKToolsWindow.LargeButtonStyle))
                    {
                        CreateAssetScene();
                    }


#if ENABLE_SDK_AVATAR_FEATURE
                    if (GUILayout.Button(new GUIContent("Create Avatar Scene", "Initializes current scene to build out a mtion avatar"), MTIONSDKToolsWindow.LargeButtonStyle))
                    {
                        CreateAvatarScene();
                    }
#endif

                }
            }
            EndBox();
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

            StartBox();
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
            EndBox();
        }


        private static IEnumerator PublishSceneEnvironment(MTIONSDKDescriptorSceneBase descriptor, string userGUID)
        {
            var scenePath = SDKUtil.GetSDKItemDirectory(descriptor, descriptor.LocationOption);
            if (!Directory.Exists(scenePath))
            {
                Debug.LogError($"Could not find directory to upload: {scenePath}");
                yield break;
            }

            var aws = new compiled.AWSUtil();

            uploadErrors = false;
            uploadInProgress = true;
            uploadProgress = 0;
            uploadStartTime = DateTime.Now;
            uploadProgressId = Progress.Start("Uploading Room Scene");
            yield return aws.UploadDirectory(descriptor, userGUID, progress => uploadProgress = progress).AsCoroutine();

            Progress.Remove(uploadProgressId);
            uploadInProgress = false;

            EditorUtility.DisplayDialog("Success", $"Upload of {descriptor.Name} completed.", "OK");
        }


        private static void CreateRoomScene()
        {
            GameObject RoomDescriptorObject = new GameObject("MTIONRoomDescriptor");
            RoomDescriptorObject.transform.position = Vector3.zero;
            RoomDescriptorObject.transform.rotation = Quaternion.identity;
            var sdkcomp = RoomDescriptorObject.AddComponent<MTIONSDKRoom>();

            var scene = EditorSceneManager.GetActiveScene();
            SDKEditorUtil.InitAddressableAssetFields(sdkcomp, MTIONObjectType.MTIONSDK_ROOM, scene.name);
            AssetDatabase.SaveAssets();
            GenerateMTIONScene(sdkcomp, scene);
        }

        private static void CreateEnvironmentScene()
        {
            GameObject RoomDescriptorObject = new GameObject("MTIONEnvironmentDescriptor");
            RoomDescriptorObject.transform.position = Vector3.zero;
            RoomDescriptorObject.transform.rotation = Quaternion.identity;
            var sdkcomp = RoomDescriptorObject.AddComponent<MTIONSDKEnvironment>();

            var scene = EditorSceneManager.GetActiveScene();
            SDKEditorUtil.InitAddressableAssetFields(sdkcomp, MTIONObjectType.MTIONSDK_ENVIRONMENT, scene.name);
            AssetDatabase.SaveAssets();
            GenerateMTIONScene(sdkcomp, scene);
        }

        private static void CreateAssetScene()
        {
            GameObject RoomDescriptorObject = new GameObject("MTIONAssetDescriptor");
            RoomDescriptorObject.transform.position = Vector3.zero;
            RoomDescriptorObject.transform.rotation = Quaternion.identity;
            var sdkcomp = RoomDescriptorObject.AddComponent<MTIONSDKAsset>();

            var scene = EditorSceneManager.GetActiveScene();
            SDKEditorUtil.InitAddressableAssetFields(sdkcomp, MTIONObjectType.MTIONSDK_ASSET, scene.name);
            AssetDatabase.SaveAssets();
            GenerateMTIONScene(sdkcomp, scene);
        }

        private static void CreateAvatarScene()
        {
            GameObject avatarDescriptorObject = new GameObject("MTIONAvatarDescriptor");
            avatarDescriptorObject.transform.position = Vector3.zero;
            avatarDescriptorObject.transform.rotation = Quaternion.identity;
            var sdkcomp = avatarDescriptorObject.AddComponent<MTIONSDKAvatar>();

            var scene = EditorSceneManager.GetActiveScene();
            SDKEditorUtil.InitAddressableAssetFields(sdkcomp, MTIONObjectType.MTIONSDK_AVATAR, scene.name);
            AssetDatabase.SaveAssets();
            GenerateMTIONScene(sdkcomp, scene);
        }

        private static void GenerateMTIONScene(MTIONSDKDescriptorSceneBase descriptor, Scene scene)
        {
            string descriptorObjectType = MTIONSDKAssetBase.ConvertObjectTypeToString(descriptor.ObjectType);
            string scenePath = scene.path;

            Camera mainCamera = null;
            foreach (var rootGO in scene.GetRootGameObjects())
            {
                if (rootGO.GetComponent<Camera>())
                {
                    mainCamera = rootGO.GetComponent<Camera>();
                }
            }
            if (mainCamera == null)
            {
                GameObject camGO = new GameObject("Main Camera");
                mainCamera = camGO.AddComponent<Camera>();
            }

            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 1.93f;
            mainCamera.transform.position = new Vector3(-3.31999993f, 2.3599999f, 2.93000007f);
            mainCamera.transform.rotation = new Quaternion(-0.0752960071f, -0.872711599f, 0.142591402f, -0.460839152f);
            mainCamera.transform.parent = descriptor.gameObject.transform;

            var forceObjectReferenceCration = descriptor.ObjectReferenceProp;

            if (descriptor.ObjectType == MTIONObjectType.MTIONSDK_ROOM)
            {
                var roomDescriptor = descriptor as MTIONSDKRoom;
                if (roomDescriptor.SDKRoot == null)
                {
                    roomDescriptor.SDKRoot = new GameObject("SDK PROPS");
                    roomDescriptor.SDKRoot.transform.localPosition = Vector3.zero;
                    roomDescriptor.SDKRoot.transform.localRotation = Quaternion.identity;
                    roomDescriptor.SDKRoot.transform.localScale = Vector3.one;
                }
            }
        }


        private static void GenerateWarningsAndErrors()
        {
            _buildErrorsExist = false;
            _buildErrorsExist |= GenerateIncorrectNumCamerasError();
            _buildErrorsExist |= GenerateMissingColliderOnRigidbodyError();
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

                StartBox();
                MTIONSDKToolsWindow.DrawWarning(warningMessage, MTIONSDKToolsWindow.WarningType.ERROR);
                EndBox();

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

                StartBox();
                MTIONSDKToolsWindow.DrawWarning(warningMessage, MTIONSDKToolsWindow.WarningType.ERROR);
                EndBox();

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

                StartBox();
                MTIONSDKToolsWindow.DrawWarning(warningMessage, MTIONSDKToolsWindow.WarningType.ERROR);
                EndBox();
            }

            return gameObjectsMissingCollidersWithRb.Count > 0;
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

                StartBox();
                MTIONSDKToolsWindow.DrawWarning(warningMessage);
                EndBox();
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

                StartBox();
                MTIONSDKToolsWindow.DrawWarning(warningMessage);
                EndBox();
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

                StartBox();
                MTIONSDKToolsWindow.DrawWarning(warningMessage);
                EndBox();
            }
        }

        private static void GenerateMissingScriptsWarnings()
        {
            var objectsWithMissingScripts = SceneVerificationUtil.GetGameObjectsWithMissingScripts();
            foreach (var go in objectsWithMissingScripts)
            {
                var warningMessage = $"GameObject <b>{go.name}</b> has missing scripts";

                StartBox();
                MTIONSDKToolsWindow.DrawWarning(warningMessage);
                EndBox();
            }
        }

        private static void GenerateMissingColliderWarnings()
        {
            var objectsWithoutColliders = ComponentVerificationUtil.GetSDKObjectsWithoutColliders();
            foreach (var sdkObject in objectsWithoutColliders)
            {
                var warningMessage = $"<b>{(string.IsNullOrEmpty(sdkObject.Name) ? sdkObject.InternalID : sdkObject.Name)}</b> does not have a collider.";

                StartBox();
                MTIONSDKToolsWindow.DrawWarning(warningMessage);
                EndBox();
            }
        }

        private static void GenerateActionEmptyEntryPointWarnings()
        {
            var actionsWithEmptyEntryPoints = ComponentVerificationUtil.GetActionBehavioursEmptyEntryPoints();
            foreach (var action in actionsWithEmptyEntryPoints)
            {
                var warningMessage = $"Action <b>{action.ActionName}</b> has no entry points or empty entry points. Fix this in the \"Actions\" tab.";

                StartBox();
                MTIONSDKToolsWindow.DrawWarning(warningMessage);
                EndBox();
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
                        StartBox();
                        MTIONSDKToolsWindow.DrawWarning(
                            $"Asset <b>{tracker.gameObject.name}</b> contains Action {action.GetType().Name}. Please remove it. Actions are not allowed on objects inside clubhouse templates.");
                        EndBox();
                    }
                }
            }

            if (sceneDescriptorObject != null)
            {
                IAction[] actions = sceneDescriptorObject.ObjectReference.GetComponentsInChildren<IAction>();
                if (actions.Length > 0)
                {
                    StartBox();
                    MTIONSDKToolsWindow.DrawWarning($"{actions.Length} Action scripts found in the scene. Actions can only be added to assets. Fix this by removing any Action Components from the scene.");
                    EndBox();
                }
            }
        }


        private static void StartBox()
        {
            GUIStyle modifiedBox = GUI.skin.GetStyle("Box");
            modifiedBox.normal.background = MTIONSDKToolsWindow.CreateTextureForColor(1, 1, new Color(0.14f, 0.14f, 0.14f));

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
