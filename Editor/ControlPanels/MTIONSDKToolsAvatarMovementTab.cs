using UnityEditor;
using UnityEngine;

namespace mtion.room.sdk
{
    public static class MTIONSDKToolsAvatarMovementTab
    {
        private static Vector2 _scrollPos;
        private static AvatarMovementSettings _avatarMovement;
        private static Editor _avatarMovementEditor;


        public static void Refresh()
        {
            if (_avatarMovement == null)
            {
                _avatarMovement = GameObject.FindObjectOfType<AvatarMovementSettings>();
                _avatarMovementEditor = Editor.CreateEditor(_avatarMovement);
            }
        }

        public static void Draw()
        {
            using (var scrollView = new EditorGUILayout.ScrollViewScope(_scrollPos))
            {
                _scrollPos = scrollView.scrollPosition;
                DrawAvatarMovementSettings();
            }
        }

        private static void DrawAvatarMovementSettings()
        {
            if (_avatarMovement == null)
            {
                MTIONSDKToolsWindow.StartBox();
                {
                    EditorGUILayout.LabelField("Add an avatar to edit movement settings",
                        MTIONSDKToolsWindow.ListHeaderStyle);
                }
                MTIONSDKToolsWindow.EndBox();
                return;
            }

            _avatarMovementEditor.OnInspectorGUI();
        }
    }
}
