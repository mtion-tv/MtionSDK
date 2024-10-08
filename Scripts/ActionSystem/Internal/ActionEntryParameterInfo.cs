using Newtonsoft.Json;
using System;
using UnityEngine;
using System.Collections.Generic;

#if MTION_INTERNAL_BUILD
using mtion.service.api;
#endif

namespace mtion.room.sdk.action
{

    [Serializable]
    public class TypeMetadata
    {
        public T Cast<T>() where T : TypeMetadata
        {
            return this as T;
        }

        public bool Is<T>() where T : TypeMetadata
        {
            return this is T;
        }

        public virtual object DefaultValue { get; }
    }

    [Serializable]
    public class ObjectMetadata : TypeMetadata
    {
        public override object DefaultValue => null;
    }
    
    [Serializable]
    public class BoolMetadata : TypeMetadata
    {
        public override object DefaultValue => false;
    }

    [Serializable]
    public class EnumMetadata : TypeMetadata
    {
        public override object DefaultValue => 0;
    }

    [Serializable]
    public class ContainerMetadata : TypeMetadata
    {
        public int MaxCount = -1;
        
        public override object DefaultValue => null;
    }

    [Serializable]
    public class StringMetadata : TypeMetadata
    {
        public int TotalLength = -1;
        public string Default = "interlinked";
        public List<string> Options = new List<string>();
        
        public override object DefaultValue => Default;
    }

    [Serializable]
    public class NumericMetadata : TypeMetadata
    {
        public float Min = 0;
        public float Max = 1;
        public float Default = 0.5f;
        
        public override object DefaultValue => Default;
    }


    [Serializable]
    public class ActionEntryParameterInfo : ISerializationCallbackReceiver
    {
        public string Name;
        public string ParameterType;
        public string Description = "Enter description here";

        [SerializeReference]
        public TypeMetadata Metadata;

        [SerializeField, HideInInspector]
        private string _internalMetadataCache;

        [SerializeField, HideInInspector]
        private string _internalMetadataType;

        public void OnAfterDeserialize()
        {
            System.Type metadataType = System.Type.GetType(_internalMetadataType);
            Metadata = (TypeMetadata)JsonConvert.DeserializeObject(_internalMetadataCache, metadataType);
        }

        public void OnBeforeSerialize()
        {
            if (Metadata == null)
            {
                if (string.IsNullOrEmpty(_internalMetadataType) ||
                    string.IsNullOrEmpty(_internalMetadataCache))
                {
                    return;
                }

                System.Type metadataType = System.Type.GetType(_internalMetadataType);
                Metadata = (TypeMetadata)JsonConvert.DeserializeObject(_internalMetadataCache, metadataType);
            }

            string fullName = Metadata.GetType().FullName;
            string assemblyFullName = Metadata.GetType().Assembly.FullName;
            _internalMetadataType = $"{fullName}, {assemblyFullName}";
            _internalMetadataCache = JsonConvert.SerializeObject(Metadata,
                Formatting.None,
                new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });
        }

#if MTION_INTERNAL_BUILD
        public static ParameterType GetParameterType(string parameterType)
        {
            Type expectedType = Type.GetType(parameterType);
            if (expectedType.IsEnum)
            {
                return service.api.ParameterType.ENUM;
            }
            else if (TypeConversion.NumericConverter.IsNumericType(expectedType))
            {
                return service.api.ParameterType.NUMBER;
            }
            else if (Type.GetTypeCode(expectedType) == TypeCode.Boolean)
            {
                return service.api.ParameterType.BOOLEAN;
            }
            else if (expectedType == typeof(GameObject))
            {
                return service.api.ParameterType.GAMEOBJECT;
            }
            else if (expectedType == typeof (SignalDataType))
            {
                return service.api.ParameterType.SIGNAL;
            }

            return service.api.ParameterType.STRING;
        }
#endif 

    }
}
