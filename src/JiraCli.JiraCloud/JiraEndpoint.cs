using JiraCli.Core;
using JiraCli.Credentials;

namespace JiraCli.JiraCloud;

public sealed record JiraConnectionOptions(
    string SiteUrl,
    string User,
    ApiTokenMode TokenMode,
    string? CloudId,
    TimeSpan Timeout,
    long MaximumResponseBytes = 16 * 1024 * 1024);

public static class JiraEndpoint
{
    public static Uri CreateBaseUri(JiraConnectionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.User) || options.User.Any(char.IsControl))
        {
            throw new JiraCliException("invalid_user", "An Atlassian account email is required.", CliExitCode.UsageOrConfiguration);
        }

        if (!Uri.TryCreate(options.SiteUrl, UriKind.Absolute, out var siteUri))
        {
            throw new JiraCliException("invalid_url", "The Jira URL must be an absolute HTTPS URL.", CliExitCode.UsageOrConfiguration);
        }

        ValidateSiteUri(siteUri);
        if (options.TokenMode == ApiTokenMode.Unscoped)
        {
            return new Uri(siteUri.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/", UriKind.Absolute);
        }

        if (!Guid.TryParse(options.CloudId, out var cloudId))
        {
            throw new JiraCliException(
                "invalid_cloud_id",
                "Scoped API tokens require a Cloud ID in UUID form.",
                CliExitCode.UsageOrConfiguration);
        }

        return new Uri($"https://api.atlassian.com/ex/jira/{cloudId:D}/", UriKind.Absolute);
    }

    public static void ValidateRequestDestination(Uri baseUri, Uri destination)
    {
        if (destination.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(baseUri.Scheme, destination.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(baseUri.IdnHost, destination.IdnHost, StringComparison.OrdinalIgnoreCase) ||
            baseUri.Port != destination.Port ||
            !string.IsNullOrEmpty(destination.UserInfo))
        {
            throw new JiraCliException(
                "unsafe_destination",
                "Refusing to send Jira credentials to a different or insecure origin.",
                CliExitCode.SafetyRefusal,
                new { destination = destination.GetLeftPart(UriPartial.Authority) });
        }

        if (baseUri.IdnHost.Equals("api.atlassian.com", StringComparison.OrdinalIgnoreCase) &&
            !destination.AbsolutePath.StartsWith(baseUri.AbsolutePath, StringComparison.Ordinal))
        {
            throw new JiraCliException(
                "unsafe_gateway_path",
                "Refusing to send scoped credentials outside the configured Jira Cloud-ID gateway path.",
                CliExitCode.SafetyRefusal);
        }
    }

    private static void ValidateSiteUri(Uri uri)
    {
        var validHost = uri.IdnHost.EndsWith(".atlassian.net", StringComparison.OrdinalIgnoreCase)
            && uri.IdnHost.Length > ".atlassian.net".Length;
        if (uri.Scheme != Uri.UriSchemeHttps || !validHost || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || !uri.IsDefaultPort)
        {
            throw new JiraCliException(
                "unsafe_jira_url",
                "Jira Cloud URLs must use HTTPS on the default port and a site host ending in .atlassian.net, without credentials, query, or fragment.",
                CliExitCode.SafetyRefusal);
        }
    }
}
