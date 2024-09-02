using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using mtion.room.sdk;
using System.IO;
using mtion.room.sdk.compiled;
using System.Linq;
using mtion.room.sdk.action;
using mtion.room.sdk.customproperties;

[System.Serializable]
public struct VirtualCameraView
{
    public int Order;
    public string name;
}

namespace mtion.room.sdk
{
    public static class MTIONSDKToolsAssetTab
    {
        public static ReorderableList _cameraViewHints = null;
        public static List<MVirtualCameraEventTracker> _virtualcameraEvents = new List<MVirtualCameraEventTracker>();

        public static ReorderableList _displayComponentRList = null;
        public static List<MVirtualDisplayTracker> _displayComponents = new List<MVirtualDisplayTracker>();

        public static ReorderableList _lightingComponentRList = null;
        public static List<MVirtualLightingTracker> _lightingComponents = new List<MVirtualLightingTracker>();

        public static ReorderableList _assetComponentRList = null;
        public static List<MVirtualAssetTracker> _assetComponents = new List<MVirtualAssetTracker>();

        private static MTIONSDKRoom _roomSDKDescriptorObject = null;
        private static Vector2 _scrollPos;

        private static bool _openCameraFoldout;
        private static bool _openDisplayFoldout;
        private static bool _openLightsFoldout;
        private static bool _openAssetsFoldout;

        public static void Initialize()
        {
            if (_roomSDKDescriptorObject == null)
            {
                _roomSDKDescriptorObject = GameObject.FindObjectOfType<MTIONSDKRoom>();
            }

            if (_cameraViewHints == null)
            {
                _cameraViewHints = new ReorderableList(_virtualcameraEvents,
                                   typeof(VirtualCameraView), true, true, false, false);
                _cameraViewHints.showDefaultBackground = false;

                _cameraViewHints.drawHeaderCallback = (Rect rect) =>
                {
                    EditorGUI.LabelField(rect, "Name", MTIONSDKToolsWindow.ListHeaderStyle);
                    EditorGUI.LabelField(new Rect(rect.x + 190, rect.y, 120, EditorGUIUtility.singleLineHeight), "Shortcut", MTIONSDKToolsWindow.ListHeaderStyle);
                };

                _cameraViewHints.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    MVirtualCameraEventTracker vc = (MVirtualCameraEventTracker)_cameraViewHints.list[index];
                    if (vc == null) return;

                    rect.y += 2;
                    EditorGUI.LabelField(new Rect(rect.x, rect.y, 200, EditorGUIUtility.singleLineHeight), vc.name, MTIONSDKToolsWindow.LabelStyle);

                    vc.UNITYEDITOR_KeyCodeNum = EditorGUI.IntField(
                        new Rect(rect.x += 175, rect.y, 30, EditorGUIUtility.singleLineHeight), vc.UNITYEDITOR_KeyCodeNum, MTIONSDKToolsWindow.TextFieldStyle);
                    if (vc.UNITYEDITOR_KeyCodeNum < 1) vc.UNITYEDITOR_KeyCodeNum = 1;
                    if (vc.UNITYEDITOR_KeyCodeNum > 4) vc.UNITYEDITOR_KeyCodeNum = 4;
                    if (vc.UNITYEDITOR_KeyCodeNum > vc.CameraParams.KeyCodeList.Count)
                    {
                        int delta = vc.UNITYEDITOR_KeyCodeNum - vc.CameraParams.KeyCodeList.Count;
                        for (int i = 0; i < delta; ++i)
                        {
                            vc.CameraParams.KeyCodeList.Add(mtion.input.sdk.compiled.KeyCodeCustomSubset.None);
                        }
                    }
                    else if (vc.UNITYEDITOR_KeyCodeNum < vc.CameraParams.KeyCodeList.Count)
                    {
                        for (int i = vc.CameraParams.KeyCodeList.Count - 1; i >= vc.UNITYEDITOR_KeyCodeNum; --i)
                        {
                            vc.CameraParams.KeyCodeList.RemoveAt(i);
                        }
                    }
                    Debug.Assert(vc.CameraParams.KeyCodeList.Count == vc.UNITYEDITOR_KeyCodeNum);

                    int w = 100;
                    int space = 15;
                    int startOffset = 25;
                    bool updated = false;
                    for (int i = 0; i < vc.CameraParams.KeyCodeList.Count; ++i)
                    {
                        int xoffset = (w + 5) * i;
                        var newKey = (mtion.input.sdk.compiled.KeyCodeCustomSubset)EditorGUI.EnumPopup(
                            new Rect(rect.x + startOffset + xoffset + space, rect.y, w, EditorGUIUtility.singleLineHeight), vc.CameraParams.KeyCodeList[i]);
                        if (newKey != vc.CameraParams.KeyCodeList[i])
                        {
                            updated = true;
                            vc.CameraParams.KeyCodeList[i] = newKey;
                        }
                    }

                    if (updated)
                    {
                        EditorUtility.SetDirty(vc);
                    }
                };

                _cameraViewHints.onReorderCallbackWithDetails = (ReorderableList list, int oldIndex, int newIndex) =>
                {
                    int index = 1;
                    foreach (var v in list.list)
                    {
                        var vc = v as MVirtualCameraEventTracker;
                        vc.OrderPrecedence = index;
                        index++;

                        EditorUtility.SetDirty(vc);
                    }
                };

                _cameraViewHints.onSelectCallback = (ReorderableList list) =>
                {
                    var vc = (MVirtualCameraEventTracker)list.list[list.index];
                    Selection.activeObject = vc.gameObject;
                };

            }

            if (_displayComponentRList == null)
            {
                _displayComponentRList = new ReorderableList(_displayComponents,
                    typeof(MVirtualDisplayTracker), true, true, false, false);
                _displayComponentRList.showDefaultBackground = false;

                _displayComponentRList.drawHeaderCallback = (Rect rect) =>
                {
                    EditorGUI.LabelField(rect, "Name", MTIONSDKToolsWindow.ListHeaderStyle);

                    int width = 150;
                    int spacing = 5;
                    rect.x += (width + spacing) + 40;
                    EditorGUI.LabelField(rect, "Shortcut (Attached Camera)", MTIONSDKToolsWindow.ListHeaderStyle);
                };

                _displayComponentRList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    bool updated = false;

                    MVirtualDisplayTracker display = (MVirtualDisplayTracker)_displayComponentRList.list[index];
                    if (display == null) return;

                    float xpos = rect.x;
                    int spacing = 5;

                    int width = 150;
                    rect.y += 2;

                    EditorGUI.LabelField(new Rect(xpos, rect.y, width, EditorGUIUtility.singleLineHeight), display.name, MTIONSDKToolsWindow.LabelStyle);

                    xpos += width + spacing + 25;
                    width = 30;
                    display.UNITYEDITOR_KeyCodeNum = EditorGUI.IntField(
                        new Rect(xpos, rect.y, width, EditorGUIUtility.singleLineHeight), display.UNITYEDITOR_KeyCodeNum, MTIONSDKToolsWindow.TextFieldStyle);
                    if (display.UNITYEDITOR_KeyCodeNum < 1) display.UNITYEDITOR_KeyCodeNum = 1;
                    if (display.UNITYEDITOR_KeyCodeNum > 4) display.UNITYEDITOR_KeyCodeNum = 4;

                    if (display.UNITYEDITOR_KeyCodeNum > display.DisplayParams.KeyCodeList.Count)
                    {
                        int delta = display.UNITYEDITOR_KeyCodeNum - display.DisplayParams.KeyCodeList.Count;
                        for (int i = 0; i < delta; ++i)
                        {
                            display.DisplayParams.KeyCodeList.Add(mtion.input.sdk.compiled.KeyCodeCustomSubset.None);
                        }
                    }
                    else if (display.UNITYEDITOR_KeyCodeNum < display.DisplayParams.KeyCodeList.Count)
                    {
                        for (int i = display.DisplayParams.KeyCodeList.Count - 1; i >= display.UNITYEDITOR_KeyCodeNum; --i)
                        {
                            display.DisplayParams.KeyCodeList.RemoveAt(i);
                        }
                    }
                    Debug.Assert(display.DisplayParams.KeyCodeList.Count == display.UNITYEDITOR_KeyCodeNum);


                    xpos += width + spacing;
                    width = 100;
                    for (int i = 0; i < display.DisplayParams.KeyCodeList.Count; ++i)
                    {
                        var newKey = (mtion.input.sdk.compiled.KeyCodeCustomSubset)EditorGUI.EnumPopup(new Rect(xpos, rect.y, width, EditorGUIUtility.singleLineHeight), display.DisplayParams.KeyCodeList[i]);
                        if (newKey != display.DisplayParams.KeyCodeList[i])
                        {
                            updated = true;
                            display.DisplayParams.KeyCodeList[i] = newKey;
                        }
                        xpos += width + spacing;
                    }

                    if (updated)
                    {
                        EditorUtility.SetDirty(display);
                    }
                };

                _displayComponentRList.onSelectCallback = (ReorderableList list) =>
                {
                    var d = (MVirtualDisplayTracker)list.list[list.index];
                    Selection.activeObject = d.gameObject;
                };
            }

            if (_lightingComponentRList == null)
            {
                _lightingComponentRList = new ReorderableList(_lightingComponents,
                    typeof(MVirtualLightingTracker), true, true, false, false);
                _lightingComponentRList.showDefaultBackground = false;

                _lightingComponentRList.drawHeaderCallback = (Rect rect) =>
                {
                    EditorGUI.LabelField(rect, "Name", MTIONSDKToolsWindow.ListHeaderStyle);

                    int width = 200;
                    int spacing = 5;

                    rect.x += width + spacing;
                    EditorGUI.LabelField(rect, "Lighting Type", MTIONSDKToolsWindow.ListHeaderStyle);
                };

                _lightingComponentRList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    bool updated = false;

                    MVirtualLightingTracker light = (MVirtualLightingTracker)_lightingComponentRList.list[index];
                    if (light == null) return;

                    float xpos = rect.x;
                    int spacing = 5;

                    int width = 185;
                    rect.y += 2;

                    EditorGUI.LabelField(new Rect(xpos, rect.y, width, EditorGUIUtility.singleLineHeight), 
                        light.name, MTIONSDKToolsWindow.LabelStyle);

                    xpos += width + spacing;
                    width = 150;
                    var previousType = light.LightParams.LightType;
                    light.LightParams.LightType = (LightingComponentType)EditorGUI.EnumPopup(
                        new Rect(xpos, rect.y, width, EditorGUIUtility.singleLineHeight),
                        light.LightParams.LightType);
                    if (previousType != light.LightParams.LightType)
                    {
                        updated = true;
                    }

                    if (updated)
                    {
                        EditorUtility.SetDirty(light);
                    }
                };

                _lightingComponentRList.onSelectCallback = (ReorderableList list) =>
                {
                    var d = (MVirtualLightingTracker)list.list[list.index];
                    Selection.activeObject = d.gameObject;
                };
            }

            if (_assetComponentRList == null)
            {
                _assetComponentRList = new ReorderableList(_assetComponents,
                    typeof(MVirtualAssetTracker), true, true, false, false);
                _assetComponentRList.showDefaultBackground = false;

                _assetComponentRList.drawHeaderCallback = (Rect rect) =>
                {
                    int width = 0;
                    int spacing = 0;

                    EditorGUI.LabelField(rect, "Name", MTIONSDKToolsWindow.ListHeaderStyle);

                    width = 250;
                    spacing = 40;
                    rect.x += width + spacing;
                    EditorGUI.LabelField(rect, "Asset Type", MTIONSDKToolsWindow.ListHeaderStyle);

                    width = 125;
                    spacing = 50;
                    rect.x += width + spacing;
                    EditorGUI.LabelField(rect, "Description", MTIONSDKToolsWindow.ListHeaderStyle);
                };

                _assetComponentRList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    bool updated = false;

                    MVirtualAssetTracker assetTracker = (MVirtualAssetTracker)_assetComponentRList.list[index];
                    if (assetTracker == null) return;

                    float xpos = rect.x;
                    int spacing = 5;

                    int width = 200;
                    rect.y += 2;

                    width = 250;
                    spacing = 25;
                    string cacheName = assetTracker.Name;
                    assetTracker.Name = EditorGUI.TextField(new Rect(xpos, rect.y, width, EditorGUIUtility.singleLineHeight),
                        assetTracker.Name, MTIONSDKToolsWindow.TextFieldStyle);
                    if (cacheName != assetTracker.Name)
                    {
                        updated = true;
                    }

                    xpos += width + spacing;
                    width = 150;
                    spacing = 25;
                    var previousDisplayType = assetTracker.AssetParams.VirtualObjectType;
                    assetTracker.AssetParams.VirtualObjectType = (VirtualObjectComponentType)EditorGUI.EnumPopup(
                        new Rect(xpos, rect.y, width, EditorGUIUtility.singleLineHeight), assetTracker.AssetParams.VirtualObjectType);
                    if (previousDisplayType != assetTracker.AssetParams.VirtualObjectType)
                    {
                        updated = true;
                    }

                    xpos += width + spacing;
                    width = 400;
                    string cacheDesc = assetTracker.Description;
                    assetTracker.Description = EditorGUI.TextField(new Rect(xpos, rect.y, width, EditorGUIUtility.singleLineHeight),
                        assetTracker.Description, MTIONSDKToolsWindow.TextFieldStyle);
                    if (cacheName != assetTracker.Description)
                    {
                        updated = true;
                    }


                    if (updated)
                    {
                        EditorUtility.SetDirty(assetTracker);
                    }
                };

                _assetComponentRList.onSelectCallback = (ReorderableList list) =>
                {
                    var d = (MVirtualAssetTracker)list.list[list.index];
                    Selection.activeObject = d.gameObject;
                };
            }

            RefreshList();
        }

        public static void Refresh()
        {
            RefreshList();

            _virtualcameraEvents.Sort(delegate (MVirtualCameraEventTracker x, MVirtualCameraEventTracker y)
            {
                if (x.OrderPrecedence < y.OrderPrecedence)
                {
                    return -1;
                }

                if (x.OrderPrecedence > y.OrderPrecedence)
                {
                    return 1;
                }

                return 0;
            });

            int index = 1;
            foreach (var vc in _virtualcameraEvents)
            {
                vc.OrderPrecedence = index;
                index++;
            }
        }

        private static void RefreshList()
        {
            if (_roomSDKDescriptorObject == null)
            {
                _roomSDKDescriptorObject = GameObject.FindObjectOfType<MTIONSDKRoom>();
            }

            if (_roomSDKDescriptorObject == null ||
               (_roomSDKDescriptorObject != null && _roomSDKDescriptorObject.ObjectType != MTIONObjectType.MTIONSDK_ROOM))
            {
                return;
            }


            if (_roomSDKDescriptorObject)
            {
                if (_roomSDKDescriptorObject.SDKRoot == null)
                {
                    _roomSDKDescriptorObject.SDKRoot = GameObject.Find("SDK PROPS");
                    if (_roomSDKDescriptorObject.SDKRoot == null)
                    {
                        _roomSDKDescriptorObject.SDKRoot = new GameObject("SDK PROPS");
                        _roomSDKDescriptorObject.SDKRoot.transform.localPosition = Vector3.zero;
                        _roomSDKDescriptorObject.SDKRoot.transform.localRotation = Quaternion.identity;
                        _roomSDKDescriptorObject.SDKRoot.transform.localScale = Vector3.one;
                    }
                }

                if (_roomSDKDescriptorObject.SDKRoot.transform.parent != null)
                {
                    _roomSDKDescriptorObject.SDKRoot.transform.parent = null;
                    int siblingIndex = _roomSDKDescriptorObject.ObjectReferenceProp.transform.GetSiblingIndex();
                    _roomSDKDescriptorObject.SDKRoot.transform.SetSiblingIndex(siblingIndex + 1);
                }
            }

            ComponentVerificationUtil.VerifyAllComponentsIntegrity(_roomSDKDescriptorObject, MTIONObjectType.MTIONSDK_CAMERA, false);
            var virtualCameraViews = GameObject.FindObjectsOfType<MVirtualCameraEventTracker>().OrderBy(x => x.OrderPrecedence).ToList();
            _virtualcameraEvents.Clear();
            _virtualcameraEvents.AddRange(virtualCameraViews);

            ComponentVerificationUtil.VerifyAllComponentsIntegrity(_roomSDKDescriptorObject, MTIONObjectType.MTIONSDK_DISPLAY, false);
            var virtualDisplays = GameObject.FindObjectsOfType<MVirtualDisplayTracker>();
            _displayComponents.Clear();
            _displayComponents.AddRange(virtualDisplays);

            ComponentVerificationUtil.VerifyAllComponentsIntegrity(_roomSDKDescriptorObject, MTIONObjectType.MTIONSDK_LIGHT, false);
            var virtualLights = GameObject.FindObjectsOfType<MVirtualLightingTracker>();
            _lightingComponents.Clear();
            _lightingComponents.AddRange(virtualLights);

            ComponentVerificationUtil.VerifyAllComponentsIntegrity(_roomSDKDescriptorObject, MTIONObjectType.MTIONSDK_ASSET, false);
            var virtualAssets = GameObject.FindObjectsOfType<MVirtualAssetTracker>();
            _assetComponents.Clear();
            _assetComponents.AddRange(virtualAssets);
        }

        public static void Draw()
        {
            if (_roomSDKDescriptorObject == null ||
               (_roomSDKDescriptorObject != null && _roomSDKDescriptorObject.ObjectType != MTIONObjectType.MTIONSDK_ROOM))
            {
                GenerateWarningGUI();
                return;
            }

            using (var scrollView = new EditorGUILayout.ScrollViewScope(_scrollPos))
            {
                _scrollPos = scrollView.scrollPosition;
                GenerateVirtualCameraGUI();
                GenerateUniversalDisplayGUI();
                GenerateLightingGUI();
                GenerateAssetComponentGUI();
            }
        }

        private static void GenerateWarningGUI()
        {
            StartBox();
            {
                MTIONSDKToolsWindow.DrawWarning("Disabled. Create a room scene under the \"Build\" tab to use the props tool.");
            }
            EndBox();
        }

        private static void GenerateUniversalDisplayGUI()
        {
            StartBox();
            {
                _openDisplayFoldout = EditorGUILayout.Foldout(_openDisplayFoldout, "Display Components", true, MTIONSDKToolsWindow.FoldoutStyle);
                if (_openDisplayFoldout)
                {
                    GUILayout.Space(10);

                    GUI.enabled = !Application.isPlaying;

                    _displayComponentRList.DoLayoutList();

                    if (GUILayout.Button("Add Display", MTIONSDKToolsWindow.MediumButtonStyle))
                    {
                        GameObject go = new GameObject("UniversalDisplay (" + (_displayComponents.Count + 1) + ")");
                        go.transform.position = GetNewObjectPosition();

                        if (_roomSDKDescriptorObject == null)
                        {
                            Debug.LogError("Scene not setup correctly.");
                            return;
                        }
                        go.transform.parent = _roomSDKDescriptorObject.SDKRoot.transform;


                        var d = go.AddComponent<MVirtualDisplayTracker>();
                        SDKEditorUtil.InitVirtualComponentFields(d);

                        d.gizmoDisplaySelection = 1;

                        _displayComponents.Add(d);
                    }

                    GUI.enabled = true;
                }
            }
            EndBox();
        }

        private static void GenerateLightingGUI()
        {
            StartBox();
            {
                _openLightsFoldout = EditorGUILayout.Foldout(_openLightsFoldout, "Light Components", true, MTIONSDKToolsWindow.FoldoutStyle);
                if (_openLightsFoldout)
                {
                    GUILayout.Space(10);

                    GUI.enabled = !Application.isPlaying;

                    _lightingComponentRList.DoLayoutList();

                    if (GUILayout.Button("Add Light", MTIONSDKToolsWindow.MediumButtonStyle))
                    {
                        GameObject go = new GameObject("Lighting Component (" + (_lightingComponents.Count + 1) + ")");
                        go.transform.position = GetNewObjectPosition();

                        if (_roomSDKDescriptorObject == null)
                        {
                            Debug.LogError("Scene not setup correctly.");
                            return;
                        }
                        go.transform.parent = _roomSDKDescriptorObject.SDKRoot.transform;

                        var light = go.AddComponent<MVirtualLightingTracker>();
                        SDKEditorUtil.InitVirtualComponentFields(light);

                        _lightingComponents.Add(light);
                    }

                    GUI.enabled = true;
                }
            }

            EndBox();
        }

        private static void GenerateVirtualCameraGUI()
        {
            StartBox();
            {
                GUI.enabled = !Application.isPlaying;

                _openCameraFoldout = EditorGUILayout.Foldout(_openCameraFoldout, "Virtual Cameras", true, MTIONSDKToolsWindow.FoldoutStyle);
                if (_openCameraFoldout)
                {
                    GUILayout.Space(10);

                    _cameraViewHints.DoLayoutList();

                    if (GUILayout.Button("Add Virtual Camera", MTIONSDKToolsWindow.MediumButtonStyle))
                    {
                        GameObject go = new GameObject("VirtualCameraElement (" + _virtualcameraEvents.Count + ")");
                        go.transform.position = GetNewObjectPosition();

                        if (_roomSDKDescriptorObject == null)
                        {
                            Debug.LogError("Scene not setup correctly.");
                            return;
                        }
                        go.transform.parent = _roomSDKDescriptorObject.SDKRoot.transform;

                        var vc = go.AddComponent<MVirtualCameraEventTracker>();
                        SDKEditorUtil.InitVirtualComponentFields(vc);

                        var numCams = GameObject.FindObjectsOfType<MVirtualCameraEventTracker>().Length;
                        vc.OrderPrecedence = numCams;

                        _virtualcameraEvents.Add(vc);
                    }
                }

                GUI.enabled = true;
            }
            EndBox();
        }

        private static void GenerateAssetComponentGUI()
        {
            StartBox();
            {
                GUI.enabled = !Application.isPlaying;

                _openAssetsFoldout = EditorGUILayout.Foldout(_openAssetsFoldout, "Virtual Assets", true, MTIONSDKToolsWindow.FoldoutStyle);
                if (_openAssetsFoldout)
                {
                    GUILayout.Space(10);

                    _assetComponentRList.DoLayoutList();

                    if (GUILayout.Button("Add 3D Asset", MTIONSDKToolsWindow.MediumButtonStyle))
                    {
                        GameObject go = new GameObject("VirtualAsset (" + _assetComponents.Count + ")");
                        go.transform.position = GetNewObjectPosition();

                        if (_roomSDKDescriptorObject == null)
                        {
                            Debug.LogError("Scene not setup correctly.");
                            return;
                        }
                        go.transform.parent = _roomSDKDescriptorObject.SDKRoot.transform;

                        go.AddComponent<CustomPropertiesContainer>();
                        var asset = go.AddComponent<MVirtualAssetTracker>();
                        asset.ObjectReference = go;
                        SDKEditorUtil.InitAddressableAssetFields(asset, MTIONObjectType.MTIONSDK_ASSET);

                        _assetComponents.Add(asset);
                    }

                    GUI.enabled = true;
                }
            }
            EndBox();
        }

        private static void StartBox()
        {
            GUIStyle modifiedBox = GUI.skin.GetStyle("Box");

            if (EditorGUIUtility.isProSkin == false)
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

        private static Vector3 GetNewObjectPosition()
        {
            Camera sceneCamera = EditorWindow.GetWindow<SceneView>().camera;
            Ray ray = sceneCamera.ScreenPointToRay(new Vector3(sceneCamera.pixelWidth / 2, sceneCamera.pixelHeight / 2, 1));

            return ray.GetPoint(1.5f);
        }
    }
}
