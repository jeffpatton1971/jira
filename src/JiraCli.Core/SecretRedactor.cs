using System.Text.RegularExpressions;

namespace JiraCli.Core;

public static partial class SecretRedactor
{
    private const string Redacted = "[REDACTED]";

    public static string Redact(string? value, IEnumerable<string?>? knownSecrets = null)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        var result = AuthorizationPattern().Replace(value, "$1" + Redacted);
        result = TokenQueryPattern().Replace(result, "$1" + Redacted);

        if (knownSecrets is null)
        {
            return result;
        }

        foreach (var secret in knownSecrets.Where(secret => !string.IsNullOrEmpty(secret)))
        {
            result = result.Replace(secret!, Redacted, StringComparison.Ordinal);
        }

        return result;
    }

    [GeneratedRegex("(?i)(authorization\\s*[:=]\\s*(?:basic|bearer)\\s+)[^\\s,;]+", RegexOptions.CultureInvariant)]
    private static partial Regex AuthorizationPattern();

    [GeneratedRegex("(?i)([?&](?:token|api_token|access_token)=)[^&\\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenQueryPattern();
}
