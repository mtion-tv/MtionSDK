using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace mtion.room.sdk.customproperties
{
    [Serializable]
    public abstract class CustomProperty<T> : ICustomProperty
    {
        #region protected attributes

        protected Component _propertyComponent;
        protected PropertyInfo _propertyInfo;
        
        #endregion
        
        #region private attributes
        
        [SerializeField]
        private string _guid = Guid.NewGuid().ToString("n");
        [SerializeField]
        private List<int> _gameObjectSiblingIndexPath;
        [SerializeField]
        private string _declaringTypeName;
        [SerializeField]
        private string _propertyName;
        [SerializeField]
        protected T _defaultValue;
        
        #endregion

        #region public properties
        
        public string GUID => _guid;
        public List<int> GameObjectSiblingIndexPath => new List<int>(_gameObjectSiblingIndexPath);
        public string DeclaringTypeName => _declaringTypeName;
        public string PropertyName => _propertyName;
        public T DefaultValue => _defaultValue;

        #endregion
        
        #region private properties
        
        private bool PropertyLocated => _propertyComponent != null && _propertyInfo != null;
        
        #endregion

        #region public functions

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
        
        public override bool Equals(object obj)
        {
            CustomProperty<T> property = obj as CustomProperty<T>;

            if (property == null)
            {
                return false;
            }

            return DeclaringTypeName == property.DeclaringTypeName && PropertyName == property.PropertyName &&
                   GameObjectSiblingIndexPath.SequenceEqual(property.GameObjectSiblingIndexPath);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(DeclaringTypeName.GetHashCode(), PropertyName.GetHashCode(),
                GameObjectSiblingIndexPath.GetHashCode());
        }
        
        #endregion
    }
}
