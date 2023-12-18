using mtion.room.sdk.compiled;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace mtion.room.sdk
{
    [ExecuteInEditMode]
    public class MTIONSDKInitialization
    {
        public static bool manualCheck = false;
        private const string MTION_BACKERY_EXT_EXISTS = "MTION_BACKERY_EXT_EXISTS";
        private const string MTION_BACKERY_EXT_NOTEXISTS = "MTION_BACKERY_EXT_NOTEXISTS";


        [MenuItem("MTION SDK/Fix Scripts and Dependencies", priority = 1)]
        private static void ManualCheck()
        {
            CheckOnLoad();
            manualCheck = true;
        }

        [DidReloadScripts]
        private static void CheckOnLoad()
        {
            bool backeryTypeExists = TypeExist("BakeryProjectSettings");

            string scriptingDefine = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
            string[] scriptingDefines = scriptingDefine.Split(';');
            bool mtionExtDefined = scriptingDefines.Contains(MTION_BACKERY_EXT_EXISTS);
            mtionExtDefined |= scriptingDefines.Contains(MTION_BACKERY_EXT_NOTEXISTS);
            if (!mtionExtDefined)
            {
                if (backeryTypeExists)
                {
                    AddDefine(MTION_BACKERY_EXT_EXISTS);
                }
                else
                {
                    AddDefine(MTION_BACKERY_EXT_NOTEXISTS);
                }

            }

            var roomSDKObject = GameObject.FindObjectOfType<MTIONSDKRoom>();
            if (roomSDKObject != null)
            {
                FixTrackedAsset(roomSDKObject, MTIONObjectType.MTIONSDK_ROOM);
            }

            var envSDKObject = GameObject.FindObjectOfType<MTIONSDKEnvironment>();
            if (envSDKObject != null)
            {
                FixTrackedAsset(envSDKObject, MTIONObjectType.MTIONSDK_ENVIRONMENT);
            }

            var assetSDKObject = GameObject.FindObjectOfType<MTIONSDKAsset>();
            if (assetSDKObject != null)
            {
                FixTrackedAsset(assetSDKObject, MTIONObjectType.MTIONSDK_ASSET);
            }

            var avatarSDKObject = GameObject.FindObjectOfType<MTIONSDKAvatar>();
            if (avatarSDKObject != null)
            {
                FixTrackedAsset(avatarSDKObject, MTIONObjectType.MTIONSDK_AVATAR);
            }
            
            if (manualCheck)
            {
                manualCheck = false;
                EditorUtility.DisplayDialog("MTION SDK Dependency Checker", "Scripting Define Symbols configured.", "OK");
            }
        }

        public static bool TypeExist(string className)
        {
            var foundType = (from assembly in AppDomain.CurrentDomain.GetAssemblies()
                             from type in GetTypesSafe(assembly)
                             where type.Name == className
                             select type).FirstOrDefault();

            return foundType != null;
        }

        private static IEnumerable<Type> GetTypesSafe(Assembly assembly)
        {
            Type[] types;

            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types;
            }

            return types.Where(x => x != null);
        }

        static private void AddDefine(string define)
        {
            string scriptingDefine = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
            string[] scriptingDefines = scriptingDefine.Split(';');
            List<string> listDefines = scriptingDefines.ToList();
            listDefines.Add(define);

            string newDefines = string.Join(";", listDefines.ToArray());
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, newDefines);
        }

        private static void FixTrackedAsset(MTIONSDKDescriptorSceneBase descriptorBase, MTIONObjectType mType)
        {
            var scene = EditorSceneManager.GetActiveScene();

            SDKEditorUtil.InitAddressableAssetFields(descriptorBase, mType, scene.name);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
