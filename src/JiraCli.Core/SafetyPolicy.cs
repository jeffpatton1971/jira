namespace JiraCli.Core;

public sealed record SafetyContext(bool ReadOnly, bool DryRun, bool NonInteractive);

public sealed class SafetyPolicy
{
    public void EnsureMutationAllowed(SafetyContext context, string operation)
    {
        if (context.ReadOnly)
        {
            throw new JiraCliException(
                "read_only_violation",
                $"'{operation}' is blocked because --read-only is active.",
                CliExitCode.SafetyRefusal);
        }
    }

    public async Task ConfirmDeletionAsync(
        string target,
        string? suppliedConfirmation,
        bool recursive,
        string? recursiveConfirmation,
        SafetyContext context,
        TextReader input,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        EnsureMutationAllowed(context, $"delete {target}");

        if (recursive && !string.Equals(recursiveConfirmation, target, StringComparison.Ordinal))
        {
            throw new JiraCliException(
                "recursive_confirmation_required",
                $"Recursive deletion requires --confirm-recursive {target}.",
                CliExitCode.SafetyRefusal,
                new { target });
        }

        if (string.Equals(suppliedConfirmation, target, StringComparison.Ordinal))
        {
            return;
        }

        if (context.NonInteractive || Console.IsInputRedirected)
        {
            throw new JiraCliException(
                "confirmation_required",
                $"Deletion requires --confirm {target} in noninteractive mode.",
                CliExitCode.SafetyRefusal,
                new { target });
        }

        await output.WriteAsync($"Type the exact target '{target}' to confirm deletion: ");
        cancellationToken.ThrowIfCancellationRequested();
        var entered = await input.ReadLineAsync(cancellationToken);
        if (!string.Equals(entered, target, StringComparison.Ordinal))
        {
            throw new JiraCliException(
                "confirmation_mismatch",
                "Deletion cancelled because the confirmation did not match the target.",
                CliExitCode.SafetyRefusal,
                new { target });
        }
    }
}
