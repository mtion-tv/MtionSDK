using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace mtion.room.sdk.customproperties
{
    [Serializable]
    public abstract class CustomProperty<T> : ICustomProperty
    {
        [SerializeField] private string _guid = Guid.NewGuid().ToString("n");
        [SerializeField] private List<int> _gameObjectSiblingIndexPath;
        [SerializeField] private string _declaringTypeName;
        [SerializeField] private string _propertyName;
        [SerializeField] protected T _defaultValue;

        protected Component _propertyComponent;
        protected PropertyInfo _propertyInfo;

        private bool PropertyLocated => _propertyComponent != null && _propertyInfo != null;

        public string GUID => _guid;
        public List<int> GameObjectSiblingIndexPath => new List<int>(_gameObjectSiblingIndexPath);
        public string DeclaringTypeName => _declaringTypeName;
        public string PropertyName => _propertyName;
        public T DefaultValue => _defaultValue;

        public void SetPropertyMetadata(string declaringTypeName, string propertyName, List<int> siblingIndexPath)
        {
            _declaringTypeName = declaringTypeName;
            _propertyName = propertyName;
            _gameObjectSiblingIndexPath = siblingIndexPath;
        }

        public void LocateProperty(GameObject exportedAssetGO)
        {
            var transform = exportedAssetGO.transform;
            foreach (var index in _gameObjectSiblingIndexPath)
            {
                if (index >= transform.childCount)
                {
                    return;
                }

                transform = transform.GetChild(index);
            }

            var componentType = Type.GetType(_declaringTypeName);
            if (componentType != null)
            {
                _propertyComponent = transform.GetComponent(componentType);
                _propertyInfo = componentType.GetProperty(_propertyName);
            }
        }

        public T GetValue()
        {
            if (!PropertyLocated)
            {
                return default;
            }

            return (T)_propertyInfo.GetValue(_propertyComponent);
        }


        public void SetValue(T value)
        {
            if (!PropertyLocated)
            {
                return;
            }

            var cleanedVal = CleanValue(value);
            _propertyInfo.SetValue(_propertyComponent, cleanedVal);
        }

        public abstract T CleanValue(T value);
    }
}
