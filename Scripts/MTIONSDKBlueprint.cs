using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;
using System;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace mtion.room.sdk.compiled
{
    [ExecuteInEditMode]
    public sealed class MTIONSDKBlueprint : MTIONSDKDescriptorSceneBase
    {
        public static readonly string CurrentFormatVersion = "1.0.0";
        
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
        public string EnvironmentSceneName;

        [SerializeField] 
        public GameObject SDKRoot;

        public MTIONSDKRoom GetMTIONSDKRoom()
        {
            if (string.IsNullOrEmpty(RoomSceneName))
            {
                return null;
            }

#if UNITY_EDITOR
            var roomScene = EditorSceneManager.GetSceneByName(RoomSceneName);
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
            if (string.IsNullOrEmpty(EnvironmentSceneName))
            {
                return null;
            }

#if UNITY_EDITOR
            var environmentScene = EditorSceneManager.GetSceneByName(EnvironmentSceneName);
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
    }
}
