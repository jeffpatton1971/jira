namespace JiraCli.Core;

public enum WriteVerificationState
{
    NotApplicable,
    Confirmed,
    VerificationFailed,
    OutcomeUncertain
}

public sealed record OperationResult(
    string Operation,
    object? Data,
    bool DryRun = false,
    WriteVerificationState Verification = WriteVerificationState.NotApplicable,
    string? Message = null);
