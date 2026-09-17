using System.Text.Json.Nodes;
using JiraCli.Core;

namespace JiraCli.JiraCloud;

public static class JiraLookupResolver
{
    public static string ResolveAccountId(JsonNode? response, string query)
    {
        var users = response as JsonArray ?? [];
        var candidates = users.OfType<JsonObject>().ToArray();
        var exact = candidates.Where(candidate =>
            EqualsText(candidate["accountId"], query) ||
            EqualsText(candidate["displayName"], query) ||
            EqualsText(candidate["emailAddress"], query)).ToArray();
        var matches = exact.Length > 0 ? exact : candidates;
        if (matches.Length == 0)
        {
            throw new JiraCliException("user_not_found", $"No Jira user matched '{query}'.", CliExitCode.AuthorizationOrNotFound);
        }
        if (matches.Length > 1)
        {
            throw new JiraCliException(
                "ambiguous_user",
                $"User lookup '{query}' matched more than one account. Use an exact account ID.",
                CliExitCode.AmbiguousLookup,
                new { candidates = matches.Select(candidate => new { accountId = candidate["accountId"]?.GetValue<string>(), displayName = candidate["displayName"]?.GetValue<string>() }) });
        }
        return matches[0]["accountId"]?.GetValue<string>()
            ?? throw new JiraCliException("user_account_id_missing", "The matched Jira user has no account ID.", CliExitCode.NetworkOrProtocol);
    }

    public static JsonObject ResolveTransition(JsonNode? response, string requested)
    {
        var transitions = response?["transitions"] as JsonArray ?? [];
        var candidates = transitions.OfType<JsonObject>().Where(item =>
            string.Equals(item["id"]?.GetValue<string>(), requested, StringComparison.Ordinal) ||
            string.Equals(item["name"]?.GetValue<string>(), requested, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (candidates.Length == 0)
        {
            throw new JiraCliException("transition_not_found", $"No available transition matched '{requested}'.", CliExitCode.AuthorizationOrNotFound);
        }
        if (candidates.Length > 1)
        {
            throw new JiraCliException(
                "ambiguous_transition",
                $"Transition name '{requested}' is ambiguous; use an exact transition ID.",
                CliExitCode.AmbiguousLookup,
                new { candidates = candidates.Select(item => new { id = item["id"]?.GetValue<string>(), name = item["name"]?.GetValue<string>() }) });
        }
        return candidates[0];
    }

    private static bool EqualsText(JsonNode? value, string expected)
        => string.Equals(value?.GetValue<string>(), expected, StringComparison.OrdinalIgnoreCase);
}
