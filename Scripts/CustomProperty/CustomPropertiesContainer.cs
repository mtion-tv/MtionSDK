using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace mtion.room.sdk.customproperties
{
    public interface IPropertiesContainer
    {
        #region public properties
        
        List<BoolCustomProperty> BoolProperties { get; }
        List<IntCustomProperty> IntProperties { get; }
        List<FloatCustomProperty> FloatProperties { get; }
        List<StringCustomProperty> StringProperties { get; }

        List<ListIntCustomProperty> ListIntProperties { get; }
        List<ListStringCustomProperty> ListStringProperties { get; }
        
        #endregion
    }
    
    public class CustomPropertiesSimpleContainer : IPropertiesContainer
    {
        #region public properties
        
        public List<BoolCustomProperty> BoolProperties { get; set; } = new List<BoolCustomProperty>();
        public List<IntCustomProperty> IntProperties { get; set; } = new List<IntCustomProperty>();
        public List<FloatCustomProperty> FloatProperties { get; set; } = new List<FloatCustomProperty>();
        public List<StringCustomProperty> StringProperties { get; set; } = new List<StringCustomProperty>();

        public List<ListIntCustomProperty> ListIntProperties { get; set; } = new List<ListIntCustomProperty>();
        public List<ListStringCustomProperty> ListStringProperties { get; set; } = new List<ListStringCustomProperty>();
        
        #endregion
    }
    
    public class CustomPropertiesContainer : MonoBehaviour, IPropertiesContainer
    {
        #region private attributes

        [SerializeField] private List<BoolCustomProperty> BoolCustomProperties = new List<BoolCustomProperty>();
        [SerializeField] private List<IntCustomProperty> IntCustomProperties = new List<IntCustomProperty>();
        [SerializeField] private List<FloatCustomProperty> FloatCustomProperties = new List<FloatCustomProperty>();
        [SerializeField] private List<StringCustomProperty> StringCustomProperties = new List<StringCustomProperty>();

        [SerializeField] private List<ListIntCustomProperty> ListIntCustomProperties = new List<ListIntCustomProperty>();
        [SerializeField] private List<ListStringCustomProperty> ListStringCustomProperties = new List<ListStringCustomProperty>();

        #endregion
        

        #region public properties
        
        public List<BoolCustomProperty> BoolProperties => BoolCustomProperties;
        public List<IntCustomProperty> IntProperties => IntCustomProperties;
        public List<FloatCustomProperty> FloatProperties => FloatCustomProperties;
        public List<StringCustomProperty> StringProperties => StringCustomProperties;
        
        public List<ListIntCustomProperty> ListIntProperties => ListIntCustomProperties;
        public List<ListStringCustomProperty> ListStringProperties => ListStringCustomProperties;
        
        #endregion

        #region public functions

        public void AddMissing(IPropertiesContainer properties)
        {
            AddMissing(BoolCustomProperties, properties.BoolProperties);
            AddMissing(IntCustomProperties, properties.IntProperties);
            AddMissing(FloatCustomProperties, properties.FloatProperties);
            AddMissing(StringCustomProperties, properties.StringProperties);
            
            AddMissing(ListIntCustomProperties, properties.ListIntProperties);
            AddMissing(ListStringCustomProperties, properties.ListStringProperties);
        }

        public void RemoveExtras(IPropertiesContainer properties)
        {
            RemoveExtras(BoolCustomProperties, properties.BoolProperties);
            RemoveExtras(IntCustomProperties, properties.IntProperties);
            RemoveExtras(FloatCustomProperties, properties.FloatProperties);
            RemoveExtras(StringCustomProperties, properties.StringProperties);
            
            RemoveExtras(ListIntCustomProperties, properties.ListIntProperties);
            RemoveExtras(ListStringCustomProperties, properties.ListStringProperties);
        }

        public List<ICustomProperty> GetAllProperties()
        {
            List<ICustomProperty> output = new List<ICustomProperty>();
            output.AddRange(BoolCustomProperties);
            output.AddRange(IntCustomProperties);
            output.AddRange(FloatCustomProperties);
            output.AddRange(StringCustomProperties);
            output.AddRange(ListIntCustomProperties); 
            output.AddRange(ListStringCustomProperties);
            return output;
        }
        

        #endregion
        
        #region private functions

        private void AddMissing<T>(List<T> target, List<T> source) where T : ICustomProperty
        {
            foreach (T property in source)
            {
                if (!target.Contains(property))
                {
                    target.Add(property);
                }
            }
        }

        private void RemoveExtras<T>(List<T> target, List<T> source) where T : ICustomProperty
        {
            for(int i = target.Count - 1; i >= 0; i--)
            {
                if (!source.Contains(target[i]))
                {
                    target.RemoveAt(i);
                }
            }
        }
        
        #endregion
    }
}
