using mtion.room.sdk.action;
using mtion.room.sdk.customproperties;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace mtion.room.sdk.compiled
{
    public static class ComponentVerificationUtil
    {
        public static Dictionary<MTIONSDKAssetBase, List<string>> GetSDKObjectsWithMissingMetadata()
        {
            var output = new Dictionary<MTIONSDKAssetBase, List<string>>();

            // Check all the assets
            var assets = GameObject.FindObjectsOfType<MTIONSDKAssetBase>();
            foreach (var asset in assets)
            {
                output[asset] = new List<string>();

                if (string.IsNullOrEmpty(asset.Name))
                {
                    output[asset].Add("Name");
                }

                if (string.IsNullOrEmpty(asset.Description))
                {
                    output[asset].Add("Description");
                }

                if (asset.ObjectReference == null)
                {
                    output[asset].Add("Object Reference");
                }
            }

            // Check all scene objects
            var sceneObjects = GameObject.FindObjectsOfType<MTIONSDKDescriptorSceneBase>();
            foreach (var sceneObject in sceneObjects)
            {
                if (sceneObject.SDKRoot == null)
                {
                    output[sceneObject].Add("SDK Root");
                }
            }

            // Check the room object
            var room = GameObject.FindObjectOfType<MTIONSDKRoom>();
            if (room != null && string.IsNullOrEmpty(room.EnvironmentInternalID))
            {
                output[room].Add("Environment Internal ID");
            }

            // Clear any empty entries
            foreach (var entry in output.Keys.ToList())
            {
                if (output[entry].Count == 0)
                {
                    output.Remove(entry);
                }
            }

            return output;
        }

        public static List<MTIONSDKAssetBase> GetSDKObjectsWithoutColliders()
        {
            var output = new List<MTIONSDKAssetBase>();

            // Check all sdk assets
            var assets = GameObject.FindObjectsOfType<MTIONSDKAssetBase>();
            foreach (var asset in assets)
            {
                if (asset.ObjectType != MTIONObjectType.MTIONSDK_ASSET) continue;

                if (asset.ObjectReference != null && 
                    asset.ObjectReference.GetComponentInChildren<Collider>() == null) 
                {
                    output.Add(asset);
                }
            }

            return output;
        }

        public static List<MActionBehaviour> GetActionBehavioursEmptyEntryPoints()
        {
            var output = new List<MActionBehaviour>();

            var behaviourGroup = GameObject.FindAnyObjectByType<MActionBehaviourGroup>();
            if (behaviourGroup == null)
            {
                return output;
            }

            var actions = behaviourGroup.GetActions();
            foreach (var action in actions)
            {
                if (action == null) continue;

                if (action.ActionEntryPoints.Count == 0)
                {
                    output.Add(action);
                    break;
                }

                foreach (var entryPoint in action.ActionEntryPoints)
                {
                    if (entryPoint.Target != null) continue;

                    output.Add(action);
                    break;
                }
            }

            return output;
        }

        public static void VerifyAllComponentsIntegrity(MTIONSDKDescriptorSceneBase roomSDKDescriptorObject,
            MTIONObjectType sdkType)
        {
            VirtualComponentTracker[] components;
            switch (sdkType)
            {
                case MTIONObjectType.MTIONSDK_CAMERA:
                    components = GameObject.FindObjectsOfType<MVirtualCameraEventTracker>();
                    break;
                case MTIONObjectType.MTIONSDK_DISPLAY:
                    components = GameObject.FindObjectsOfType<MVirtualDisplayTracker>();
                    break;
                case MTIONObjectType.MTIONSDK_LIGHT:
                    components = GameObject.FindObjectsOfType<MVirtualLightingTracker>();
                    break;
                case MTIONObjectType.MTIONSDK_ASSET:
                    // Special case
                    VerifyAllAssetsIntegrity(roomSDKDescriptorObject);
                    return;
                default:
                    return;
            }

            HashSet<string> usedGuids = new HashSet<string>();
            foreach (var component in components)
            {
                // Force all SDK Components to be child of SDK Props
                if (component.transform.parent != roomSDKDescriptorObject.SDKRoot.transform)
                {
                    component.transform.parent = roomSDKDescriptorObject.SDKRoot.transform;
                }

                if (string.IsNullOrEmpty(component.GUID))
                {
                    SDKEditorUtil.InitVirtualComponentFields(component);
                }

                usedGuids.Add(component.GUID);
            }
        }

        private static void VerifyAllAssetsIntegrity(MTIONSDKDescriptorSceneBase roomSDKDescriptorObject)
        {
            var virtualAssetsPass1 = GameObject.FindObjectsOfType<MVirtualAssetTracker>();
            for (int i = 0; i < virtualAssetsPass1.Length; ++i)
            {
                WrapAsset(virtualAssetsPass1[i], roomSDKDescriptorObject);
            }

            var virtualAssetsPass2 = GameObject.FindObjectsOfType<MVirtualAssetTracker>();         
            foreach (var asset in virtualAssetsPass2)
            {
                asset.ObjectReference = asset.gameObject;
                SDKEditorUtil.InitAddressableAssetFields(asset, MTIONObjectType.MTIONSDK_ASSET);
                CollectAssetCustomProperties(asset);
            }

            var virtualAssetsPass3 = GameObject.FindObjectsOfType<MVirtualAssetTracker>();
            var filteredDuplicates = AssetComparisonUtil.FilterDuplicateAssets(
                virtualAssetsPass3, roomSDKDescriptorObject.LocationOption);

            foreach (var asset in virtualAssetsPass3)
            {
                AssignAssetGuids(asset, filteredDuplicates);
            }
        }

        private static void WrapAsset(MVirtualAssetTracker asset,
            MTIONSDKDescriptorSceneBase roomSDKDescriptorObject)
        {
            // Ensure that asset is wrapped
            var componentCount = asset.GetComponents<MonoBehaviour>().Length;
            var meshCount = asset.GetComponents<MeshRenderer>().Length;

            if (componentCount != 2 || meshCount > 0)
            {
                // Move to container
                GameObject go = new GameObject(asset.gameObject.name);
                go.transform.parent = roomSDKDescriptorObject.SDKRoot.transform;
                go.AddComponent<CustomPropertiesContainer>();
                var tracker = go.AddComponent<MVirtualAssetTracker>();

                tracker.GUID = asset.GUID;
                tracker.ExportGLTFEnabled = asset.ExportGLTFEnabled;
                tracker.AssetParams = asset.AssetParams;

                // remove component
                GameObject assetGo = asset.gameObject;
                go.transform.position = assetGo.transform.position;
                go.transform.rotation = assetGo.transform.rotation;

                assetGo.transform.parent = go.transform;
                assetGo.transform.localPosition = Vector3.zero;
                assetGo.transform.localRotation = Quaternion.identity;

                GameObject.DestroyImmediate(asset);
                asset = tracker;
            }

            // Force all SDK Components to be child of SDK Props
            if (asset.transform.parent != roomSDKDescriptorObject.SDKRoot.transform)
            {
                asset.transform.parent = roomSDKDescriptorObject.SDKRoot.transform;
            }
        }

        private static void AssignAssetGuids(MTIONSDKAssetBase asset,
            IEnumerable<MTIONSDKAssetBase> filteredDuplicates)
        {
            foreach (var duplicate in filteredDuplicates)
            {
                if (duplicate.Equals(asset)) continue;

                var isDuplicate = AssetComparisonUtil.AssetsAreDuplicates(asset, duplicate);
                if (isDuplicate)
                {
                    asset.GUID = duplicate.GUID;
                    return;
                }
            }

            var staleDuplicate = false;
            foreach (var duplicate in filteredDuplicates)
            {
                if (asset.Equals(duplicate)) continue;

                if (asset.GUID == duplicate.GUID)
                {
                    staleDuplicate = true;
                    break;
                }
            }

            if (staleDuplicate)
            {
                asset.GenerateNewGUID();
            }
        }

        public static void CollectAssetCustomProperties(MTIONSDKAssetBase asset)
        {
            if (asset == null ||
                asset.ObjectReference == null)
            {
                return;
            }

            var customPropsContainer = asset.ObjectReference.GetComponent<CustomPropertiesContainer>();
            if (customPropsContainer == null)
            {
                customPropsContainer = asset.ObjectReference.AddComponent<CustomPropertiesContainer>();
            }
            customPropsContainer.ClearProperties();

            var components = asset.ObjectReference.GetComponentsInChildren<Component>();
            foreach (var component in components)
            {
                if (component == null)
                {
                    continue;
                }

                var allProps = component.GetType().GetProperties();
                var exposedProps = allProps.Where(prop => Attribute.IsDefined(prop, typeof(CustomPropertyAttribute))).ToList();
                foreach (var prop in exposedProps)
                {
                    ICustomProperty customProp = null;
                    IListCustomProperty listCustomProp = null;
                    var customPropAttribute = (CustomPropertyAttribute)prop.GetCustomAttribute(typeof(CustomPropertyAttribute));

                    if (prop.PropertyType == typeof(bool))
                    {
                        var boolProp = new BoolCustomProperty(
                            customPropAttribute.BoolDefaultValue);
                        customPropsContainer.BoolCustomProperties.Add(boolProp);
                        customProp = boolProp;
                    }
                    else if (prop.PropertyType == typeof(int))
                    {
                        var intProp = new IntCustomProperty(
                            customPropAttribute.IntDefaultValue,
                            customPropAttribute.IntMinValue,
                            customPropAttribute.IntMaxValue);
                        customPropsContainer.IntCustomProperties.Add(intProp);
                        customProp = intProp;
                    }
                    else if (prop.PropertyType == typeof(float))
                    {
                        var floatProp = new FloatCustomProperty(
                            customPropAttribute.FloatDefaultValue,
                            customPropAttribute.FloatMinValue,
                            customPropAttribute.FloatMaxValue);
                        customPropsContainer.FloatCustomProperties.Add(floatProp);
                        customProp = floatProp;
                    }
                    else if (prop.PropertyType == typeof(string))
                    {
                        var stringProp = new StringCustomProperty(
                            customPropAttribute.StringDefaultValue);
                        customPropsContainer.StringCustomProperties.Add(stringProp);
                        customProp = stringProp;
                    }
                    else if (prop.PropertyType == typeof(List<int>))
                    {
                        var listIntProp = new ListIntCustomProperty(
                            customPropAttribute.IntDefaultValue,
                            customPropAttribute.IntMinValue,
                            customPropAttribute.IntMaxValue);
                        customPropsContainer.ListIntCustomProperties.Add(listIntProp);
                        customProp = listIntProp;
                        listCustomProp = listIntProp;
                    }
                    else if (prop.PropertyType == typeof(List<string>))
                    {
                        var listStringProp = new ListStringCustomProperty(
                            customPropAttribute.StringDefaultValue);
                        customPropsContainer.ListStringCustomProperties.Add(listStringProp);
                        customProp = listStringProp;
                        listCustomProp = listStringProp;
                    }

                    if (customProp != null)
                    {
                        var declaringTypeName = prop.DeclaringType.AssemblyQualifiedName;
                        var propName = prop.Name;
                        var siblingIndexPath = GetAssetGameobjectSiblingIndexPath(asset, component.transform);
                        customProp.SetPropertyMetadata(declaringTypeName, propName, siblingIndexPath);
                    }

                    if (listCustomProp != null)
                    {
                        listCustomProp.DefineList(
                            customPropAttribute.ListDefaultLength,
                            customPropAttribute.ListFixedLength,
                            customPropAttribute.ListMinLength,
                            customPropAttribute.ListMaxLength);
                    }
                }
            }
        }

        private static List<int> GetAssetGameobjectSiblingIndexPath(MTIONSDKAssetBase assetBase,
            Transform transform)
        {
            var output = new List<int>();
            while (transform != assetBase.ObjectReference.transform)
            {
                output.Insert(0, transform.GetSiblingIndex());
                transform = transform.parent;
            }

            return output;
        }
    }
}
