using System.CommandLine;

namespace JiraCli.Cli;

internal sealed class GlobalOptions
{
    public Option<string?> Url { get; } = Create<string?>("--url", "Jira Cloud site URL (https://name.atlassian.net).", recursive: true);
    public Option<string?> User { get; } = Create<string?>("--user", "Atlassian account email.", recursive: true);
    public Option<string?> Token { get; } = Create<string?>("--token", "API token value (visible to process inspection; prefer stdin or an OS store).", recursive: true);
    public Option<bool> TokenStdin { get; } = Create<bool>("--token-stdin", "Read one API-token line from standard input.", recursive: true);
    public Option<bool> TokenPrompt { get; } = Create<bool>("--token-prompt", "Read an API token from a hidden interactive prompt.", recursive: true);
    public Option<string?> Profile { get; } = Create<string?>("--profile", "Configuration profile name.", recursive: true);
    public Option<string?> Config { get; } = Create<string?>("--config", "Explicit user-owned configuration file.", recursive: true);
    public Option<string?> TokenMode { get; } = Create<string?>("--token-mode", "Token mode: unscoped or scoped.", recursive: true);
    public Option<string?> CloudId { get; } = Create<string?>("--cloud-id", "Atlassian Cloud ID for a scoped token.", recursive: true);
    public Option<bool> Json { get; } = Create<bool>("--json", "Emit stable machine-readable JSON.", recursive: true);
    public Option<bool> Quiet { get; } = Create<bool>("--quiet", "Suppress nonessential human output.", recursive: true);
    public Option<bool> NonInteractive { get; } = Create<bool>("--non-interactive", "Never prompt for input.", recursive: true);
    public Option<bool> ReadOnly { get; } = Create<bool>("--read-only", "Centrally block all mutation commands.", recursive: true);
    public Option<bool> DryRun { get; } = Create<bool>("--dry-run", "Preview a mutation locally without sending it.", recursive: true);
    public Option<int> TimeoutSeconds { get; } = Create<int>("--timeout", "Request timeout in seconds (1-300).", recursive: true, defaultValue: 30);

    public IEnumerable<Option> All =>
    [
        Url, User, Token, TokenStdin, TokenPrompt, Profile, Config, TokenMode, CloudId,
        Json, Quiet, NonInteractive, ReadOnly, DryRun, TimeoutSeconds
    ];

    private static Option<T> Create<T>(string name, string description, bool recursive, T? defaultValue = default)
    {
        var option = new Option<T>(name) { Description = description, Recursive = recursive };
        if (defaultValue is not null)
        {
            option.DefaultValueFactory = _ => defaultValue;
        }
        return option;
    }
}
