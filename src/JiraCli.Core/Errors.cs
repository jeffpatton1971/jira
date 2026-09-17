namespace JiraCli.Core;

public enum CliExitCode
{
    Success = 0,
    UsageOrConfiguration = 2,
    Authentication = 3,
    AuthorizationOrNotFound = 4,
    NetworkOrProtocol = 5,
    AmbiguousLookup = 6,
    SafetyRefusal = 7,
    UncertainWrite = 8,
    Cancelled = 130
}

public sealed class JiraCliException : Exception
{
    public JiraCliException(
        string errorCode,
        string message,
        CliExitCode exitCode,
        object? details = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        ExitCode = exitCode;
        Details = details;
    }

    public string ErrorCode { get; }
    public CliExitCode ExitCode { get; }
    public object? Details { get; }
}
