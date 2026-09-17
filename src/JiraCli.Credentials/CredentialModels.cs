using System.Security.Cryptography;
using System.Text;
using JiraCli.Core;

namespace JiraCli.Credentials;

public enum ApiTokenMode
{
    Unscoped,
    Scoped
}

public enum CredentialStoreStatus
{
    Success,
    ItemNotFound,
    StoreUnavailableOrLocked,
    AccessDenied,
    InteractiveAuthorizationRequired,
    UnsupportedPlatform,
    InvalidReference
}

public sealed record CredentialStoreReference
{
    public string Provider { get; init; } = string.Empty;
    public string? Target { get; init; }
    public string? Service { get; init; }
    public string? Account { get; init; }
    public Dictionary<string, string>? Attributes { get; init; }
}

public sealed record CredentialStoreResult(
    CredentialStoreStatus Status,
    SecretMaterial? Secret = null,
    string? SafeMessage = null);

public interface ICredentialStoreProvider
{
    string Name { get; }
    ValueTask<CredentialStoreResult> ReadAsync(
        CredentialStoreReference reference,
        CancellationToken cancellationToken);
}

public sealed class SecretMaterial : IDisposable
{
    private char[]? _characters;

    private SecretMaterial(char[] characters)
    {
        Validate(characters);
        _characters = characters;
    }

    public int Length => _characters?.Length ?? 0;

    public static SecretMaterial FromString(string value)
        => new(value.ToCharArray());

    public static SecretMaterial FromCharacters(char[] value, bool takeOwnership = true)
        => new(takeOwnership ? value : value.ToArray());

    public void CopyUtf8To(Span<byte> destination, out int bytesWritten)
    {
        ObjectDisposedException.ThrowIf(_characters is null, this);
        bytesWritten = Encoding.UTF8.GetBytes(_characters, destination);
    }

    public int GetUtf8ByteCount()
    {
        ObjectDisposedException.ThrowIf(_characters is null, this);
        return Encoding.UTF8.GetByteCount(_characters);
    }

    public void Dispose()
    {
        if (_characters is null)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(System.Runtime.InteropServices.MemoryMarshal.AsBytes(_characters.AsSpan()));
        _characters = null;
    }

    public override string ToString() => "[REDACTED]";

    private static void Validate(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            throw new JiraCliException("empty_token", "The supplied API token is empty.", CliExitCode.UsageOrConfiguration);
        }

        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                throw new JiraCliException(
                    "invalid_token_characters",
                    "The supplied API token contains a newline or control character.",
                    CliExitCode.UsageOrConfiguration);
            }
        }
    }
}

public sealed record ResolvedCredential(SecretMaterial Secret, string SourceName) : IDisposable
{
    public void Dispose() => Secret.Dispose();
}
