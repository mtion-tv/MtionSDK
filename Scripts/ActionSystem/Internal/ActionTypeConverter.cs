using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using Object = UnityEngine.Object;

namespace mtion.room.sdk.action
{
    ////////////////////////////////////////////////////////////////////////////////////////
    ///  Util
    ////////////////////////////////////////////////////////////////////////////////////////




    ////////////////////////////////////////////////////////////////////////////////////////
    ///  Type Converter
    ////////////////////////////////////////////////////////////////////////////////////////

    public static class TypeConversion
    {

        ////////////////////////////////////////////////////////////////////////////////////////
        ///  Numeric Converter
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


        ////////////////////////////////////////////////////////////////////////////////////////
        ///  Container Converter
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
            // TODO: Add other container types here
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


        /// <summary>
        /// Verifies and converts a list of incoming data with the expected parameter definitions
        /// </summary>
        /// <param name="input"></param>
        /// <param name="blueprint"></param>
        /// <returns></returns>
        public static List<object> GenerateParameters(List<object> inputParameters, List<ActionEntryParameterInfo> parameterDefinitions)
        {
            List<object> output = new List<object>();

            for (int i = 0; i < parameterDefinitions.Count; i++)
            {
                ActionEntryParameterInfo paramInfo = parameterDefinitions[i];

                object param = null;
                if (i < inputParameters.Count)
                {
                    param = inputParameters[i];
                }

                // Get type information
                Type expectedType = Type.GetType(paramInfo.ParameterType);
                Type inputType = param == null ? typeof(object) : param.GetType();

                // Add to parameter list
                if (!inputType.Equals(expectedType))
                {
                    if (NumericConverter.IsNumericType(inputType) && NumericConverter.IsNumericType(expectedType))
                    {
                        // Try to cast to numertic type
                        param = NumericConverter.ConvertToNumericValue(param, expectedType);
                    }
                    else
                    {
                        Debug.LogWarning("Mismatch or undefined parameters between incoming parameters and registered parameters in Action");
                        param = null;

                        // TODO: Try to force conversion here? 
                        //       Mostly likely not a good solution as it can have unintended behavior or security converns
                    }
                }

                // Attempt to set defaults as defined by the asset developer. else, we set to new instance of the object's type
                if (param == null)
                {
                    if (NumericConverter.IsNumericType(expectedType) && paramInfo.Metadata.Is<NumericMetadata>())
                    {
                        NumericMetadata numericMetadata = paramInfo.Metadata.Cast<NumericMetadata>();
                        param = numericMetadata.Default;
                    }
                    else if (expectedType == typeof(string) && paramInfo.Metadata.Is<StringMetadata>())
                    {
                        StringMetadata stringMetadata = paramInfo.Metadata.Cast<StringMetadata>();
                        param = stringMetadata.Default;
                    }
                    else
                    {
                        // Set to new instance of type
                        param = Activator.CreateInstance(expectedType);
                    }
                }

                // Enforce constraints
                if (NumericConverter.IsNumericType(expectedType) && paramInfo.Metadata.Is<NumericMetadata>())
                {
                    NumericMetadata numericMetadata = paramInfo.Metadata.Cast<NumericMetadata>();
                    param = Mathf.Clamp(NumericConverter.CastToNumeric<float>(param), numericMetadata.Min, numericMetadata.Max);
                    param = NumericConverter.ConvertToNumericValue(param, expectedType);
                }
                else if (ContainerConverter.IsContainer(expectedType) && paramInfo.Metadata.Is<ContainerMetadata>())
                {
                    ContainerMetadata containerMetadata = paramInfo.Metadata.Cast<ContainerMetadata>();

                    // TODO:
                }
                else if (expectedType == typeof(string) && paramInfo.Metadata.Is<StringMetadata>())
                {
                    StringMetadata stringMetadata = paramInfo.Metadata.Cast<StringMetadata>();
                    // TODO:

                }
                else if (expectedType == typeof(bool) && paramInfo.Metadata.Is<BoolMetadata>())
                {
                    BoolMetadata boolMetadata = paramInfo.Metadata.Cast<BoolMetadata>();
                    // TODO:


                }
                else if (expectedType == typeof(Object) && paramInfo.Metadata.Is<ObjectMetadata>())
                {
                    ObjectMetadata objectMetadata = paramInfo.Metadata.Cast<ObjectMetadata>();
                    // TODO:

                }
                else
                {
                    // TODO: others
                }


                output.Add(param);
            }

            return output;
        }





    }
}
