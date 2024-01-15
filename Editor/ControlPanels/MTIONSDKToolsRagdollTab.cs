using mtion.room.sdk.action;
using mtion.room.sdk.compiled;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace mtion.room.sdk
{
    public class MTIONSDKToolsRagdollTab
    {
        private static Vector2 _scrollPos;
        private static MTIONAvatarRagdoll _avatarRagdoll;

        public static void Draw()
        {
            using (var scrollView = new EditorGUILayout.ScrollViewScope(_scrollPos))
            {
                _scrollPos = scrollView.scrollPosition;
                InitValues();
                DrawRagdollComponentMissing();
                DrawRagdollComponentEditor();
            }
        }

        private static void InitValues()
        {
            if (_avatarRagdoll == null)
            {
                var descriptor = GameObject.FindObjectOfType<MTIONSDKAvatar>();
                if (descriptor != null)
                {
                    _avatarRagdoll = descriptor.ObjectReference?.GetComponentInChildren<MTIONAvatarRagdoll>();
                }
            }
        }

        private static void DrawRagdollComponentMissing()
        {
            if (_avatarRagdoll != null)
            {
                return;
            }

            if (GUILayout.Button("Add Ragdoll Component", MTIONSDKToolsWindow.MediumButtonStyle))
            {
                var descriptor = GameObject.FindObjectOfType<MTIONSDKAvatar>();
                if (descriptor != null)
                {
                    for (var i = 0; i < descriptor.ObjectReference.transform.childCount; i++)
                    {
                        var child = descriptor.ObjectReference.transform.GetChild(i);
                        if (child.GetComponent<MActionBehaviourGroup>() != null)
                        {
                            continue;
                        }

                        _avatarRagdoll = child.gameObject.AddComponent<MTIONAvatarRagdoll>();
                        break;
                    }    
                }
            }
        }

        private static void DrawRagdollComponentEditor()
        {
            if (_avatarRagdoll == null)
            {
                return;
            }

            MTIONSDKToolsWindow.StartBox();
            GUILayout.Label("Configure Ragdoll", MTIONSDKToolsWindow.BoxHeaderStyle);
            EditorGUILayout.Space();

            var editor = Editor.CreateEditor(_avatarRagdoll);
            editor.OnInspectorGUI();

            MTIONSDKToolsWindow.EndBox();
        }
    }
}
