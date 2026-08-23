using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class RenameAttribute : PropertyAttribute
{
    public readonly string label;

    public RenameAttribute(string label)
    {
        this.label = label;
    }
}