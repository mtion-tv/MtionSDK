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
            var componentType = Type.GetType(_declaringTypeName);
            
            if (componentType == null)
            {
                componentType = FindTypeByName(_declaringTypeName);
            }
            
            if (componentType == null)
            {
                Debug.LogWarning($"[CustomProperty] Failed to resolve type: {_declaringTypeName}");
                return;
            }

            Transform targetTransform = exportedAssetGO.transform;
            if (_gameObjectSiblingIndexPath != null && _gameObjectSiblingIndexPath.Count > 0)
            {
                foreach (int index in _gameObjectSiblingIndexPath)
                {
                    if (index < 0 || index >= targetTransform.childCount)
                    {
                        Debug.LogWarning($"[CustomProperty] Invalid sibling path for {_propertyName}. " +
                            $"Index {index} out of range (childCount={targetTransform.childCount}). " +
                            "Falling back to GetComponentInChildren.");
                        targetTransform = null;
                        break;
                    }
                    targetTransform = targetTransform.GetChild(index);
                }
            }

            if (targetTransform != null)
            {
                _propertyComponent = targetTransform.GetComponent(componentType);
            }
            
            if (_propertyComponent == null)
            {
                _propertyComponent = exportedAssetGO.transform.GetComponentInChildren(componentType);
                if (_propertyComponent == null)
                {
                    _propertyComponent = exportedAssetGO.transform.GetComponent(componentType);
                }
            }

            if (_propertyComponent == null)
            {
                Debug.LogWarning($"[CustomProperty] Failed to find component {componentType.Name} " +
                    $"for property {_propertyName}");
                return;
            }

            _propertyInfo = componentType.GetProperty(_propertyName);
            if (_propertyInfo == null)
            {
                Debug.LogWarning($"[CustomProperty] Property {_propertyName} not found on {componentType.Name}");
            }
        }
        
        private static Type FindTypeByName(string assemblyQualifiedName)
        {
            if (string.IsNullOrEmpty(assemblyQualifiedName))
            {
                return null;
            }
            
            string typeName = assemblyQualifiedName.Split(',')[0].Trim();
            
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null) return type;
            }
            return null;
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
