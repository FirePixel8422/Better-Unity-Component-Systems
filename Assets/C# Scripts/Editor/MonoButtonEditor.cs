using UnityEditor;
using UnityEngine;


[CustomEditor(typeof(MonoBehaviour), true)]
public class MonoButtonEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        InspectorButtonDrawer.Draw(target);
    }
}