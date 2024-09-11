using UnityEditor;
using UnityEngine;

namespace mtion.room.sdk.compiled
{
    [CustomPropertyDrawer(typeof(LayerAttribute))]
    public sealed class LayerAttributeEditor : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            property.intValue = EditorGUI.LayerField(position, label,  property.intValue);
        }
    }
}
