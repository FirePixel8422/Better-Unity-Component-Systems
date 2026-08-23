using UnityEngine;


/// <summary>
/// Performance optimized DebugLogger Logger that only logs messages when conditional <see cref="ScriptingDefineSymbol"/> argument is provided in build settings. Also has logging based on condition support
/// </summary>
public static class DebugLogger
{
    public const string ScriptingDefineSymbol = "Enable_Debug_Systems";


    [System.Diagnostics.Conditional(ScriptingDefineSymbol)]
    public static void Log(object message, bool logCondition = true)
    {
        if (!logCondition) return;

        Debug.Log(message);
    }

    [System.Diagnostics.Conditional(ScriptingDefineSymbol)]
    public static void LogWarning(object message, bool logCondition = true)
    {
        if (!logCondition) return;

        Debug.LogWarning(message);
    }

    [System.Diagnostics.Conditional(ScriptingDefineSymbol)]
    public static void LogError(object message, bool logCondition = true)
    {
        if (!logCondition) return;

        Debug.LogError(message);
    }

    [System.Diagnostics.Conditional(ScriptingDefineSymbol)]
    public static void LogAssertion(object message, bool errorCondition = true)
    {
        if (!errorCondition) return;

        Debug.LogAssertion(message);
    }
}