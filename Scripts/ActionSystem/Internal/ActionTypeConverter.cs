using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Security.Cryptography;
using UnityEngine;
using Object = UnityEngine.Object;

namespace mtion.room.sdk.action
{


    public static class TypeConversion
    {
        private const int k_MaxParams = 32;

        private static class ListObjectPool
        {
            private static readonly Queue<List<object>> _pool = new Queue<List<object>>(100);

            public static List<object> Get()
            {
                if (_pool.Count > 0)
                {
                    var list = _pool.Dequeue();
                    list.Clear();
                    
                    return list;
                }

                return new List<object>(k_MaxParams);
            }

            public static void Return(List<object> list)
            {
                _pool.Enqueue(list);
            }
        }

        private static readonly Dictionary<Type, Func<object, Type, object>> _numericConverterCache =
            new Dictionary<Type, Func<object, Type, object>>
            {
                { typeof(int), (param, _) => Convert.ToInt32(param) },
                { typeof(float), (param, _) => Convert.ToSingle(param) },
                { typeof(double), (param, _) => Convert.ToDouble(param) },
                { typeof(bool), (param, _) => Convert.ToBoolean(param) },
                { typeof(string), (param, _) => param.ToString() }
            };

        private static readonly Dictionary<Type, object> _defaultValueCache = new Dictionary<Type, object>();

        private static readonly Dictionary<Type, Func<object>> _ctorCache = new Dictionary<Type, Func<object>>();

        private static object GetDefaultValue(Type type)
        {
            if (_defaultValueCache.TryGetValue(type, out var defaultValue))
            {
                return defaultValue;
            }

            if (type.IsValueType)
            {
                defaultValue = Activator.CreateInstance(type);
            }
            else
            {
                if (!_ctorCache.TryGetValue(type, out var ctor))
                {
                    try
                    {
                        var newExpr = Expression.New(type);
                        var convertExpr = Expression.Convert(newExpr, typeof(object));
                        var lambda = Expression.Lambda<Func<object>>(convertExpr);
                        ctor = lambda.Compile();
                        _ctorCache[type] = ctor;
                    }
                    catch
                    {
                        ctor = () => Activator.CreateInstance(type);
                        _ctorCache[type] = ctor;
                    }
                }
                defaultValue = ctor();
            }

            _defaultValueCache[type] = defaultValue;
            return defaultValue;
        }

        private static object ConvertNumericDirect(object value, Type targetType)
        {
            if (targetType == typeof(int))
                return Convert.ToInt32(value);
            if (targetType == typeof(float))
                return Convert.ToSingle(value);
            if (targetType == typeof(double))
                return Convert.ToDouble(value);
            if (targetType == typeof(bool))
                return Convert.ToBoolean(value);
            if (targetType == typeof(string))
                return value.ToString();
            
            return Convert.ChangeType(value, targetType);
        }

        public static class NumericConverter
        {
            public static bool IsNumericType(Type type)
            {
                switch (Type.GetTypeCode(type))
                {
                    case TypeCode.Byte:
                    case TypeCode.SByte:
                    case TypeCode.UInt16:
                    case TypeCode.UInt32:
                    case TypeCode.UInt64:
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                    case TypeCode.Int64:
                    case TypeCode.Decimal:
                    case TypeCode.Double:
                    case TypeCode.Single:
                        return true;
                    default:
                        return false;
                }
            }

            public static T CastToNumeric<T>(object value)
            {
                if (value == null)
                {
                    return default(T);
                }

                Type targetType = typeof(T);

                return (T)CastToNumeric(value, targetType);
            }

            public static object CastToNumeric(object value, Type targetType)
            {
                if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    targetType = Nullable.GetUnderlyingType(targetType);
                }

                if (!IsNumericType(targetType))
                {
                    return default;
                }

                return Convert.ChangeType(value, targetType);
            }

            public static object ConvertToNumericValue(object input, Type expectedType)
            {
                Type inputType = input.GetType();

                if (expectedType == typeof(int))
                {
                    return Convert.ToInt32(input);
                }
                else if (expectedType == typeof(double))
                {
                    return Convert.ToDouble(input);
                }
                else if (expectedType == typeof(float))
                {
                    return Convert.ToSingle(input);
                }
                else
                {
                    Debug.LogWarning("Unsupported numeric type: " + inputType);
                    return null;
                }
            }
        }

        public static class ContainerConverter
        {
            public static T DeserializeContainer<T>(string jsonString)
            {
                return (T)DeserializeContainer(jsonString, typeof(T));
            }

            public static object DeserializeContainer(string jsonString, Type containerType)
            {
                if (!CanDeserialize(jsonString, containerType))
                {
                    return null;
                }

                try
                {
                    return JsonConvert.DeserializeObject(jsonString, containerType);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Failed to deserialize the string into the container type.");
                }

                return null;
            }

            public static bool CanDeserialize(string jsonString, Type targetType)
            {
                try
                {
                    JsonConvert.DeserializeObject(jsonString, targetType);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            
            public static bool IsContainer(Type type)
            {
                Type[] containerTypes =
                {
                    typeof(List<>),
                    typeof(Dictionary<,>),
                    typeof(HashSet<>),
                    typeof(Array),
                };

                foreach (Type containerType in containerTypes)
                {
                    if (IsAssignableToGenericType(type, containerType))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool IsAssignableToGenericType(Type givenType, Type genericType)
            {
                var interfaceTypes = givenType.GetInterfaces();

                foreach (var interfaceType in interfaceTypes)
                {
                    if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == genericType)
                    {
                        return true;
                    }
                }

                if (givenType.IsGenericType && givenType.GetGenericTypeDefinition() == genericType)
                {
                    return true;
                }

                Type baseType = givenType.BaseType;
                if (baseType == null)
                {
                    return false;
                }

                return IsAssignableToGenericType(baseType, genericType);
            }
        }


        private static Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();


        public static List<object> GenerateParameters(List<object> inputParameters, List<ActionEntryParameterInfo> parameterDefinitions)
        {
            List<object> output = ListObjectPool.Get();

            GenerateParametersNonAlloc(inputParameters, parameterDefinitions, ref output);

            ListObjectPool.Return(output);
            return output;
        }

        public static void GenerateParametersNonAlloc(List<object> inputParameters, List<ActionEntryParameterInfo> parameterDefinitions, ref List<object> output)
        {
            if (output.Capacity < parameterDefinitions.Count)
                output.Capacity = Math.Max(parameterDefinitions.Count, k_MaxParams);

            Type expectedType = null;
            Type inputType = null;
            object param = null;

            for (int i = 0; i < parameterDefinitions.Count; i++)
            {
                ActionEntryParameterInfo paramInfo = parameterDefinitions[i];

                param = i < inputParameters.Count ? inputParameters[i] : null;

                if (!_typeCache.TryGetValue(paramInfo.ParameterType, out expectedType))
                {
                    expectedType = Type.GetType(paramInfo.ParameterType);
                    if (expectedType != null)
                    {
                        _typeCache[paramInfo.ParameterType] = expectedType;
                    }
                    else
                    {
                        Debug.LogWarning($"Could not resolve type: {paramInfo.ParameterType}");
                        _typeCache[paramInfo.ParameterType] = typeof(object);
                        expectedType = typeof(object);
                    }
                }

                if (param == null)
                {
                    if (paramInfo.Metadata != null && paramInfo.Metadata.DefaultValue != null)
                    {
                        param = paramInfo.Metadata.DefaultValue;
                    }
                    else
                    {
                        param = GetDefaultValue(expectedType);
                    }
                    output.Add(param);
                    continue;
                }

                inputType = param.GetType();

                if (inputType.Equals(expectedType))
                {
                    output.Add(param);
                    continue;
                }

                if (NumericConverter.IsNumericType(inputType))
                {
                    if (expectedType.IsEnum)
                    {
                        if (inputType == typeof(int))
                        {
                            param = Enum.ToObject(expectedType, (int)param);
                        }
                        else
                        {
                            param = Enum.ToObject(expectedType, Convert.ToInt32(param));
                        }
                    }
                    else if (NumericConverter.IsNumericType(expectedType))
                    {
                        param = ConvertNumericDirect(param, expectedType);
                    }
                    else if (expectedType == typeof(string))
                    {
                        param = param.ToString();
                    }
                    else
                    {
                        Debug.LogWarning($"Mismatch: Expected {expectedType}, got {inputType}");
                        param = GetDefaultValue(expectedType);
                    }
                }
                else if (inputType == typeof(bool) && expectedType == typeof(string))
                {
                    param = ((bool)param).ToString();
                }
                else
                {
                    Debug.LogWarning($"Mismatch or undefined parameters: Expected {expectedType}, got {inputType}");
                    param = GetDefaultValue(expectedType);
                }

                TypeMetadata metadata = paramInfo.Metadata;
                if (metadata != null && NumericConverter.IsNumericType(expectedType) && metadata is NumericMetadata numericMetadata)
                {
                    float numericValue = NumericConverter.CastToNumeric<float>(param);
                    float clampedValue = Mathf.Clamp(numericValue, numericMetadata.Min, numericMetadata.Max);

                    if (Math.Abs(clampedValue - numericValue) > float.Epsilon)
                    {
                        param = ConvertNumericDirect(clampedValue, expectedType);
                    }
                }

                output.Add(param);
            }
        }

        public static void ReturnToPool(List<object> list)
        {
            if (list != null)
            {
                ListObjectPool.Return(list);
            }
        }
    }
}
