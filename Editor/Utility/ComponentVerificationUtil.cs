using mtion.room.sdk.action;
using mtion.room.sdk.customproperties;
using mtion.service.api;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace mtion.room.sdk.compiled
{
    public static class ComponentVerificationUtil
    {
        public static Dictionary<MTIONSDKAssetBase, List<string>> GetSDKObjectsWithMissingMetadata()
        {
            var output = new Dictionary<MTIONSDKAssetBase, List<string>>();

            var assets = GameObject.FindObjectsOfType<MTIONSDKAssetBase>();
            var containsBlueprint = assets.Any(x => x.ObjectType == MTIONObjectType.MTIONSDK_BLUEPRINT);

            foreach (var asset in assets)
            {
                if (containsBlueprint &&
                    (asset.ObjectType == MTIONObjectType.MTIONSDK_ROOM ||
                    asset.ObjectType == MTIONObjectType.MTIONSDK_ENVIRONMENT))
                {
                    continue;
                }

                output[asset] = new List<string>();

                if (string.IsNullOrEmpty(asset.Name))
                {
                    output[asset].Add("Name");
                }

                if (string.IsNullOrEmpty(asset.Description))
                {
                    output[asset].Add("Description");
                }

                if (asset.ObjectReferenceProp == null)
                {
                    output[asset].Add("Object Reference");
                }
            }

            var room = GameObject.FindObjectOfType<MTIONSDKRoom>();
            if (room != null && string.IsNullOrEmpty(room.EnvironmentInternalID))
            {
                output[room].Add("Environment Internal ID");
            }

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

            var assets = GameObject.FindObjectsOfType<MTIONSDKAssetBase>();
            foreach (var asset in assets)
            {
                if (asset.ObjectType != MTIONObjectType.MTIONSDK_ASSET) continue;

                if (asset.ObjectReferenceProp != null &&
                    asset.ObjectReferenceProp.GetComponentInChildren<Collider>() == null)
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

        public static void VerifyAllComponentsIntegrity(MTIONSDKRoom roomSDKDescriptorObject,
            MTIONObjectType sdkType, bool doServerCheck)
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
                    VerifyAllAssetsIntegrity(roomSDKDescriptorObject, doServerCheck);
                    return;
                case MTIONObjectType.MTIONSDK_AVATAR:
                    VerifyAllAvatarIntegrity(roomSDKDescriptorObject, doServerCheck);
                    return;
                default:
                    return;
            }

            HashSet<string> usedGuids = new HashSet<string>();
            foreach (var component in components)
            {
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

        private static void VerifyAllAssetsIntegrity(MTIONSDKRoom roomSDKDescriptorObject, bool doServerCheck = false)
        {
            if (doServerCheck)
            {
                var virtualAssetsPass0 = GameObject.FindObjectsOfType<MVirtualAssetTracker>();
                for (int i = 0; i < virtualAssetsPass0.Length; ++i)
                {
                    var asset = virtualAssetsPass0[i];

                    SDKServerManager.VerifyAssetGuid(asset);



                }
            }

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

        private static void VerifyAllAvatarIntegrity(MTIONSDKRoom roomSDKDescriptorObject, bool doServerCheck = false)
        {
            if (doServerCheck)
            {
                var virtualAvatarPass0 = GameObject.FindObjectsOfType<MVirtualAvatarTracker>();
                for (int i = 0; i < virtualAvatarPass0.Length; ++i)
                {
                    var asset = virtualAvatarPass0[i];
                    SDKServerManager.VerifyAssetGuid(asset);


                }
            }
        }

        private static void WrapAsset(MVirtualAssetTracker asset, MTIONSDKRoom roomSDKDescriptorObject)
        {
            var componentCount = asset.GetComponents<MonoBehaviour>().Length;
            var meshCount = asset.GetComponents<MeshRenderer>().Length;

            if (componentCount != 2 || meshCount > 0)
            {
                GameObject go = new GameObject(asset.gameObject.name);
                go.transform.parent = roomSDKDescriptorObject.SDKRoot.transform;
                go.AddComponent<CustomPropertiesContainer>();
                var tracker = go.AddComponent<MVirtualAssetTracker>();

                tracker.GUID = asset.GUID;
                tracker.ExportGLTFEnabled = asset.ExportGLTFEnabled;
                tracker.AssetParams = asset.AssetParams;

                GameObject assetGo = asset.gameObject;
                go.transform.position = assetGo.transform.position;
                go.transform.rotation = assetGo.transform.rotation;

                assetGo.transform.parent = go.transform;
                assetGo.transform.localPosition = Vector3.zero;
                assetGo.transform.localRotation = Quaternion.identity;

                GameObject.DestroyImmediate(asset);
                asset = tracker;
            }

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
                asset.GenerateNewGUID(asset.GUID);
            }
        }

        public static void CollectAssetCustomProperties(MTIONSDKAssetBase asset)
        {
            if (asset == null ||
                asset.ObjectReferenceProp == null)
            {
                return;
            }

            CollectAssetCustomProperties(asset.ObjectReferenceProp);
        }

        public static void CollectAssetCustomProperties(GameObject go)
        {
            CustomPropertiesSimpleContainer updatedProperties = new CustomPropertiesSimpleContainer();

            var components = go.GetComponentsInChildren<Component>();
            foreach (var component in components)
            {
                if (component == null)
                {
                    continue;
                }

                PropertyInfo[] allProps = component.GetType().GetProperties();
                List<PropertyInfo> exposedProps = allProps.Where((PropertyInfo prop) => Attribute.IsDefined(prop, typeof(CustomPropertyAttribute))).ToList();
                foreach (PropertyInfo prop in exposedProps)
                {
                    ICustomProperty customProp = null;
                    IListCustomProperty listCustomProp = null;
                    CustomPropertyAttribute customPropAttribute = prop.GetCustomAttribute<CustomPropertyAttribute>();

                    if (prop.PropertyType == typeof(bool))
                    {
                        BoolCustomProperty boolProp = new BoolCustomProperty(
                            customPropAttribute.BoolDefaultValue);
                        updatedProperties.BoolProperties.Add(boolProp);
                        customProp = boolProp;
                    }
                    else if (prop.PropertyType == typeof(int))
                    {
                        IntCustomProperty intProp = new IntCustomProperty(
                            customPropAttribute.IntDefaultValue,
                            customPropAttribute.IntMinValue,
                            customPropAttribute.IntMaxValue);
                        updatedProperties.IntProperties.Add(intProp);
                        customProp = intProp;
                    }
                    else if (prop.PropertyType == typeof(float))
                    {
                        FloatCustomProperty floatProp = new FloatCustomProperty(
                            customPropAttribute.FloatDefaultValue,
                            customPropAttribute.FloatMinValue,
                            customPropAttribute.FloatMaxValue);
                        updatedProperties.FloatProperties.Add(floatProp);
                        customProp = floatProp;
                    }
                    else if (prop.PropertyType == typeof(string))
                    {
                        StringCustomProperty stringProp = new StringCustomProperty(
                            customPropAttribute.StringDefaultValue);
                        updatedProperties.StringProperties.Add(stringProp);
                        customProp = stringProp;
                    }
                    else if (prop.PropertyType == typeof(List<int>))
                    {
                        ListIntCustomProperty listIntProp = new ListIntCustomProperty(
                            customPropAttribute.IntDefaultValue,
                            customPropAttribute.IntMinValue,
                            customPropAttribute.IntMaxValue);
                        updatedProperties.ListIntProperties.Add(listIntProp);
                        customProp = listIntProp;
                        listCustomProp = listIntProp;
                    }
                    else if (prop.PropertyType == typeof(List<string>))
                    {
                        ListStringCustomProperty listStringProp = new ListStringCustomProperty(
                            customPropAttribute.StringDefaultValue);
                        updatedProperties.ListStringProperties.Add(listStringProp);
                        customProp = listStringProp;
                        listCustomProp = listStringProp;
                    }

                    if (customProp != null)
                    {
                        string declaringTypeName = prop.DeclaringType.AssemblyQualifiedName;
                        string propName = prop.Name;
                        List<int> siblingIndexPath = GetAssetGameobjectSiblingIndexPath(go, component.transform);
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

            CustomPropertiesContainer customPropsContainer = go.GetComponent<CustomPropertiesContainer>();
            if (customPropsContainer == null)
            {
                customPropsContainer = go.AddComponent<CustomPropertiesContainer>();
            }
            customPropsContainer.AddMissing(updatedProperties);
            customPropsContainer.RemoveExtras(updatedProperties);
        }


        private static List<int> GetAssetGameobjectSiblingIndexPath(GameObject go,
            Transform transform)
        {
            var output = new List<int>();
            while (transform != go.transform)
            {
                output.Insert(0, transform.GetSiblingIndex());
                transform = transform.parent;
            }

            return output;
        }
    }
}
