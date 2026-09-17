using System.CommandLine;
using JiraCli.Core;
using JiraCli.Credentials;
using JiraCli.Credentials.Linux;
using JiraCli.Credentials.Mac;
using JiraCli.Credentials.Windows;
using JiraCli.JiraCloud;

namespace JiraCli.Cli;

internal sealed class CliContext(
    JiraProfile profile,
    string? profileName,
    string siteUrl,
    string user,
    string credentialSource,
    SafetyContext safety,
    int defaultPageSize,
    OutputWriter output,
    ResolvedCredential credential,
    JiraTransport transport,
    JiraCloudClient client) : IDisposable
{
    public JiraProfile Profile { get; } = profile;
    public string? ProfileName { get; } = profileName;
    public string SiteUrl { get; } = siteUrl;
    public string User { get; } = user;
    public string CredentialSource { get; } = credentialSource;
    public SafetyContext Safety { get; } = safety;
    public int DefaultPageSize { get; } = defaultPageSize;
    public OutputWriter Output { get; } = output;
    public JiraTransport Transport { get; } = transport;
    public JiraCloudClient Client { get; } = client;
    private ResolvedCredential Credential { get; } = credential;

    public void Dispose()
    {
        Transport.Dispose();
        Credential.Dispose();
    }
}

internal sealed class CliRuntime(GlobalOptions options)
{
    public OutputWriter CreateOutput(ParseResult parseResult) => new(
        parseResult.GetValue(options.Json),
        parseResult.GetValue(options.Quiet),
        Console.Out,
        Console.Error);

    public async Task<CliContext> CreateContextAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var output = CreateOutput(parseResult);
        var configPath = First(parseResult.GetValue(options.Config), Environment.GetEnvironmentVariable("JIRACLI_CONFIG"));
        var loaded = await ConfigurationLoader.LoadAsync(configPath, cancellationToken);
        foreach (var warning in loaded.Warnings)
        {
            output.Warning(warning);
        }

        var requestedProfile = First(parseResult.GetValue(options.Profile), Environment.GetEnvironmentVariable("JIRACLI_PROFILE"));
        var profile = ConfigurationLoader.SelectProfile(loaded, requestedProfile, out var profileName);
        var url = First(parseResult.GetValue(options.Url), Environment.GetEnvironmentVariable("JIRACLI_URL"), profile.Url);
        var user = First(parseResult.GetValue(options.User), Environment.GetEnvironmentVariable("JIRACLI_USER"), profile.User);
        if (url is null || user is null)
        {
            throw new JiraCliException(
                "connection_settings_missing",
                "Both Jira URL and Atlassian account email are required through flags, environment, or the selected profile.",
                CliExitCode.UsageOrConfiguration);
        }

        var modeText = First(parseResult.GetValue(options.TokenMode), Environment.GetEnvironmentVariable("JIRACLI_TOKEN_MODE"));
        var tokenMode = modeText is null ? profile.TokenMode : ParseTokenMode(modeText);
        var cloudId = First(parseResult.GetValue(options.CloudId), Environment.GetEnvironmentVariable("JIRACLI_CLOUD_ID"), profile.CloudId);
        var timeoutSeconds = parseResult.GetValue(options.TimeoutSeconds);
        if (timeoutSeconds is < 1 or > 300)
        {
            throw new JiraCliException("invalid_timeout", "--timeout must be between 1 and 300 seconds.", CliExitCode.UsageOrConfiguration);
        }

        if (profile.Token is not null || profile.TokenFile is not null)
        {
            output.Warning("A plaintext credential source is configured. An OS credential store provides stronger protection.");
        }
        if (parseResult.GetValue(options.Token) is not null)
        {
            output.Warning("Command-line tokens may be visible to other local processes and shell history.");
        }
        if (Environment.GetEnvironmentVariable("JIRACLI_TOKEN") is not null)
        {
            output.Warning("Environment-variable tokens are not equivalent to an OS credential store.");
        }

        var nonInteractive = parseResult.GetValue(options.NonInteractive);
        var resolver = new CredentialResolver([
            new WindowsCredentialManagerProvider(),
            new MacKeychainProvider(allowInteraction: !nonInteractive),
            new LinuxSecretServiceProvider()
        ]);
        var credential = await resolver.ResolveAsync(
            new CredentialOverrides(
                parseResult.GetValue(options.Token),
                parseResult.GetValue(options.TokenStdin),
                parseResult.GetValue(options.TokenPrompt),
                Environment.GetEnvironmentVariable("JIRACLI_TOKEN")),
            profile,
            Console.In,
            cancellationToken);
        try
        {
            var connection = new JiraConnectionOptions(url, user, tokenMode, cloudId, TimeSpan.FromSeconds(timeoutSeconds));
            var transport = new JiraTransport(connection, credential.Secret);
            var safety = new SafetyContext(
                parseResult.GetValue(options.ReadOnly) || IsTrue(Environment.GetEnvironmentVariable("JIRACLI_READ_ONLY")),
                parseResult.GetValue(options.DryRun),
                nonInteractive);
            return new CliContext(
                profile,
                profileName,
                url,
                user,
                credential.SourceName,
                safety,
                Math.Clamp(profile.Defaults.PageSize, 1, 100),
                output,
                credential,
                transport,
                new JiraCloudClient(transport));
        }
        catch
        {
            credential.Dispose();
            throw;
        }
    }

    public async Task<int> ExecuteAsync(
        ParseResult parseResult,
        CancellationToken cancellationToken,
        Func<CliContext, CancellationToken, Task<int>> action)
    {
        var output = CreateOutput(parseResult);
        try
        {
            using var context = await CreateContextAsync(parseResult, cancellationToken);
            return await action(context, cancellationToken);
        }
        catch (JiraCliException exception)
        {
            return output.Failure(exception);
        }
        catch (OperationCanceledException)
        {
            return output.Failure("cancelled", "The operation was cancelled.", CliExitCode.Cancelled);
        }
        catch (Exception exception)
        {
            return output.Failure(new JiraCliException(
                "unexpected_error",
                SecretRedactor.Redact(exception.Message),
                CliExitCode.NetworkOrProtocol,
                innerException: exception));
        }
    }

    private static ApiTokenMode ParseTokenMode(string value) => value.ToLowerInvariant() switch
    {
        "unscoped" => ApiTokenMode.Unscoped,
        "scoped" => ApiTokenMode.Scoped,
        _ => throw new JiraCliException("invalid_token_mode", "Token mode must be 'unscoped' or 'scoped'.", CliExitCode.UsageOrConfiguration)
    };

    private static string? First(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    private static bool IsTrue(string? value) => bool.TryParse(value, out var parsed) && parsed;
}
