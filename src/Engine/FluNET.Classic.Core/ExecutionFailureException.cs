namespace FluNET.Classic.Core;

/// <summary>Represents a stable, user-facing failure raised while executing a verb.</summary>
public sealed class ExecutionFailureException : Exception
{
    public ExecutionFailureException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code
    {
        get;
    }
}
