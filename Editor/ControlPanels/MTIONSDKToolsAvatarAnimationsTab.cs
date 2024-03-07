using UnityEditor;
using UnityEngine;

namespace mtion.room.sdk
{
    public static class MTIONSDKToolsAvatarAnimationsTab
    {
        private static Vector2 _scrollPos;
        private static AvatarAnimations _avatarAnimations;
        private static Editor _avatarAnimationsEditor;


        public static void Refresh()
        {
            if (_avatarAnimations == null)
            {
                _avatarAnimations = GameObject.FindObjectOfType<AvatarAnimations>();
                _avatarAnimationsEditor = Editor.CreateEditor(_avatarAnimations);
            }
        }

        public static void Draw()
        {
            using (var scrollView = new EditorGUILayout.ScrollViewScope(_scrollPos))
            {
                _scrollPos = scrollView.scrollPosition;
                DrawAvatarAnimations();
            }
        }

        private static void DrawAvatarAnimations()
        {
            if (_avatarAnimations == null)
            {
                MTIONSDKToolsWindow.StartBox();
                {
                    EditorGUILayout.LabelField("Add an avatar to edit animations",
                        MTIONSDKToolsWindow.ListHeaderStyle);
                }
                MTIONSDKToolsWindow.EndBox();
                return;
            }

            _avatarAnimationsEditor.OnInspectorGUI();
        }
    }

}
