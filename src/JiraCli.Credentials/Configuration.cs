using System.Text.Json;
using System.Text.Json.Serialization;
using JiraCli.Core;

namespace JiraCli.Credentials;

public sealed record JiraCliConfiguration
{
    public int SchemaVersion { get; init; } = 1;
    public string? DefaultProfile { get; init; }
    public Dictionary<string, JiraProfile> Profiles { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record JiraProfile
{
    public string? Url { get; init; }
    public string? User { get; init; }
    public ApiTokenMode TokenMode { get; init; } = ApiTokenMode.Unscoped;
    public string? CloudId { get; init; }
    public string? Token { get; init; }
    public string? TokenFile { get; init; }
    public CredentialStoreReference? Credential { get; init; }
    public JiraDefaults Defaults { get; init; } = new();
}

public sealed record JiraDefaults
{
    public string? Project { get; init; }
    public int PageSize { get; init; } = 50;
}

public sealed record LoadedConfiguration(
    JiraCliConfiguration Configuration,
    string? Path,
    IReadOnlyList<string> Warnings);

public static class ConfigurationLoader
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string GetDefaultPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(root, "jiracli", "config.json");
    }

    public static async Task<LoadedConfiguration> LoadAsync(
        string? explicitPath,
        CancellationToken cancellationToken)
    {
        var path = explicitPath ?? GetDefaultPath();
        if (!File.Exists(path))
        {
            if (explicitPath is not null)
            {
                throw new JiraCliException(
                    "config_not_found",
                    $"Configuration file was not found: {path}",
                    CliExitCode.UsageOrConfiguration);
            }

            return new LoadedConfiguration(new JiraCliConfiguration(), null, []);
        }

        var warnings = new List<string>();
        CheckPermissions(path, warnings);
        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var configuration = await JsonSerializer.DeserializeAsync<JiraCliConfiguration>(stream, Options, cancellationToken)
                ?? new JiraCliConfiguration();
            if (configuration.SchemaVersion != 1)
            {
                throw new JiraCliException(
                    "unsupported_config_schema",
                    $"Configuration schema version {configuration.SchemaVersion} is not supported.",
                    CliExitCode.UsageOrConfiguration);
            }

            return new LoadedConfiguration(configuration, Path.GetFullPath(path), warnings);
        }
        catch (JsonException exception)
        {
            throw new JiraCliException(
                "invalid_config",
                $"Configuration is not valid JSON: {exception.Message}",
                CliExitCode.UsageOrConfiguration,
                innerException: exception);
        }
    }

    public static JiraProfile SelectProfile(LoadedConfiguration loaded, string? requestedName, out string? selectedName)
    {
        selectedName = requestedName ?? loaded.Configuration.DefaultProfile;
        if (selectedName is null)
        {
            return new JiraProfile();
        }

        if (!loaded.Configuration.Profiles.TryGetValue(selectedName, out var profile))
        {
            throw new JiraCliException(
                "profile_not_found",
                $"Profile '{selectedName}' was not found.",
                CliExitCode.UsageOrConfiguration,
                new { profile = selectedName });
        }

        return profile;
    }

    private static void CheckPermissions(string path, ICollection<string> warnings)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var mode = File.GetUnixFileMode(path);
            const UnixFileMode unsafeBits =
                UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
                UnixFileMode.OtherRead | UnixFileMode.OtherWrite;
            if ((mode & unsafeBits) != 0)
            {
                warnings.Add($"Configuration file '{path}' is accessible by group or other users. Consider chmod 600.");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            warnings.Add($"Could not inspect permissions for configuration file '{path}'.");
        }
    }
}
