using JiraCli.Core;

namespace JiraCli.Credentials;

public sealed record CredentialOverrides(
    string? Token,
    bool TokenFromStandardInput,
    bool TokenFromPrompt,
    string? EnvironmentToken);

public sealed class CredentialResolver(IEnumerable<ICredentialStoreProvider> providers)
{
    private readonly Dictionary<string, ICredentialStoreProvider> _providers = providers.ToDictionary(
        provider => provider.Name,
        StringComparer.OrdinalIgnoreCase);

    public async Task<ResolvedCredential> ResolveAsync(
        CredentialOverrides overrides,
        JiraProfile profile,
        TextReader input,
        CancellationToken cancellationToken)
    {
        var explicitCount = (overrides.Token is null ? 0 : 1)
            + (overrides.TokenFromStandardInput ? 1 : 0)
            + (overrides.TokenFromPrompt ? 1 : 0);
        if (explicitCount > 1)
        {
            throw new JiraCliException(
                "conflicting_credential_sources",
                "Use only one of --token, --token-stdin, or --token-prompt.",
                CliExitCode.UsageOrConfiguration);
        }

        if (overrides.Token is not null)
        {
            return new ResolvedCredential(SecretMaterial.FromString(overrides.Token), "command-line token");
        }

        if (overrides.TokenFromStandardInput)
        {
            var token = await input.ReadLineAsync(cancellationToken);
            if (token is null)
            {
                throw new JiraCliException("token_input_empty", "No token was available on standard input.", CliExitCode.UsageOrConfiguration);
            }

            return new ResolvedCredential(SecretMaterial.FromString(token), "standard input");
        }

        if (overrides.TokenFromPrompt)
        {
            if (Console.IsInputRedirected || Console.IsOutputRedirected)
            {
                throw new JiraCliException(
                    "interactive_prompt_unavailable",
                    "A hidden token prompt requires an interactive terminal.",
                    CliExitCode.UsageOrConfiguration);
            }

            return new ResolvedCredential(ReadHiddenToken(cancellationToken), "hidden prompt");
        }

        if (overrides.EnvironmentToken is not null)
        {
            return new ResolvedCredential(SecretMaterial.FromString(overrides.EnvironmentToken), "JIRACLI_TOKEN environment variable");
        }

        if (profile.Token is not null)
        {
            return new ResolvedCredential(SecretMaterial.FromString(profile.Token), "plaintext configuration token");
        }

        if (profile.TokenFile is not null)
        {
            return new ResolvedCredential(await ReadTokenFileAsync(profile.TokenFile, cancellationToken), "plaintext token file");
        }

        if (profile.Credential is null)
        {
            throw new JiraCliException(
                "credential_source_missing",
                "No API-token source was configured. Use stdin, a hidden prompt, an environment override, or an OS credential-store reference.",
                CliExitCode.Authentication);
        }

        if (!_providers.TryGetValue(profile.Credential.Provider, out var provider))
        {
            throw new JiraCliException(
                "credential_provider_unknown",
                $"Credential provider '{profile.Credential.Provider}' is not available.",
                CliExitCode.Authentication,
                new { provider = profile.Credential.Provider });
        }

        var result = await provider.ReadAsync(profile.Credential, cancellationToken);
        if (result.Status == CredentialStoreStatus.Success && result.Secret is not null)
        {
            return new ResolvedCredential(result.Secret, provider.Name);
        }

        throw new JiraCliException(
            "credential_store_" + ToCode(result.Status),
            result.SafeMessage ?? $"Credential provider '{provider.Name}' failed: {result.Status}.",
            CliExitCode.Authentication,
            new { provider = provider.Name, status = ToCode(result.Status) });
    }

    private static async Task<SecretMaterial> ReadTokenFileAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new JiraCliException("token_file_not_found", $"Token file was not found: {path}", CliExitCode.Authentication);
        }

        var text = await File.ReadAllTextAsync(path, cancellationToken);
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                var mode = File.GetUnixFileMode(path);
                const UnixFileMode unsafeBits = UnixFileMode.GroupRead | UnixFileMode.OtherRead;
                if ((mode & unsafeBits) != 0)
                {
                    throw new JiraCliException(
                        "unsafe_token_file_permissions",
                        $"Token file '{path}' is readable by group or other users. Restrict it to the owner before use.",
                        CliExitCode.Authentication);
                }
            }
            catch (PlatformNotSupportedException)
            {
                // Permission inspection is best effort on non-Unix filesystems.
            }
        }

        return SecretMaterial.FromString(text);
    }

    private static SecretMaterial ReadHiddenToken(CancellationToken cancellationToken)
    {
        Console.Error.Write("API token: ");
        var characters = new List<char>();
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.Error.WriteLine();
                    return SecretMaterial.FromCharacters(characters.ToArray());
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (characters.Count > 0)
                    {
                        characters.RemoveAt(characters.Count - 1);
                    }
                    continue;
                }

                if (!char.IsControl(key.KeyChar))
                {
                    characters.Add(key.KeyChar);
                }
            }
        }
        finally
        {
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(characters).Clear();
            characters.Clear();
        }
    }

    private static string ToCode(CredentialStoreStatus status) => status switch
    {
        CredentialStoreStatus.ItemNotFound => "item_not_found",
        CredentialStoreStatus.StoreUnavailableOrLocked => "unavailable_or_locked",
        CredentialStoreStatus.AccessDenied => "access_denied",
        CredentialStoreStatus.InteractiveAuthorizationRequired => "interactive_authorization_required",
        CredentialStoreStatus.UnsupportedPlatform => "unsupported_platform",
        CredentialStoreStatus.InvalidReference => "invalid_reference",
        _ => "failure"
    };
}
