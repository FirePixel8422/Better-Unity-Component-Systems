#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(RenameAttribute))]
public sealed class RenameDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        RenameAttribute attr = (RenameAttribute)attribute;

        // only override text, keep tooltip/context intact
        if (!string.IsNullOrEmpty(attr.label))
        {
            label = new GUIContent(attr.label, label.tooltip);
        }

        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}
#endif