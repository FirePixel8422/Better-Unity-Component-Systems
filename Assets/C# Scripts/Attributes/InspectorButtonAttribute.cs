using System;


[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class InspectorButtonAttribute : Attribute
{
    public string Label;

    // Allow execution in edit mode (default true)
    public bool AllowUsageOutsidePlayMode = true;

    public InspectorButtonAttribute(string label = null, bool allowUsageOutsidePlayMode = true)
    {
        Label = label;
        AllowUsageOutsidePlayMode = allowUsageOutsidePlayMode;
    }
}