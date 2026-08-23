using UnityEditor;
using UnityEngine;


[CustomEditor(typeof(ScriptableObject), true)]
public class SOButtonEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        InspectorButtonDrawer.Draw(target);
    }
}