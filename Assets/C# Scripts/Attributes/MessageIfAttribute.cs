using UnityEditor;
using UnityEngine;

public abstract class MessageIfAttribute : PropertyAttribute
{
    public readonly ComparisonType Comparison;
    public readonly float Value;
    public readonly string Message;
    public readonly MessageType MessageType;

    protected MessageIfAttribute(ComparisonType comparison, float value, string message, MessageType messageType)
    {
        Comparison = comparison;
        Value = value;
        Message = message;
        MessageType = messageType;
    }
}
public sealed class InfoIfAttribute : MessageIfAttribute
{
    public InfoIfAttribute(ComparisonType comparison, float value, string message)
        : base(comparison, value, message, MessageType.Info)
    {
    }
    public InfoIfAttribute(ComparisonType comparison, string message)
        : base(comparison, default, message, MessageType.Info)
    {
    }
}
public sealed class WarningIfAttribute : MessageIfAttribute
{
    public WarningIfAttribute(ComparisonType comparison, float value, string message)
        : base(comparison, value, message, MessageType.Warning)
    {
    }
    public WarningIfAttribute(ComparisonType comparison, string message)
        : base(comparison, default, message, MessageType.Warning)
    {
    }
}
public sealed class ErrorIfAttribute : MessageIfAttribute
{
    public ErrorIfAttribute(ComparisonType comparison, float value, string message)
        : base(comparison, value, message, MessageType.Error)
    {
    }
    public ErrorIfAttribute(ComparisonType comparison, string message)
        : base(comparison, default, message, MessageType.Error)
    {
    }
}

public enum ComparisonType
{
    IsNull,

    True,
    False,

    LessThan,
    LessThanOrEqual,
    Equal,
    NotEqual,
    GreaterThanOrEqual,
    GreaterThan,
}
public enum MessageType
{
    //
    // Summary:
    //     Neutral message.
    None,
    //
    // Summary:
    //     Info message.
    Info,
    //
    // Summary:
    //     Warning message.
    Warning,
    //
    // Summary:
    //     Error message.
    Error
}