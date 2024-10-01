using UnityEditor;
using UnityEngine;
using mtion.room.sdk.action;
using System;
using System.Collections.Generic;
using System.Reflection;
using Object = UnityEngine.Object;
using mtion.room.sdk.compiled;

namespace mtion.room.sdk.action.editor
{
    [CustomEditor(typeof(MActionBehaviour))]
    public class MActionBehaviourEditor : Editor
    {
        private MActionBehaviour actionBh;
        private Vector2 _scrollPos;
        private bool _showEntryPoints = true;
        private bool _showExitPoints = true;
        private bool _showExitParameterProviders = true;

        private void OnEnable()
        {
            actionBh = (MActionBehaviour)target;
        }

        public override void OnInspectorGUI()
        {
            StartBox();
            {
                EditorGUILayout.LabelField("Action Configuration", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                DrawField("Deprecated", ref actionBh.Deprecated);
                DrawField("Active", ref actionBh.Active);
                EditorGUILayout.EndHorizontal();

                DrawField("Action Name", ref actionBh.ActionName);
                DrawField("Action Description", ref actionBh.ActionDescription);
                DrawField("Action Node Type", ref actionBh.ActionNodeType);

#if UNITY_EDITOR
                DrawField("Default Chat Command", ref actionBh.DefaultChatCommand);
#endif

                DrawField("GUID", ref actionBh.Guid);

                EditorGUILayout.Space();
                _showEntryPoints = EditorGUILayout.Foldout(_showEntryPoints, "Action Entry Points", true);
                if (_showEntryPoints)
                {
                    EditorGUI.indentLevel++;
                    for (int i = 0; i < actionBh.ActionEntryPoints.Count; i++)
                    {
                        DrawActionEntryPoint(actionBh.ActionEntryPoints[i], i);
                    }

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("+", GUILayout.Width(30)))
                    {
                        actionBh.ActionEntryPoints.Add(new ActionEntryPointInternal
                        {
                            EntryPoint = new ActionEntryPointInfo
                            {
                                Guid = SDKUtil.GenerateNewGUID()
                            }
                        });
                        EditorUtility.SetDirty(actionBh);
                    }
                    if (GUILayout.Button("-", GUILayout.Width(30)))
                    {
                        if (actionBh.ActionEntryPoints.Count > 0)
                        {
                            actionBh.ActionEntryPoints.RemoveAt(actionBh.ActionEntryPoints.Count - 1);
                            EditorUtility.SetDirty(actionBh);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space();
                _showExitPoints = EditorGUILayout.Foldout(_showExitPoints, "Action Exit Points", true);
                if (_showExitPoints)
                {
                    EditorGUI.indentLevel++;
                    for (int i = 0; i < actionBh.ActionExitPoints.Count; i++)
                    {
                        DrawActionExitPoint(actionBh.ActionExitPoints[i], i);
                    }

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("+", GUILayout.Width(30)))
                    {
                        actionBh.ActionExitPoints.Add(new ActionExitPointInternal
                        {
                            ExitPoint = new ActionExitPointInfo
                            {
                                Guid = SDKUtil.GenerateNewGUID()
                            }
                        });
                        EditorUtility.SetDirty(actionBh);
                    }
                    if (GUILayout.Button("-", GUILayout.Width(30)))
                    {
                        if (actionBh.ActionExitPoints.Count > 0)
                        {
                            actionBh.ActionExitPoints.RemoveAt(actionBh.ActionExitPoints.Count - 1);
                            EditorUtility.SetDirty(actionBh);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space();
                _showExitParameterProviders = EditorGUILayout.Foldout(_showExitParameterProviders, "Action Exit Parameters Providers", true);
                if (_showExitParameterProviders)
                {
                    EditorGUI.indentLevel++;
                    for (int i = 0; i < actionBh.ActionExitParameterProviders.Count; i++)
                    {
                        DrawActionExitParameterProvider(actionBh.ActionExitParameterProviders[i], i);
                    }

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("+", GUILayout.Width(30)))
                    {
                        actionBh.ActionExitParameterProviders.Add(new ActionExitParametersProviderInternal
                        {
                            Parameters = new List<ActionExitParameterInfo>()
                        });
                        EditorUtility.SetDirty(actionBh);
                    }
                    if (GUILayout.Button("-", GUILayout.Width(30)))
                    {
                        if (actionBh.ActionExitParameterProviders.Count > 0)
                        {
                            actionBh.ActionExitParameterProviders.RemoveAt(actionBh.ActionExitParameterProviders.Count - 1);
                            EditorUtility.SetDirty(actionBh);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space();
                if (GUILayout.Button("Populate Entry Points"))
                {
                    MethodInfo populateMethod = target.GetType().GetMethod("PopulateEntryPoints",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (populateMethod != null)
                    {
                        populateMethod.Invoke(target, null);
                        EditorUtility.SetDirty(target);
                    }
                }
            }
            EndBox();
        }

        private void DrawActionEntryPoint(ActionEntryPointInternal entryPoint, int index)
        {
            StartBox();
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Entry Point " + index, EditorStyles.boldLabel);
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    actionBh.ActionEntryPoints.RemoveAt(index);
                    EditorUtility.SetDirty(actionBh);
                    return;
                }
                EditorGUILayout.EndHorizontal();

                DrawField("Target", ref entryPoint.Target);

                DrawActionEntryPointInfo(entryPoint.EntryPoint);
            }
            EndBox();
        }

        private void DrawActionEntryPointInfo(ActionEntryPointInfo entryPointInfo)
        {
            EditorGUI.indentLevel++;
            DrawField("GUID", ref entryPointInfo.Guid);

            EditorGUILayout.LabelField("Parameter Definitions", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            for (int i = 0; i < entryPointInfo.ParameterDefinitions.Count; i++)
            {
                DrawActionEntryParameterInfo(entryPointInfo.ParameterDefinitions[i], i, entryPointInfo.ParameterDefinitions);
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+", GUILayout.Width(30)))
            {
                entryPointInfo.ParameterDefinitions.Add(new ActionEntryParameterInfo());
                EditorUtility.SetDirty(actionBh);
            }
            if (GUILayout.Button("-", GUILayout.Width(30)))
            {
                if (entryPointInfo.ParameterDefinitions.Count > 0)
                {
                    entryPointInfo.ParameterDefinitions.RemoveAt(entryPointInfo.ParameterDefinitions.Count - 1);
                    EditorUtility.SetDirty(actionBh);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
            EditorGUI.indentLevel--;
        }

        private void DrawActionEntryParameterInfo(ActionEntryParameterInfo paramDef, int index, List<ActionEntryParameterInfo> paramList)
        {
            StartBox();
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Parameter " + index, EditorStyles.boldLabel);
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    paramList.RemoveAt(index);
                    EditorUtility.SetDirty(actionBh);
                    return;
                }
                EditorGUILayout.EndHorizontal();

                DrawField("Name", ref paramDef.Name);
                DrawField("Parameter Type", ref paramDef.ParameterType);
                DrawField("Description", ref paramDef.Description);

                EditorGUILayout.LabelField("Metadata", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                DrawTypeMetadata(paramDef);
                EditorGUI.indentLevel--;
            }
            EndBox();
        }

        private void DrawTypeMetadata(ActionEntryParameterInfo paramDef)
        {
            if (paramDef == null)
            {
                return;
            }

            List<Type> metadataTypes = new List<Type>()
            {
                typeof(BoolMetadata),
                typeof(StringMetadata),
                typeof(NumericMetadata),
                typeof(EnumMetadata),
                typeof(ObjectMetadata),
                typeof(ContainerMetadata)
            };

            List<string> typeNames = new List<string>();
            foreach (var t in metadataTypes)
            {
                typeNames.Add(t.Name);
            }

            int selectedIndex = -1;
            if (paramDef.Metadata != null)
            {
                selectedIndex = metadataTypes.FindIndex(t => t == paramDef.Metadata.GetType());
            }

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                int newIndex = EditorGUILayout.Popup("Metadata Type", selectedIndex, typeNames.ToArray());
                if (check.changed)
                {
                    Type newType = metadataTypes[newIndex];
                    paramDef.Metadata = (TypeMetadata)Activator.CreateInstance(newType);
                    EditorUtility.SetDirty(actionBh);
                }
            }

            if (paramDef.Metadata != null)
            {
                Type metadataType = paramDef.Metadata.GetType();
                FieldInfo[] fields = metadataType.GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (FieldInfo field in fields)
                {
                    object fieldValue = field.GetValue(paramDef.Metadata);

                    if (field.FieldType == typeof(int))
                    {
                        int value = (int)fieldValue;
                        DrawField(field.Name, ref value);
                        if (!Equals(value, fieldValue))
                        {
                            field.SetValue(paramDef.Metadata, value);
                            EditorUtility.SetDirty(actionBh);
                        }
                    }
                    else if (field.FieldType == typeof(float))
                    {
                        float value = (float)fieldValue;
                        DrawField(field.Name, ref value);
                        if (!Equals(value, fieldValue))
                        {
                            field.SetValue(paramDef.Metadata, value);
                            EditorUtility.SetDirty(actionBh);
                        }
                    }
                    else if (field.FieldType == typeof(bool))
                    {
                        bool value = (bool)fieldValue;
                        DrawField(field.Name, ref value);
                        if (!Equals(value, fieldValue))
                        {
                            field.SetValue(paramDef.Metadata, value);
                            EditorUtility.SetDirty(actionBh);
                        }
                    }
                    else if (field.FieldType == typeof(string))
                    {
                        string value = (string)fieldValue;
                        DrawField(field.Name, ref value);
                        if (!Equals(value, fieldValue))
                        {
                            field.SetValue(paramDef.Metadata, value);
                            EditorUtility.SetDirty(actionBh);
                        }
                    }
                    else if (field.FieldType.IsEnum)
                    {
                        var value = (Enum)fieldValue;
                        DrawField(field.Name, ref value);
                        if (!Equals(value, fieldValue))
                        {
                            field.SetValue(paramDef.Metadata, value);
                            EditorUtility.SetDirty(actionBh);
                        }
                    }
                    else if (typeof(IList<string>).IsAssignableFrom(field.FieldType))
                    {
                        IList<string> list = (IList<string>)fieldValue;
                        EditorGUILayout.LabelField(field.Name);
                        EditorGUI.indentLevel++;
                        int listSize = list.Count;
                        int newListSize = listSize;
                        using (var check = new EditorGUI.ChangeCheckScope())
                        {
                            newListSize = EditorGUILayout.IntField("Size", listSize);
                            if (check.changed)
                            {
                                while (newListSize > list.Count)
                                    list.Add("");
                                while (newListSize < list.Count)
                                    list.RemoveAt(list.Count - 1);
                                EditorUtility.SetDirty(actionBh);
                            }
                        }
                        for (int i = 0; i < list.Count; i++)
                        {
                            string element = list[i];
                            DrawField("Element " + i, ref element);
                            if (element != list[i])
                            {
                                list[i] = element;
                                EditorUtility.SetDirty(actionBh);
                            }
                        }
                        EditorGUI.indentLevel--;
                    }
                    else
                    {
                        EditorGUILayout.LabelField(field.Name, "Unsupported field type");
                    }
                }
            }
        }

        private void DrawActionExitPoint(ActionExitPointInternal exitPoint, int index)
        {
            StartBox();
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Exit Point " + index, EditorStyles.boldLabel);
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    actionBh.ActionExitPoints.RemoveAt(index);
                    EditorUtility.SetDirty(actionBh);
                    return;
                }
                EditorGUILayout.EndHorizontal();

                DrawField("Target", ref exitPoint.Target);

                DrawActionExitPointInfo(exitPoint.ExitPoint);
            }
            EndBox();
        }

        private void DrawActionExitPointInfo(ActionExitPointInfo exitPointInfo)
        {
            EditorGUI.indentLevel++;
            DrawField("GUID", ref exitPointInfo.Guid);
            DrawField("Name", ref exitPointInfo.Name);
            DrawField("Description", ref exitPointInfo.Description);
            EditorGUI.indentLevel--;
        }

        private void DrawActionExitParameterProvider(ActionExitParametersProviderInternal parameterProvider, int index)
        {
            StartBox();
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Parameter Provider " + index, EditorStyles.boldLabel);
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    actionBh.ActionExitParameterProviders.RemoveAt(index);
                    EditorUtility.SetDirty(actionBh);
                    return;
                }
                EditorGUILayout.EndHorizontal();

                DrawField("Target", ref parameterProvider.Target);

                EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;

                for (int i = 0; i < parameterProvider.Parameters.Count; i++)
                {
                    DrawActionExitParameterInfo(parameterProvider.Parameters[i], i, parameterProvider.Parameters);
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+", GUILayout.Width(30)))
                {
                    parameterProvider.Parameters.Add(new ActionExitParameterInfo());
                    EditorUtility.SetDirty(actionBh);
                }
                if (GUILayout.Button("-", GUILayout.Width(30)))
                {
                    if (parameterProvider.Parameters.Count > 0)
                    {
                        parameterProvider.Parameters.RemoveAt(parameterProvider.Parameters.Count - 1);
                        EditorUtility.SetDirty(actionBh);
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }
            EndBox();
        }

        private void DrawActionExitParameterInfo(ActionExitParameterInfo parameterInfo, int index, List<ActionExitParameterInfo> paramList)
        {
            StartBox();
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Parameter " + index, EditorStyles.boldLabel);
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    paramList.RemoveAt(index);
                    EditorUtility.SetDirty(actionBh);
                    return;
                }
                EditorGUILayout.EndHorizontal();

                DrawField("GUID", ref parameterInfo.Guid);
                DrawField("Name", ref parameterInfo.Name);
                DrawField("Parameter Type", ref parameterInfo.ParameterType);
            }
            EndBox();
        }


        private void StartBox(bool alt = false)
        {
            GUIStyle modifiedBox = new GUIStyle(GUI.skin.box);

            if (alt)
            {
                modifiedBox.normal.background = Texture2D.whiteTexture;
            }

            EditorGUILayout.BeginHorizontal(modifiedBox);
            GUILayout.Label(string.Empty, GUILayout.MaxWidth(5));

            EditorGUILayout.BeginVertical();
            GUILayout.Label(string.Empty, GUILayout.MaxHeight(5));
        }

        private void EndBox()
        {
            GUILayout.Label(string.Empty, GUILayout.MaxHeight(5));
            EditorGUILayout.EndVertical();

            GUILayout.Label(string.Empty, GUILayout.MaxWidth(5));
            EditorGUILayout.EndHorizontal();

            GUILayout.Label(string.Empty, GUILayout.MaxHeight(5));
        }

        private void DrawField<T>(string label, ref T value)
        {
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                T newValue = default(T);

                if (typeof(T) == typeof(bool))
                {
                    newValue = (T)(object)EditorGUILayout.Toggle(label, (bool)(object)value);
                }
                else if (typeof(T) == typeof(int))
                {
                    newValue = (T)(object)EditorGUILayout.IntField(label, (int)(object)value);
                }
                else if (typeof(T) == typeof(float))
                {
                    newValue = (T)(object)EditorGUILayout.FloatField(label, (float)(object)value);
                }
                else if (typeof(T) == typeof(string))
                {
                    newValue = (T)(object)EditorGUILayout.TextField(label, (string)(object)value);
                }
                else if (typeof(T).IsEnum)
                {
                    newValue = (T)(object)EditorGUILayout.EnumPopup(label, (Enum)(object)value);
                }
                else if (typeof(Object).IsAssignableFrom(typeof(T)))
                {
                    newValue = (T)(object)EditorGUILayout.ObjectField(label, (Object)(object)value, typeof(T), true);
                }
                else
                {
                    EditorGUILayout.LabelField(label, "Unsupported type");
                    return;
                }

                if (check.changed)
                {
                    value = newValue;
                    EditorUtility.SetDirty(actionBh);
                }
            }
        }
    }
}
