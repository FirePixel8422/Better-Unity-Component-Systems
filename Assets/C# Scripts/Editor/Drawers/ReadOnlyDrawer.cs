#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EditorReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // IMPORTANT: lets Unity correctly calculate foldout + children height
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginDisabledGroup(true);

        // 'true' = draw children, required for class/struct foldouts
        EditorGUI.PropertyField(position, property, label, true);

        EditorGUI.EndDisabledGroup();
    }
}
#endif