#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ShowIfAttribute))]
public sealed class ShowIfDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!ShouldShow(property))
        {
            return 0f;
        }

        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (!ShouldShow(property))
        {
            return;
        }

        EditorGUI.PropertyField(position, property, label, true);
    }

    private bool ShouldShow(SerializedProperty property)
    {
        ShowIfAttribute attr = (ShowIfAttribute)attribute;

        SerializedProperty condition = FindCondition(property, attr.condition);

        if (condition == null)
        {
            return true;
        }

        return Evaluate(condition);
    }

    private static SerializedProperty FindCondition(SerializedProperty property, string condition)
    {
        SerializedObject obj = property.serializedObject;

        // 1. direct field
        SerializedProperty direct = obj.FindProperty(condition);
        if (direct != null)
        {
            return direct;
        }

        // 2. IMPORTANT FIX: scan all properties (handles backing fields correctly)
        SerializedProperty iterator = obj.GetIterator();

        while (iterator.NextVisible(true))
        {
            if (IsMatch(iterator.name, condition))
            {
                return iterator.Copy();
            }
        }

        // 3. fallback: relative path resolution
        string path = property.propertyPath;
        int lastDot = path.LastIndexOf('.');

        while (lastDot >= 0)
        {
            string prefix = path.Substring(0, lastDot + 1);
            SerializedProperty candidate = obj.FindProperty(prefix + condition);

            if (candidate != null)
            {
                return candidate;
            }

            path = path.Substring(0, lastDot);
            lastDot = path.LastIndexOf('.');
        }

        return null;
    }

    private static bool IsMatch(string serializedName, string condition)
    {
        // handles:
        // <X>k__BackingField
        // X
        // _x
        if (serializedName == condition)
        {
            return true;
        }

        if (serializedName == $"<{condition}>k__BackingField")
        {
            return true;
        }

        if (serializedName.EndsWith(condition))
        {
            return true;
        }

        return false;
    }

    private static bool Evaluate(SerializedProperty prop)
    {
        return prop.propertyType switch
        {
            SerializedPropertyType.Boolean => prop.boolValue,
            SerializedPropertyType.ObjectReference => prop.objectReferenceValue != null,
            SerializedPropertyType.Integer => prop.intValue != 0,
            SerializedPropertyType.Float => prop.floatValue != 0f,
            SerializedPropertyType.Enum => prop.enumValueIndex != 0,
            _ => true
        };
    }
}
#endif