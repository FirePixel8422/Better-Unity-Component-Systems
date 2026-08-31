#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ShowIfAttribute))]
public sealed class ShowIfDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!ShouldShow(property))
            return 0f;

        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(
        Rect position,
        SerializedProperty property,
        GUIContent label)
    {
        if (!ShouldShow(property))
            return;

        EditorGUI.PropertyField(position, property, label, true);
    }

    private bool ShouldShow(SerializedProperty property)
    {
        ShowIfAttribute attribute = (ShowIfAttribute)this.attribute;

        SerializedProperty condition = FindCondition(property, attribute.condition);

        if (condition == null)
            return true;

        return Evaluate(condition);
    }

    private static SerializedProperty FindCondition(
        SerializedProperty property,
        string conditionName)
    {
        // Try the same parent first.
        SerializedProperty parent = GetParentProperty(property);

        if (parent != null)
        {
            SerializedProperty relative =
                parent.FindPropertyRelative(conditionName);

            if (relative != null)
                return relative;

            relative = parent.FindPropertyRelative(
                $"<{conditionName}>k__BackingField");

            if (relative != null)
                return relative;
        }

        // Root-level fallback.
        SerializedProperty root =
            property.serializedObject.FindProperty(conditionName);

        if (root != null)
            return root;

        return property.serializedObject.FindProperty(
            $"<{conditionName}>k__BackingField");
    }

    private static SerializedProperty GetParentProperty(
        SerializedProperty property)
    {
        string path = property.propertyPath;

        int lastDot = path.LastIndexOf('.');

        if (lastDot < 0)
            return null;

        string parentPath = path.Substring(0, lastDot);

        return property.serializedObject.FindProperty(parentPath);
    }

    private static bool Evaluate(SerializedProperty property)
    {
        return property.propertyType switch
        {
            SerializedPropertyType.Boolean =>
                property.boolValue,

            SerializedPropertyType.Integer =>
                property.intValue != 0,

            SerializedPropertyType.Float =>
                property.floatValue != 0f,

            SerializedPropertyType.ObjectReference =>
                property.objectReferenceValue != null,

            SerializedPropertyType.Enum =>
                property.enumValueIndex != 0,

            _ => true
        };
    }
}
#endif