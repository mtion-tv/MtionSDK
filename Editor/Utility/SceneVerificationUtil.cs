using mtion.room.sdk.action;
using mtion.room.sdk.compiled;
using mtion.room.sdk.customproperties;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk
{
    public static class SceneVerificationUtil
    {
        public static void VerifySceneIntegrity(MTIONSDKDescriptorSceneBase sceneBase)
        {
            if (sceneBase == null)
            {
                return;
            }

            if (sceneBase.ObjectType == MTIONObjectType.MTIONSDK_ASSET)
            {
                ComponentVerificationUtil.CollectAssetCustomProperties(sceneBase);
            }
        }

        public static List<GameObject> GetGameObjectsWithRigidbodyWithoutCollider()
        {
            var output = new List<GameObject>();

            var rigidbodies = GameObject.FindObjectsOfType<Rigidbody>();
            foreach (var rb in rigidbodies)
            {
                if (rb.GetComponentInChildren<Collider>() == null)
                {
                    output.Add(rb.gameObject);
                }
            }

            return output;
        }

        public static List<GameObject> GetGameObjectsWithMissingScripts()
        {
            var output = new List<GameObject>();

            var allGameObjects = GameObject.FindObjectsOfType<GameObject>();
            foreach (var go in allGameObjects)
            {
                var components = go.GetComponents<Component>();
                foreach (var component in components)
                {
                    if (component == null)
                    {
                        output.Add(go);
                        break;
                    }
                }
            }

            return output;
        }

        public static Dictionary<CustomPropertiesContainer, HashSet<string>> GetCustomPropContainersWithDuplicatePropNames()
        {
            var output = new Dictionary<CustomPropertiesContainer, HashSet<string>>();

            var customPropContainers = GameObject.FindObjectsOfType<CustomPropertiesContainer>();
            foreach (var customPropContainer in customPropContainers)
            {
                output.Add(customPropContainer, new HashSet<string>());
                var namesFound = new HashSet<string>();
                foreach (var customProp in customPropContainer.GetAllProperties())
                {
                    if (namesFound.Contains(customProp.PropertyName))
                    {
                        output[customPropContainer].Add(customProp.PropertyName);
                        continue;
                    }

                    namesFound.Add(customProp.PropertyName);
                }    
            }

            return output;
        }

        public static Dictionary<UnityEventAction, List<string>> GetGameObjectsWithInvalidUnityEventActions()
        {
            var output = new Dictionary<UnityEventAction, List<string>>();

            var unityEventActionObjects = GameObject.FindObjectsOfType<UnityEventAction>();
            foreach (var action in unityEventActionObjects)
            {
                var nonUnityEventTargets = action.GetNonUnityEventTargets();
                if (nonUnityEventTargets.Count > 0)
                {
                    output[action] = nonUnityEventTargets;
                }
            }

            return output;
        }

        public static bool IsRagdollConfiguredForAvatar()
        {
            var descriptor = GameObject.FindObjectOfType<MTIONSDKAvatar>();
            if (descriptor == null || descriptor.ObjectReference == null)
            {
                return true;
            }

            var avatarRagdoll = descriptor.ObjectReference.GetComponentInChildren<MTIONAvatarRagdoll>();
            return avatarRagdoll != null;
        }

        public static bool AvatarHasAnimator()
        {
            var descriptor = GameObject.FindObjectOfType<MTIONSDKAvatar>();
            if (descriptor == null || descriptor.ObjectReference == null)
            {
                return true;
            }

            var animator = descriptor.ObjectReference.GetComponentInChildren<Animator>();
            return animator != null;
        }

        public static int GetBuildObjectCountInScene()
        {
            var descriptor = BuildManager.GetSceneDescriptor()?.GetComponent<MTIONSDKDescriptorSceneBase>();
            if (descriptor is MTIONSDKBlueprint sdkBlueprint)
            {
                var roomSdk = sdkBlueprint.GetMTIONSDKRoom();
                if (roomSdk != null && roomSdk.ObjectReference != null)
                {
                    return roomSdk.ObjectReference.transform.childCount;
                }
            }

            if (descriptor != null && descriptor.ObjectReference != null)
            {
                return descriptor.ObjectReference.transform.childCount;
            }

            return 0;
        }
    }
}
