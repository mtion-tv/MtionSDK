using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace mtion.room.sdk
{
    [CustomEditor(typeof(MTIONAvatarRagdoll))]
    public class MTIONAvatarRagdollEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            FindAnimator();
            DrawAutoPopulateSection();
            serializedObject.ApplyModifiedProperties();
        }

        private void FindAnimator()
        {
            var animatorProp = serializedObject.FindProperty("_animator");
            if (animatorProp.objectReferenceValue != null)
            {
                return;
            }

            ((MTIONAvatarRagdoll)target).SetAnimator();
        }

        private void DrawAutoPopulateSection()
        {
            if (GUILayout.Button("Auto Populate Bones"))
            {
                var avatarRagdoll = (MTIONAvatarRagdoll)serializedObject.targetObject;
                avatarRagdoll.AutoPopulateBonesFromAnimator();
            }
        }
    }
}
