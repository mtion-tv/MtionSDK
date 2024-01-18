using mtion.room.sdk.compiled;
using mtion.room.sdk.utility;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace mtion.room.sdk
{
    public class MTIONSDKToolsWindow : EditorWindow
    {
        private enum Tabs
        {
            BUILD,
            PROPS,
            ACTIONS,
            OPTIMIZATION,
            RAGDOLL,
            HELP
        }

        public enum WarningType
        {
            STANDARD,
            ERROR
        }

        private static Tabs _selectedTab;
        private static bool _showPropPanel;
        private static bool _showActionPanel;
        private static bool _showRagdollPanel;

        private static ListRequest _packageListRequest;
        private static string _sdkVersion;
        private static string _supportedUnityVersion;
        private static Texture2D _logoIcon;
        private static Texture2D _warningIcon;
        private static Texture2D _errorIcon;

        private static GUIStyle _headerStyle;
        private static GUIStyle _headerStyleCenter;
        private static GUIStyle _lineStyle;
        private static GUIStyle _toolbarButtonStyle;
        private static GUIStyle _toolbarButtonSelectedStyle;
        private static GUIStyle _smallButtonStyle;
        private static GUIStyle _mediumButtonStyle;
        private static GUIStyle _largeButtonStyle;
        private static GUIStyle _foldoutStyle;
        private static GUIStyle _listHeaderStyle;
        private static GUIStyle _textFieldStyle;
        private static GUIStyle _labelStyle;
        private static GUIStyle _boxHeaderStyle;
        private static GUIStyle _successLabelStyle;
        private static GUIStyle _errorLabelStyle;

        private static readonly Color LineColor = new Color(0.4f, 0.4f, 0.4f);
        private static readonly Color ButtonColor = new Color(0.067f, 0.067f, 0.067f);
        private static readonly Color ActiveButtonColor = new Color(0.168f, 0.753f, 0.984f);
        private static readonly Color TextFieldBackgroundColor = new Color(0.317f, 0.317f, 0.317f);
        private static readonly Color BoxBackgroundColor = new Color(0.14f, 0.14f, 0.14f);

        public static GUIStyle SmallButtonStyle => _smallButtonStyle;
        public static GUIStyle MediumButtonStyle => _mediumButtonStyle;
        public static GUIStyle LargeButtonStyle => _largeButtonStyle;
        public static GUIStyle FoldoutStyle => _foldoutStyle;
        public static GUIStyle BoxHeaderStyle => _boxHeaderStyle;
        public static GUIStyle ListHeaderStyle => _listHeaderStyle;
        public static GUIStyle TextFieldStyle => _textFieldStyle;
        public static GUIStyle LabelStyle => _labelStyle;
        public static GUIStyle SuccessLabelStyle => _successLabelStyle;
        public static GUIStyle ErrorLabelStyle => _errorLabelStyle;

        [MenuItem("MTION SDK/SDK Tools")]
        public static void Init()
        {
            MTIONSDKToolsWindow window = (MTIONSDKToolsWindow)GetWindow(typeof(MTIONSDKToolsWindow));
            window.Show();
            window.titleContent = new GUIContent("MTION SDK Tools", _logoIcon);
        }

        private void OnEnable()
        {
            LoadAllIcons();
            GetSDKVersion();
            UpdateTabsToDisplay();
            MTIONSDKToolsBuildTab.Refresh();
            MTIONSDKToolsAssetTab.Refresh();
            MTIONSDKToolsActionTab.Refresh();
        }

        private void OnGUI()
        {
            InitializeStyles();
            UpdateTabsToDisplay();

            DrawWindowHeader();

            if (string.IsNullOrEmpty(_supportedUnityVersion))
            {
                DrawLoadingContent();
                return;
            }


            if (string.Compare(_supportedUnityVersion, Application.unityVersion) != 0)
            {
                DrawUnityVersionWarning();
                return;
            }

            DrawTabButtons();
            DrawTabContent();
        }

        private void OnFocus()
        {
            UpdateTabsToDisplay();

            switch (_selectedTab)
            {
                case Tabs.BUILD:
                    MTIONSDKToolsBuildTab.Refresh();
                    break;
                case Tabs.PROPS:
                    MTIONSDKToolsAssetTab.Refresh();
                    break;
                case Tabs.ACTIONS:
                    MTIONSDKToolsActionTab.Refresh();
                    break;
                case Tabs.OPTIMIZATION:
                    break;
                case Tabs.HELP:
                    break;
            }
        }

        private void InitializeStyles()
        {
            _headerStyle = new GUIStyle();
            _headerStyle.normal.textColor = Color.white;
            _headerStyle.fontSize = 24;

            _headerStyleCenter = new GUIStyle();
            _headerStyleCenter.normal.textColor = Color.white;
            _headerStyleCenter.fontSize = 24;
            _headerStyleCenter.alignment = TextAnchor.MiddleCenter;

            _lineStyle = new GUIStyle();
            _lineStyle.normal.background = CreateTextureForColor(1, 1, LineColor);

            _toolbarButtonStyle = new GUIStyle(EditorStyles.toolbarButton);
            _toolbarButtonStyle.fontSize = 16;
            _toolbarButtonStyle.fixedHeight = 24;
            _toolbarButtonStyle.normal.textColor = Color.white;
            _toolbarButtonStyle.normal.background = CreateTextureForColor(1, 1, ButtonColor);
            _toolbarButtonStyle.margin = new RectOffset(5, 5, 0, 0);

            _toolbarButtonSelectedStyle = new GUIStyle(_toolbarButtonStyle);
            _toolbarButtonSelectedStyle.normal.background = CreateTextureForColor(1, 1, ActiveButtonColor);

            _smallButtonStyle = new GUIStyle(GUI.skin.button);
            _smallButtonStyle.fixedHeight = 20;
            _smallButtonStyle.fontSize = 14;
            _smallButtonStyle.fontStyle = FontStyle.Bold;
            _smallButtonStyle.normal.textColor = Color.white;
            _smallButtonStyle.normal.background = CreateTextureForColor(1, 1, ButtonColor);

            _mediumButtonStyle = new GUIStyle(_smallButtonStyle);
            _mediumButtonStyle.fixedHeight = 30;
            _mediumButtonStyle.fontSize = 16;

            _largeButtonStyle = new GUIStyle(_smallButtonStyle);
            _largeButtonStyle.fixedHeight = 50;
            _largeButtonStyle.fontSize = 20;

            _foldoutStyle = new GUIStyle(EditorStyles.foldout);
            _foldoutStyle.fontSize = 16;
            _foldoutStyle.fontStyle = FontStyle.Bold;
            _foldoutStyle.normal.textColor = Color.white;

            _listHeaderStyle = new GUIStyle();
            _listHeaderStyle.fontSize = 14;
            _listHeaderStyle.normal.textColor = Color.white;

            _textFieldStyle = new GUIStyle(EditorStyles.textField);
            _textFieldStyle.normal.background = CreateTextureForColor(1, 1, TextFieldBackgroundColor);
            _textFieldStyle.normal.textColor = Color.white;

            _labelStyle = new GUIStyle(EditorStyles.label);
            _labelStyle.normal.textColor = Color.white;

            _boxHeaderStyle = new GUIStyle();
            _boxHeaderStyle.alignment = TextAnchor.MiddleLeft;
            _boxHeaderStyle.fontStyle = FontStyle.Bold;
            _boxHeaderStyle.normal.textColor = Color.white;
            _boxHeaderStyle.fontSize = 16;

            _successLabelStyle = new GUIStyle(EditorStyles.label);
            _successLabelStyle.normal.textColor = Color.green;
            _successLabelStyle.alignment = TextAnchor.MiddleLeft;

            _errorLabelStyle = new GUIStyle(EditorStyles.label);
            _errorLabelStyle.normal.textColor = Color.red;
            _errorLabelStyle.alignment = TextAnchor.MiddleLeft;
        }

        private void LoadAllIcons()
        {
            _logoIcon = TextureLoader.LoadSDKTexture("mtion-logo.png");
            _warningIcon = TextureLoader.LoadSDKTexture("warning-icon.png");
            _errorIcon = TextureLoader.LoadSDKTexture("error-icon.png");
        }

        private static void GetSDKVersion()
        {
            var packageInfoFile = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/LocalPackages/MTIONStudioSDK/package.json");
            if (packageInfoFile != null)
            {
                var manifest = JsonConvert.DeserializeObject<Dictionary<string, object>>(packageInfoFile.text);
                _sdkVersion = (string)manifest["version"];
                _supportedUnityVersion = (string)manifest["supportedVersion"];
            }
            else
            {
                _packageListRequest = Client.List();
                EditorApplication.update += GetSDKVersionCallback;
            }
        }

        private static void GetSDKVersionCallback()
        {
            if (!_packageListRequest.IsCompleted)
            {
                return;
            }

            if (_packageListRequest.Status == StatusCode.Success)
            {
                foreach (var package in _packageListRequest.Result)
                {
                    if (package.name.Equals("com.mtion.sdk"))
                    {
                        _sdkVersion = package.version;

                        var path = Path.Combine(package.resolvedPath, "package.json").Replace("\\", "/");
                        var packageInfoFile = File.ReadAllText(path);
                        if (packageInfoFile != null)
                        {
                            var manifest = JsonConvert.DeserializeObject<Dictionary<string, object>>(packageInfoFile);
                            _supportedUnityVersion = (string)manifest["supportedVersion"];
                        }

                    }
                }
            }

            EditorApplication.update -= GetSDKVersionCallback;
        }

        private void DrawLoadingContent()
        {
            GUILayout.Space(150);
            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(64);
                GUILayout.Label($"Initializing Unity for SDK", _headerStyleCenter);
                GUILayout.Space(64);
            }
            GUILayout.EndHorizontal();
        }

        private void DrawUnityVersionWarning()
        {
            GUILayout.Space(150);
            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(64);
                GUILayout.Label($"Incompatible unity version detected", _headerStyleCenter);
                GUILayout.Space(64);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(16);
            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(64);
                GUILayout.Label($"Version {_supportedUnityVersion} is required for the SDK", _headerStyleCenter);
                GUILayout.Space(64);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(16);
            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(64);
                if (GUILayout.Button($"Click here to download {_supportedUnityVersion}", _largeButtonStyle))
                {
                    Application.OpenURL("https://unity.com/releases/editor/archive");
                }
                GUILayout.Space(64);
            }
            GUILayout.EndHorizontal();
        }

        private void DrawWindowHeader()
        {
            GUILayout.Space(16);
            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(16);
                GUILayout.Label(_logoIcon, GUILayout.MaxWidth(64), GUILayout.MaxHeight(64));
                GUILayout.Space(16);

                GUILayout.BeginVertical();
                GUILayout.Space(16);
                GUILayout.Label($"mtion SDK | version {_sdkVersion}", _headerStyle);
                GUILayout.Space(16);
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(16);
            GUILayout.Box(GUIContent.none, _lineStyle, GUILayout.Width(Screen.width), GUILayout.Height(2));
            GUILayout.Space(16);
        }

        private void DrawTabButtons()
        {
            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Build", _selectedTab == Tabs.BUILD
                    ? _toolbarButtonSelectedStyle
                    : _toolbarButtonStyle))
                {
                    _selectedTab = Tabs.BUILD;
                    MTIONSDKToolsBuildTab.Refresh();
                }
                else if (_showPropPanel && GUILayout.Button("Props", _selectedTab == Tabs.PROPS
                    ? _toolbarButtonSelectedStyle
                    : _toolbarButtonStyle))
                {
                    _selectedTab = Tabs.PROPS;
                    MTIONSDKToolsAssetTab.Initialize();
                    MTIONSDKToolsAssetTab.Refresh();
                }
                else if (_showActionPanel && GUILayout.Button("Actions", _selectedTab == Tabs.ACTIONS
                    ? _toolbarButtonSelectedStyle
                    : _toolbarButtonStyle))
                {
                    _selectedTab = Tabs.ACTIONS;
                    MTIONSDKToolsActionTab.Refresh();
                }
                else if (_showRagdollPanel && GUILayout.Button("Ragdoll", _selectedTab == Tabs.RAGDOLL
                    ? _toolbarButtonSelectedStyle
                    : _toolbarButtonStyle))
                {
                    _selectedTab = Tabs.RAGDOLL;
                }
                else if (GUILayout.Button("Help", _selectedTab == Tabs.HELP
                    ? _toolbarButtonSelectedStyle
                    : _toolbarButtonStyle))
                {
                    _selectedTab = Tabs.HELP;
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawTabContent()
        {
            GUILayout.Space(10);

            switch (_selectedTab)
            {
                case Tabs.BUILD:
                    MTIONSDKToolsBuildTab.Draw();
                    break;
                case Tabs.PROPS:
                    MTIONSDKToolsAssetTab.Draw();
                    break;
                case Tabs.ACTIONS:
                    MTIONSDKToolsActionTab.Draw();
                    break;
                case Tabs.OPTIMIZATION:
                    break;
                case Tabs.RAGDOLL:
                    MTIONSDKToolsRagdollTab.Draw();
                    break;
                case Tabs.HELP:
                    MTIONSDKToolsHelpTab.Draw();
                    break;
            }
        }

        private void UpdateTabsToDisplay()
        {
            var descriptorObject = GameObject.FindObjectOfType<MTIONSDKDescriptorSceneBase>();
            if (descriptorObject == null)
            {
                _showPropPanel = false;
                _showActionPanel = false;
                _showRagdollPanel = false;
            }
            else
            {
                _showPropPanel = descriptorObject.ObjectType == MTIONObjectType.MTIONSDK_ROOM;
                _showActionPanel = descriptorObject.ObjectType == MTIONObjectType.MTIONSDK_ASSET ||
                    descriptorObject.ObjectType == MTIONObjectType.MTIONSDK_AVATAR;
                _showRagdollPanel = descriptorObject.ObjectType == MTIONObjectType.MTIONSDK_AVATAR;
            }
        }

        #region UTILITY

        public static void StartBox()
        {
            GUIStyle modifiedBox = GUI.skin.GetStyle("Box");
            modifiedBox.normal.background = CreateTextureForColor(1, 1, BoxBackgroundColor);

            EditorGUILayout.BeginHorizontal(modifiedBox);
            GUILayout.Label(string.Empty, GUILayout.MaxWidth(5));

            EditorGUILayout.BeginVertical();
            GUILayout.Label(string.Empty, GUILayout.MaxHeight(5));
        }

        public static void EndBox()
        {
            GUILayout.Label(string.Empty, GUILayout.MaxHeight(5));
            EditorGUILayout.EndVertical();

            GUILayout.Label(string.Empty, GUILayout.MaxWidth(5));
            EditorGUILayout.EndHorizontal();

            GUILayout.Label(string.Empty, GUILayout.MaxHeight(5));
        }

        public static Texture2D CreateTextureForColor(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i)
            {
                pix[i] = col;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        public static void DrawWarning(string text, WarningType warningType = WarningType.STANDARD)
        {
            GUIStyle boxStyle = new GUIStyle();
            boxStyle.padding = new RectOffset(0, 0, 0, 0);
            boxStyle.fixedHeight = 75;

            GUIStyle labelStyle = new GUIStyle();
            labelStyle.alignment = TextAnchor.MiddleLeft;
            labelStyle.normal.textColor = Color.white;
            labelStyle.padding = new RectOffset(3, 3, 3, 3);
            labelStyle.margin = new RectOffset(0, 0, 0, 0);
            labelStyle.richText = true;
            labelStyle.wordWrap = true;

            GUIStyle iconStyle = new GUIStyle();
            iconStyle.fixedWidth = 48;
            iconStyle.fixedHeight = 48;
            iconStyle.padding = new RectOffset(0, 0, 0, 0);
            iconStyle.margin = new RectOffset(0, 0, 0, 0);

            GUILayout.BeginHorizontal(boxStyle);
            {
                GUILayout.BeginVertical(GUILayout.Height(boxStyle.fixedHeight));
                GUILayout.FlexibleSpace();
                GUILayout.Label(warningType == WarningType.STANDARD ? _warningIcon : _errorIcon, iconStyle);
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();

                GUILayout.Space(16);

                GUILayout.BeginVertical(GUILayout.Height(boxStyle.fixedHeight));
                GUILayout.FlexibleSpace();
                GUILayout.Label(text, labelStyle);
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        #endregion
    }
}
