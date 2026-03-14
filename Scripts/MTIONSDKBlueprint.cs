using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;
using System;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace mtion.room.sdk.compiled
{
    [ExecuteInEditMode]
    public sealed class MTIONSDKBlueprint : MTIONSDKDescriptorSceneBase
    {
        public static readonly string CurrentFormatVersion = "1.0.1";
        
        public class ClubhouseData
        {
            public string Id;
            public string RoomAssetId;
            public string EnvironmentAssetId;
            public string RootElementId;
            public string Name;
            public string Description;
            public string FormatVersion;
            public Dictionary<string, IClubhouseElement> ElementMap;
        }

        public class ClubhouseLocalData
        {
            public string Id;
            public long CreateTime;
            public long UpdateTime;
        }

        public interface IClubhouseElement
        {

        }

        public class ClubhouseElement<T> : IClubhouseElement
        {
            public string Id;
            public string Name;
            public string ElementType;
            public string ParentId;
            public Vector3 Position;
            public Vector3 Scale;
            public Quaternion Rotation;
            public bool Visible = true;
            public List<string> ChildrenIds = new();
            public T TypeExtra;
        }

        public class ClubhouseCamera : ClubhouseElement<ClubhouseCamera.CameraExtra>
        {
            public class CameraExtra
            {
                public float Fov;
                public float NearPlane;
                public float FarPlane;
                public float AspectRatio;
                public List<input.sdk.compiled.KeyCodeCustomSubset> KeyCodeList;
            }
        }

        public class ClubhouseDisplay : ClubhouseElement<ClubhouseDisplay.DisplayExtra>
        {
            public class DisplayExtra
            {
                public DisplayComponentType DisplayType = DisplayComponentType.DESKTOP_CAPTURE;
                public List<input.sdk.compiled.KeyCodeCustomSubset> KeyCodeList;
            }
        }

        public class ClubhouseLight : ClubhouseElement<ClubhouseLight.LightExtra>
        {
            public class LightExtra
            {
                public Color Color;
                public float Intensity;
                public LightingComponentType Type;
                public bool GizmoIsActive = true;
            }
        }

        public class ClubhouseObject : ClubhouseElement<ClubhouseObject.ObjectExtra>
        {
            public class ObjectExtra
            {
                public string AddressableAssetId;
            }
        }

        [SerializeField]
        public string RoomSceneName;

        [SerializeField]
        public string RoomSceneGuid;

        [SerializeField]
        public string RoomScenePath;

        [SerializeField]
        public string EnvironmentSceneName;

        [SerializeField]
        public string EnvironmentSceneGuid;

        [SerializeField]
        public string EnvironmentScenePath;

        [SerializeField] 
        public GameObject SDKRoot;

        public MTIONSDKRoom GetMTIONSDKRoom()
        {
#if UNITY_EDITOR
            if (!TryGetResolvedRoomScene(out Scene roomScene, out _))
            {
                return null;
            }

            foreach (var go in roomScene.GetRootGameObjects())
            {
                var sdkRoom = go.GetComponentInChildren<MTIONSDKRoom>();
                if (sdkRoom != null)
                {
                    return sdkRoom;
                }
            }
#endif

            return null;
        }

        public MTIONSDKEnvironment GetMTIONSDKEnvironment()
        {
#if UNITY_EDITOR
            if (!TryGetResolvedEnvironmentScene(out Scene environmentScene, out _))
            {
                return null;
            }

            foreach (var go in environmentScene.GetRootGameObjects())
            {
                var sdkEnvironment = go.GetComponentInChildren<MTIONSDKEnvironment>();
                if (sdkEnvironment != null)
                {
                    return sdkEnvironment;
                }
            }
#endif

            return null;
        }

#if UNITY_EDITOR


        public void SetRoomSceneReference(SceneAsset sceneAsset)
        {
            SetRoomSceneReference(AssetDatabase.GetAssetPath(sceneAsset));
        }

        public void SetRoomSceneReference(Scene scene)
        {
            AssignSceneReference(scene.name, scene.path, ref RoomSceneName, ref RoomSceneGuid, ref RoomScenePath);
        }

        public void SetEnvironmentSceneReference(SceneAsset sceneAsset)
        {
            SetEnvironmentSceneReference(AssetDatabase.GetAssetPath(sceneAsset));
        }

        public void SetEnvironmentSceneReference(Scene scene)
        {
            AssignSceneReference(scene.name, scene.path, ref EnvironmentSceneName, ref EnvironmentSceneGuid, ref EnvironmentScenePath);
        }

        public void SetRoomSceneReference(string scenePath)
        {
            AssignSceneReference(null, scenePath, ref RoomSceneName, ref RoomSceneGuid, ref RoomScenePath);
        }

        public void SetEnvironmentSceneReference(string scenePath)
        {
            AssignSceneReference(null, scenePath, ref EnvironmentSceneName, ref EnvironmentSceneGuid, ref EnvironmentScenePath);
        }

        public bool TryResolveRoomScenePath(out string resolvedPath, out string errorMessage)
        {
            return TryResolveScenePath(RoomSceneGuid, RoomScenePath, RoomSceneName, out resolvedPath, out errorMessage);
        }

        public bool TryResolveEnvironmentScenePath(out string resolvedPath, out string errorMessage)
        {
            return TryResolveScenePath(EnvironmentSceneGuid, EnvironmentScenePath, EnvironmentSceneName, out resolvedPath, out errorMessage);
        }

        public bool TryGetResolvedRoomScene(out Scene scene, out string errorMessage)
        {
            if (!TryResolveRoomScenePath(out string resolvedPath, out errorMessage))
            {
                scene = default(Scene);
                return false;
            }

            return TryGetLoadedScene(resolvedPath, RoomSceneName, out scene, out errorMessage);
        }

        public bool TryGetResolvedEnvironmentScene(out Scene scene, out string errorMessage)
        {
            if (!TryResolveEnvironmentScenePath(out string resolvedPath, out errorMessage))
            {
                scene = default(Scene);
                return false;
            }

            return TryGetLoadedScene(resolvedPath, EnvironmentSceneName, out scene, out errorMessage);
        }

        private void AssignSceneReference(string fallbackSceneName, string scenePath, ref string sceneName, ref string sceneGuid, ref string storedScenePath)
        {
            storedScenePath = string.IsNullOrWhiteSpace(scenePath) ? null : scenePath.Replace('\\', '/');
            sceneGuid = string.IsNullOrWhiteSpace(storedScenePath) ? null : AssetDatabase.AssetPathToGUID(storedScenePath);
            sceneName = !string.IsNullOrWhiteSpace(storedScenePath)
                ? Path.GetFileNameWithoutExtension(storedScenePath)
                : fallbackSceneName;
            EditorUtility.SetDirty(this);
        }

        private static bool TryResolveScenePath(string sceneGuid, string scenePath, string sceneName, out string resolvedPath, out string errorMessage)
        {
            if (!string.IsNullOrWhiteSpace(scenePath) && AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
            {
                resolvedPath = scenePath.Replace('\\', '/');
                errorMessage = null;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(sceneGuid))
            {
                string guidPath = AssetDatabase.GUIDToAssetPath(sceneGuid);
                if (!string.IsNullOrWhiteSpace(guidPath) && AssetDatabase.LoadAssetAtPath<SceneAsset>(guidPath) != null)
                {
                    resolvedPath = guidPath.Replace('\\', '/');
                    errorMessage = null;
                    return true;
                }
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                resolvedPath = null;
                errorMessage = "Scene reference is empty.";
                return false;
            }

            List<string> exactMatches = new List<string>();
            string[] guids = AssetDatabase.FindAssets($"t:Scene {sceneName}");
            foreach (string guid in guids)
            {
                string candidatePath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetFileNameWithoutExtension(candidatePath), sceneName, StringComparison.Ordinal))
                {
                    exactMatches.Add(candidatePath.Replace('\\', '/'));
                }
            }

            if (exactMatches.Count == 1)
            {
                resolvedPath = exactMatches[0];
                errorMessage = null;
                return true;
            }

            resolvedPath = null;
            errorMessage = exactMatches.Count > 1
                ? $"Scene reference '{sceneName}' is ambiguous. Store a unique scene path or guid."
                : $"Scene reference '{sceneName}' could not be resolved.";
            return false;
        }

        private static bool TryGetLoadedScene(string resolvedPath, string sceneName, out Scene scene, out string errorMessage)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loadedScene = SceneManager.GetSceneAt(i);
                if (!loadedScene.IsValid())
                {
                    continue;
                }

                if ((!string.IsNullOrWhiteSpace(resolvedPath) && string.Equals(loadedScene.path, resolvedPath, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(sceneName) && string.Equals(loadedScene.name, sceneName, StringComparison.Ordinal)))
                {
                    scene = loadedScene;
                    errorMessage = null;
                    return true;
                }
            }

            scene = default(Scene);
            errorMessage = $"Scene '{sceneName}' is not currently loaded.";
            return false;
        }

#endif
    }
}
