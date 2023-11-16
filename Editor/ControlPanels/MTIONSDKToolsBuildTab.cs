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

        // Progress Bar
        private static bool uploadErrors;
        private static bool uploadInProgress;
        private static DateTime uploadStartTime;
        private static float uploadProgress = 1;
        private static int uploadProgressId;

        public static void Refresh()
        {
            allCustomPropertiesContainers.Clear();

            var _sceneDescriptorObject = GameObject.FindObjectOfType<MTIONSDKDescriptorSceneBase>();
            if (_sceneDescriptorObject == null) 
            {
                return;
            }

            SceneVerificationUtil.VerifySceneIntegrity(_sceneDescriptorObject);
            ComponentVerificationUtil.VerifyAllComponentsIntegrity(_sceneDescriptorObject, MTIONObjectType.MTIONSDK_ASSET);
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
            // independent of actual window
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
                // Options
                StartBox();
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

                    GUILayout.Label("Options", headerLabelStyle);
                    GUILayout.Space(10);

                    bool changesMade = false;
                    var descriptorGO = BuildManager.GetSceneDescriptor();
                    var descriptor = descriptorGO.GetComponent<MTIONSDKDescriptorSceneBase>();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Name", textFieldLabelStyle);
                    var name = descriptor.Name;
                    descriptor.Name = GUILayout.TextField(descriptor.Name, textFieldStyle);
                    changesMade |= name != descriptor.Name;
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Description", textFieldLabelStyle);
                    var desc = descriptor.Description;
                    descriptor.Description = GUILayout.TextField(descriptor.Description, textFieldStyle);
                    changesMade |= desc != descriptor.Description;
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Version", textFieldLabelStyle);
                    var version = descriptor.Version;
                    descriptor.Version = EditorGUILayout.FloatField(descriptor.Version, textFieldStyle);
                    changesMade |= version != descriptor.Version;
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Export Location", textFieldLabelStyle);
                    var locOpts = descriptor.LocationOption;
                    descriptor.LocationOption = (ExportLocationOptions)EditorGUILayout.Popup(
                        (int)descriptor.LocationOption, Enum.GetNames(typeof(ExportLocationOptions)), popupStyle);
                    changesMade |= locOpts != descriptor.LocationOption;
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);



                    // Environment Scene
                    if (descriptor.ObjectType == MTIONObjectType.MTIONSDK_ROOM)
                    {
                        var roomSDKObject = GameObject.FindObjectOfType<MTIONSDKRoom>();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Environment ID", textFieldLabelStyle);
                        var defaultEnv = roomSDKObject.EnvironmentInternalID;
                        roomSDKObject.EnvironmentInternalID = EditorGUILayout.TextField(roomSDKObject.EnvironmentInternalID, textFieldStyle);
                        changesMade |= defaultEnv != roomSDKObject.EnvironmentInternalID;
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                    }

                    if (changesMade)
                    {
                        EditorUtility.SetDirty(descriptor);
                        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                    }
                }
                EndBox();


                // Draw custom properties
                CreateCustomPropertiesTable();

                // Draw warnings
                GenerateWarningsAndErrors();

                // Buttons to create and add mtion sdk components
                GUILayout.BeginHorizontal();
                {
                    var buildScene = GUILayout.Button("Build Scene", MTIONSDKToolsWindow.LargeButtonStyle);
                    if (buildScene && _buildErrorsExist)
                    {
                        EditorUtility.DisplayDialog("Build errors found", "Please fix all errors indicated in the \"Build\" tab before building.", "Close");
                    }
                    else if (buildScene)
                    {
                        // Verify that the scene is valid
                        var roomSDKObject = GameObject.FindObjectOfType<MTIONSDKRoom>();
                        if (roomSDKObject != null)
                        {
                            Debug.Assert(roomSDKObject.GUID != null);
                            roomSDKObject.ObjectType = MTIONObjectType.MTIONSDK_ROOM;
                            AssetDatabase.SaveAssets();
                        }

                        // Verify that the scene is valid
                        var envSDKObject = GameObject.FindObjectOfType<MTIONSDKEnvironment>();
                        if (envSDKObject != null)
                        {
                            Debug.Assert(envSDKObject.GUID != null);
                            envSDKObject.ObjectType = MTIONObjectType.MTIONSDK_ENVIRONMENT;
                            AssetDatabase.SaveAssets();
                        }

                        // Verify that the scene is valid
                        var assetSDKObject = GameObject.FindObjectOfType<MTIONSDKAsset>();
                        if (assetSDKObject != null)
                        {
                            Debug.Assert(assetSDKObject.GUID != null);
                            assetSDKObject.ObjectType = MTIONObjectType.MTIONSDK_ASSET;
                            AssetDatabase.SaveAssets();
                        }

                        // Migrate
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

                // Publish Sequence
                if (BuildManager.GetSceneDescriptor() != null)
                {
                    var descriptor = BuildManager.GetSceneDescriptor().GetComponent<MTIONSDKDescriptorSceneBase>();
                    var buildPath = SDKUtil.GetSDKItemDirectory(descriptor, descriptor.LocationOption);
                    var unityPath = SDKUtil.GetSDKLocalUnityBuildPath(descriptor, descriptor.LocationOption);
                    var webglPath = SDKUtil.GetSDKLocalWebGLBuildPath(descriptor, descriptor.LocationOption);
                    var id = descriptor.InternalID;

                    // Check for files
                    bool build_configured = true;
                    build_configured &= Directory.Exists(buildPath);
                    build_configured &= File.Exists($"{buildPath}/config.json");
                    build_configured &= File.Exists($"{webglPath}/GLTF-{id}.glb");
                    build_configured &= File.Exists($"{unityPath}/{SDKUtil.GetBuildTargetDirectory(SDKBuildTarget.StandaloneWindows)}/catalog.json");

                    // TODO: Remove references to publishing, use SERVER TOOLS in place
                    //if (build_configured)
                    //{
                    //    StartBox();
                    //    {
                    //        GUIStyle style = new GUIStyle();
                    //        style.padding = new RectOffset(0, 0, 0, 0);

                    //        GUIStyle Label = new GUIStyle();
                    //        Label.alignment = TextAnchor.MiddleLeft;
                    //        Label.name = "Label";
                    //        Label.normal.textColor = Color.gray;
                    //        Label.padding = new RectOffset(3, 3, 3, 3);
                    //        Label.richText = true;
                    //        Label.wordWrap = true;

                    //        EditorGUILayout.BeginHorizontal(style);
                    //        GUIContent content = new GUIContent();
                    //        content.text = "Server and Publishing Options";

                    //        GUILayout.Label(content, Label);
                    //        EditorGUILayout.EndHorizontal();


                    //        GUIStyle ButtonStyle = new GUIStyle();
                    //        ButtonStyle.alignment = TextAnchor.MiddleCenter;
                    //        ButtonStyle.border = new RectOffset(1, 1, 1, 1);
                    //        ButtonStyle.name = "Button";
                    //        ButtonStyle.onNormal.background = ButtonStyle.onNormal.background;
                    //        ButtonStyle.onNormal.textColor = ButtonStyle.normal.textColor;
                    //        ButtonStyle.active.textColor = ButtonStyle.normal.textColor;
                    //        ButtonStyle.onActive.background = ButtonStyle.active.background;
                    //        ButtonStyle.onActive.textColor = ButtonStyle.normal.textColor;
                    //        ButtonStyle.focused.textColor = ButtonStyle.normal.textColor;
                    //        ButtonStyle.onFocused.background = ButtonStyle.focused.background;
                    //        ButtonStyle.onFocused.textColor = ButtonStyle.normal.textColor;
                    //        ButtonStyle.hover.textColor = ButtonStyle.normal.textColor;
                    //        ButtonStyle.onHover.background = ButtonStyle.hover.background;
                    //        ButtonStyle.onHover.textColor = ButtonStyle.normal.textColor;

                    //        if (GUILayout.Button("Publish Scene", ButtonStyle))
                    //        {
                    //            var userAuth = BuildManager.GetUserAuthentication();

                    //            // Get Username GUID from account
                    //            var userGUID = userAuth.GetUserGUID();

                    //            // Get Room GUID
                    //            var roomGUID = descriptor.Descriptor.InternalID;

                    //            var roomDirectory = SDKUtil.GetSDKItemDirectory(descriptor, descriptor.LocationOption);

                    //            // Generate URI for AWS
                    //            var S3URI = SDKUtil.GenerateServerURI(
                    //                userGUID,
                    //                descriptor.Descriptor.InternalID,
                    //                descriptor.Descriptor.ObjectType);

                    //            // Setup AWS credentials
                    //            EditorCoroutineUtility.StartCoroutine(PublishSceneEnvironment(descriptor, userGUID), this);
                    //            Debug.Log(S3URI);
                    //        }

                    //        if (uploadInProgress)
                    //        {
                    //            ProgressBar(uploadProgress);
                    //        }

                    //    }
                    //    EndBox();
                    //}
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

            // Button Initialization
            StartBox();
            {
                // Buttons to create and add mtion sdk components
                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Create Room Scene", MTIONSDKToolsWindow.LargeButtonStyle))
                    {
                        CreateRoomScene();
                    }

                    if (GUILayout.Button("Create Environment Scene", MTIONSDKToolsWindow.LargeButtonStyle))
                    {
                        CreateEnvironmentScene();
                    }


                }
                GUILayout.EndHorizontal();

                if (GUILayout.Button("Create Asset Scene", MTIONSDKToolsWindow.LargeButtonStyle))
                {
                    CreateAssetScene();
                }
            }
            EndBox();
        }

        private static void CreateCustomPropertiesTable()
        {
            // This is done in case there are any stale references resulting from scene changes
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

                EditorGUILayout.LabelField("Property Name", entryLabelStyle, GUILayout.MaxWidth(largeLabelMaxWidth));
                EditorGUILayout.LabelField("Type", entryLabelStyle, GUILayout.MinWidth(smallLabelMinWidth));
                EditorGUILayout.LabelField("Asset", entryLabelStyle, GUILayout.MinWidth(smallLabelMinWidth));
                EditorGUILayout.LabelField("Component", entryLabelStyle, GUILayout.MaxWidth(largeLabelMaxWidth));
                EditorGUILayout.LabelField("Default Value", entryLabelStyle, GUILayout.MaxWidth(largeLabelMaxWidth));
                EditorGUILayout.LabelField("Min Value", entryLabelStyle, GUILayout.MinWidth(smallLabelMinWidth));
                EditorGUILayout.LabelField("Max Value", entryLabelStyle, GUILayout.MinWidth(smallLabelMinWidth));
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

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// Publish Room Methods
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

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

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// Room Creation Methods
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

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

        private static void GenerateMTIONScene(MTIONSDKDescriptorSceneBase descriptor, Scene scene)
        {
            string descriptorObjectType = SDKEditorUtil.ConvertObjectTypeToString(descriptor.ObjectType);
            string scenePath = scene.path;

            // Get Main camera
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

            // Position Main Camera to default location
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 1.93f;
            mainCamera.transform.position = new Vector3(-3.31999993f, 2.3599999f, 2.93000007f);
            mainCamera.transform.rotation = new Quaternion(-0.0752960071f, -0.872711599f, 0.142591402f, -0.460839152f);
            mainCamera.transform.parent = descriptor.gameObject.transform;


            // Create plane for intial starting room
            GameObject roomVizGO = new GameObject($"{descriptorObjectType}BaseVisualization");
            roomVizGO.transform.position = Vector3.zero;
            roomVizGO.transform.rotation = Quaternion.identity;
            roomVizGO.hideFlags = HideFlags.NotEditable;
            roomVizGO.transform.parent = descriptor.gameObject.transform;

            GameObject planeGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            planeGO.transform.position = Vector3.zero;
            planeGO.transform.rotation = Quaternion.Euler(new Vector3(90, 0, 0));
            planeGO.transform.localScale = Vector3.one * 6;
            planeGO.GetComponent<Renderer>().material = (Material)Resources.Load("Materials/GridMat", typeof(Material));
            planeGO.hideFlags = HideFlags.NotEditable;
            planeGO.transform.parent = roomVizGO.transform;

            // Create main room prefab
            GameObject roomGO = new GameObject($"MTION {descriptorObjectType.ToUpper()}");
            roomGO.transform.position = Vector3.zero;
            roomGO.transform.rotation = Quaternion.identity;
            descriptor.ObjectReference = roomGO;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// Build warnings and errors
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private static void GenerateWarningsAndErrors()
        {
            _buildErrorsExist = false;
            _buildErrorsExist |= GenerateIncorrectNumCamerasError();
            _buildErrorsExist |= GenerateMissingColliderOnRigidbodyError();
            GenerateDuplicatePropertyNameWarnings();
            GenerateMissingMetadataWarnings();
            GenerateMissingScriptsWarnings();
            GenerateMissingColliderWarnings();
            GenerateActionEmptyEntryPointWarnings();
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
            else if (numCameras > 1)
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

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// UI
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private static void StartBox()
        {
            // Setup a new modified box skin for Unity's new GUI
            GUIStyle modifiedBox = GUI.skin.GetStyle("Box");
            modifiedBox.normal.background = MTIONSDKToolsWindow.CreateTextureForColor(1, 1, new Color(0.14f, 0.14f, 0.14f));

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