#if UNITY_EDITOR
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(MessageIfAttribute), true)]
public sealed class MessageIfDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUI.GetPropertyHeight(property, label, true);

        if (ShouldShow(property))
        {
            height += EditorGUIUtility.singleLineHeight * 2f;
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        MessageIfAttribute messageAttribute = (MessageIfAttribute)attribute;

        float propertyHeight = EditorGUI.GetPropertyHeight(property, label, true);

        Rect propertyRect = position;
        propertyRect.height = propertyHeight;

        EditorGUI.PropertyField(propertyRect, property, label, true);

        if (!ShouldShow(property))
        {
            return;
        }

        Rect helpBoxRect = position;
        helpBoxRect.y += propertyHeight + 2f;
        helpBoxRect.height = EditorGUIUtility.singleLineHeight * 2f;

        EditorGUI.HelpBox(helpBoxRect, messageAttribute.Message, (UnityEditor.MessageType)messageAttribute.MessageType);
    }

    private bool ShouldShow(SerializedProperty property)
    {
        MessageIfAttribute attr = (MessageIfAttribute)attribute;

        // null checks first
        if (attr.Comparison == ComparisonType.IsNull)
        {
            return IsNull(property);
        }

        float value;

        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                value = property.intValue;
                break;

            case SerializedPropertyType.Float:
                value = property.floatValue;
                break;
            case SerializedPropertyType.Boolean:
                value = property.boolValue ? 1 : 0;
                break;

            default:
                return false;
        }

        return CompareNumeric(value, attr);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsNull(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.ObjectReference:
                return property.objectReferenceValue == null;

            case SerializedPropertyType.ManagedReference:
                return property.managedReferenceValue == null;

            case SerializedPropertyType.String:
                return string.IsNullOrEmpty(property.stringValue);

            default:
                return false;
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CompareNumeric(float value, MessageIfAttribute attr)
    {
        switch (attr.Comparison)
        {
            case ComparisonType.True:
                return Mathf.Approximately(value, 1);

            case ComparisonType.False:
                return Mathf.Approximately(value, 0);


            case ComparisonType.LessThan:
                return value < attr.Value;

            case ComparisonType.LessThanOrEqual:
                return value <= attr.Value;

            case ComparisonType.Equal:
                return Mathf.Approximately(value, attr.Value);

            case ComparisonType.NotEqual:
                return !Mathf.Approximately(value, attr.Value);

            case ComparisonType.GreaterThanOrEqual:
                return value >= attr.Value;

            case ComparisonType.GreaterThan:
                return value > attr.Value;

            default:
                return false;
        }
    }
}
#endif