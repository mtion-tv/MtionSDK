using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk.customproperties
{
    public interface ICustomProperty
    {
        public string GUID { get; }
        public List<int> GameObjectSiblingIndexPath { get; }
        public string DeclaringTypeName { get; }
        public string PropertyName { get; }

        public void SetPropertyMetadata(string declaringTypeName, string propertyName, List<int> siblingIndexPath);
        public void LocateProperty(GameObject exportedAssetGO);
    }
}
