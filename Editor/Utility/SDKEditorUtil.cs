using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using mtion.room.sdk.compiled;
using UnityEditor;
using UnityEngine;

namespace mtion.room.sdk
{
    public class SDKEditorUtil
    {
        public static void InitAddressableAssetFields(MTIONSDKAssetBase assetBase, MTIONObjectType objectType, string name = "")
        {
            if (string.IsNullOrEmpty(assetBase.GUID))
            {
                assetBase.GenerateNewGUID();
                assetBase.CreateTimeMS = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            if (!string.IsNullOrEmpty(name) && string.IsNullOrEmpty(assetBase.Name))
            {
                assetBase.Name = name;
                assetBase.gameObject.name = name;
            }
            else if (string.IsNullOrEmpty(assetBase.Name))
            {
                assetBase.Name = assetBase.gameObject.name;
            }
            else
            {
                assetBase.gameObject.name = assetBase.Name;
            }

            assetBase.InternalID = SDKUtil.GetSDKInternalID(assetBase.GUID);
            assetBase.ObjectType = objectType;
            EditorUtility.SetDirty(assetBase);
        }

        public static void InitVirtualComponentFields(VirtualComponentTracker virtualComponent)
        {
            if (string.IsNullOrEmpty(virtualComponent.GUID))
            {
                virtualComponent.GenerateNewGUID();
            }
        }
    }
}
