#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(InlineSOAttribute))]
public sealed class InlineSODrawer : PropertyDrawer
{
    private const float FoldoutWidth = 16f;
    private const float AlignFix = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;

        if (property.objectReferenceValue == null || !property.isExpanded)
            return height;

        SerializedObject so = new SerializedObject(property.objectReferenceValue);
        SerializedProperty iterator = so.GetIterator();

        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            if (iterator.name == "m_Script")
            {
                enterChildren = false;
                continue;
            }

            height += EditorGUI.GetPropertyHeight(iterator, true)
                   + EditorGUIUtility.standardVerticalSpacing;

            enterChildren = false;
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect lineRect = new Rect(
            position.x,
            position.y,
            position.width,
            EditorGUIUtility.singleLineHeight
        );

        bool hasObject = property.objectReferenceValue != null;

        float indentBackup = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        Rect foldoutRect = new Rect(
            lineRect.x - 1,
            lineRect.y,
            FoldoutWidth,
            lineRect.height
        );

        Rect labelRect = new Rect(
            lineRect.x + FoldoutWidth - 1,
            lineRect.y,
            EditorGUIUtility.labelWidth - FoldoutWidth - 1,
            lineRect.height
        );

        Rect objectRect = new Rect(
            position.x + EditorGUIUtility.labelWidth + AlignFix,
            lineRect.y,
            position.width - EditorGUIUtility.labelWidth - AlignFix,
            lineRect.height
        );

        Rect hoverRect = new Rect(
            lineRect.x - 14,
            lineRect.y,
            lineRect.width + 30f,
            lineRect.height
        );

        bool isHovering =
            hasObject &&
            hoverRect.Contains(Event.current.mousePosition);

        if (isHovering)
        {
            Color c = EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.05f)
                : new Color(0f, 0f, 0f, 0.04f);

            EditorGUI.indentLevel--;
            EditorGUI.DrawRect(hoverRect, c);
            EditorGUI.indentLevel++;
        }

        // Foldout ONLY here
        if (hasObject)
        {
            property.isExpanded = EditorGUI.Foldout(
                foldoutRect,
                property.isExpanded,
                GUIContent.none,
                true
            );
        }
        else
        {
            property.isExpanded = false;
        }

        // Label toggles expand
        if (hasObject && Event.current.type == EventType.MouseDown &&
            labelRect.Contains(Event.current.mousePosition))
        {
            property.isExpanded = !property.isExpanded;
            Event.current.Use();
        }

        EditorGUI.indentLevel--;
        EditorGUI.LabelField(labelRect, label);
        EditorGUI.indentLevel++;

        // Object field behaves normally ONLY (no toggle logic)
        property.objectReferenceValue = EditorGUI.ObjectField(
            objectRect,
            property.objectReferenceValue,
            fieldInfo.FieldType,
            false
        );

        EditorGUI.indentLevel = (int)indentBackup;

        if (property.objectReferenceValue == null)
        {
            property.isExpanded = false;
            EditorGUI.EndProperty();
            return;
        }

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;

            SerializedObject so = new SerializedObject(property.objectReferenceValue);
            SerializedProperty iterator = so.GetIterator();

            float y = lineRect.yMax + EditorGUIUtility.standardVerticalSpacing;

            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                if (iterator.name == "m_Script")
                {
                    enterChildren = false;
                    continue;
                }

                float propertyHeight = EditorGUI.GetPropertyHeight(iterator, true);

                Rect propertyRect = new Rect(
                    position.x,
                    y,
                    position.width,
                    propertyHeight
                );

                EditorGUI.PropertyField(propertyRect, iterator, true);

                y += propertyHeight + EditorGUIUtility.standardVerticalSpacing;

                enterChildren = false;
            }

            so.ApplyModifiedProperties();

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }
}
#endif