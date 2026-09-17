using System.Text.Json.Nodes;

namespace JiraCli.JiraCloud;

public sealed class JiraCloudClient(JiraTransport transport)
{
    public Task<JiraResponse> GetMyselfAsync(CancellationToken cancellationToken) =>
        GetAsync("rest/api/3/myself", cancellationToken);

    public Task<JiraResponse> GetServerInfoAsync(CancellationToken cancellationToken) =>
        GetAsync("rest/api/3/serverInfo", cancellationToken);

    public Task<JiraResponse> GetIssueAsync(string key, IEnumerable<string>? fields, IEnumerable<string>? expand, CancellationToken cancellationToken)
    {
        var query = new List<KeyValuePair<string, string>>();
        if (fields is not null)
        {
            query.Add(new("fields", string.Join(',', fields)));
        }
        if (expand is not null)
        {
            query.Add(new("expand", string.Join(',', expand)));
        }
        return GetAsync($"rest/api/3/issue/{Escape(key)}{BuildQuery(query)}", cancellationToken);
    }

    public Task<JiraResponse> SearchIssuesAsync(JsonObject request, CancellationToken cancellationToken) =>
        PostAsync("rest/api/3/search/jql", request, safeRead: true, cancellationToken);

    public Task<JiraResponse> CountIssuesAsync(string jql, CancellationToken cancellationToken) =>
        PostAsync("rest/api/3/search/approximate-count", new JsonObject { ["jql"] = jql }, safeRead: true, cancellationToken);

    public Task<JiraResponse> SearchProjectsAsync(int startAt, int maxResults, string? query, CancellationToken cancellationToken) =>
        GetAsync($"rest/api/3/project/search{BuildQuery([
            new("startAt", startAt.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("maxResults", maxResults.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("query", query ?? string.Empty)])}", cancellationToken);

    public Task<JiraResponse> GetCreateIssueTypesAsync(string project, CancellationToken cancellationToken) =>
        GetAsync($"rest/api/3/issue/createmeta/{Escape(project)}/issuetypes", cancellationToken);

    public Task<JiraResponse> GetCreateFieldsAsync(string project, string issueType, CancellationToken cancellationToken) =>
        GetAsync($"rest/api/3/issue/createmeta/{Escape(project)}/issuetypes/{Escape(issueType)}", cancellationToken);

    public Task<JiraResponse> GetEditFieldsAsync(string issue, CancellationToken cancellationToken) =>
        GetAsync($"rest/api/3/issue/{Escape(issue)}/editmeta", cancellationToken);

    public Task<JiraResponse> GetProjectComponentsAsync(string project, int startAt, int maxResults, CancellationToken cancellationToken) =>
        GetAsync($"rest/api/3/project/{Escape(project)}/component?startAt={startAt}&maxResults={maxResults}", cancellationToken);

    public Task<JiraResponse> GetProjectVersionsAsync(string project, int startAt, int maxResults, CancellationToken cancellationToken) =>
        GetAsync($"rest/api/3/project/{Escape(project)}/version?startAt={startAt}&maxResults={maxResults}", cancellationToken);

    public Task<JiraResponse> GetPrioritiesAsync(int startAt, int maxResults, CancellationToken cancellationToken) =>
        GetAsync($"rest/api/3/priority/search?startAt={startAt}&maxResults={maxResults}", cancellationToken);

    public Task<JiraResponse> FindUsersAsync(string query, bool assignable, string? project, CancellationToken cancellationToken)
    {
        var path = assignable ? "rest/api/3/user/assignable/search" : "rest/api/3/user/search";
        var pairs = new List<KeyValuePair<string, string>> { new("query", query), new("maxResults", "100") };
        if (assignable && project is not null)
        {
            pairs.Add(new("project", project));
        }
        return GetAsync(path + BuildQuery(pairs), cancellationToken);
    }

    public Task<JiraResponse> CreateIssueAsync(JsonObject request, CancellationToken cancellationToken) =>
        PostAsync("rest/api/3/issue", request, safeRead: false, cancellationToken);

    public Task<JiraResponse> UpdateIssueAsync(string issue, JsonObject request, CancellationToken cancellationToken) =>
        PutAsync($"rest/api/3/issue/{Escape(issue)}?returnIssue=true", request, cancellationToken);

    public Task<JiraResponse> DeleteIssueAsync(string issue, bool deleteSubtasks, CancellationToken cancellationToken) =>
        DeleteAsync($"rest/api/3/issue/{Escape(issue)}?deleteSubtasks={deleteSubtasks.ToString().ToLowerInvariant()}", cancellationToken);

    public Task<JiraResponse> AssignIssueAsync(string issue, string? accountId, CancellationToken cancellationToken) =>
        PutAsync($"rest/api/3/issue/{Escape(issue)}/assignee", new JsonObject { ["accountId"] = accountId }, cancellationToken);

    public Task<JiraResponse> GetTransitionsAsync(string issue, bool includeFields, CancellationToken cancellationToken) =>
        GetAsync($"rest/api/3/issue/{Escape(issue)}/transitions{(includeFields ? "?expand=transitions.fields" : string.Empty)}", cancellationToken);

    public Task<JiraResponse> TransitionIssueAsync(string issue, JsonObject request, CancellationToken cancellationToken) =>
        PostAsync($"rest/api/3/issue/{Escape(issue)}/transitions", request, safeRead: false, cancellationToken);

    public Task<JiraResponse> GetCommentsAsync(string issue, int startAt, int maxResults, CancellationToken cancellationToken) =>
        GetAsync($"rest/api/3/issue/{Escape(issue)}/comment?startAt={startAt}&maxResults={maxResults}", cancellationToken);

    public Task<JiraResponse> GetCommentAsync(string issue, string id, CancellationToken cancellationToken) =>
        GetAsync($"rest/api/3/issue/{Escape(issue)}/comment/{Escape(id)}", cancellationToken);

    public Task<JiraResponse> AddCommentAsync(string issue, JsonObject request, CancellationToken cancellationToken) =>
        PostAsync($"rest/api/3/issue/{Escape(issue)}/comment", request, safeRead: false, cancellationToken);

    public Task<JiraResponse> UpdateCommentAsync(string issue, string id, JsonObject request, CancellationToken cancellationToken) =>
        PutAsync($"rest/api/3/issue/{Escape(issue)}/comment/{Escape(id)}", request, cancellationToken);

    public Task<JiraResponse> DeleteCommentAsync(string issue, string id, CancellationToken cancellationToken) =>
        DeleteAsync($"rest/api/3/issue/{Escape(issue)}/comment/{Escape(id)}", cancellationToken);

    public Task<JiraResponse> GetWorklogsAsync(string issue, int startAt, int maxResults, CancellationToken cancellationToken) =>
        GetAsync($"rest/api/3/issue/{Escape(issue)}/worklog?startAt={startAt}&maxResults={maxResults}", cancellationToken);

    public Task<JiraResponse> GetWorklogAsync(string issue, string id, CancellationToken cancellationToken) =>
        GetAsync($"rest/api/3/issue/{Escape(issue)}/worklog/{Escape(id)}", cancellationToken);

    public Task<JiraResponse> AddWorklogAsync(string issue, JsonObject request, CancellationToken cancellationToken) =>
        PostAsync($"rest/api/3/issue/{Escape(issue)}/worklog", request, safeRead: false, cancellationToken);

    public Task<JiraResponse> UpdateWorklogAsync(string issue, string id, JsonObject request, CancellationToken cancellationToken) =>
        PutAsync($"rest/api/3/issue/{Escape(issue)}/worklog/{Escape(id)}", request, cancellationToken);

    public Task<JiraResponse> DeleteWorklogAsync(string issue, string id, CancellationToken cancellationToken) =>
        DeleteAsync($"rest/api/3/issue/{Escape(issue)}/worklog/{Escape(id)}", cancellationToken);

    public Task<JiraResponse> GetRemoteLinksAsync(string issue, CancellationToken cancellationToken) =>
        GetAsync($"rest/api/3/issue/{Escape(issue)}/remotelink", cancellationToken);

    public Task<JiraResponse> GetRemoteLinkAsync(string issue, string id, CancellationToken cancellationToken) =>
        GetAsync($"rest/api/3/issue/{Escape(issue)}/remotelink/{Escape(id)}", cancellationToken);

    public Task<JiraResponse> CreateRemoteLinkAsync(string issue, JsonObject request, CancellationToken cancellationToken) =>
        PostAsync($"rest/api/3/issue/{Escape(issue)}/remotelink", request, safeRead: false, cancellationToken);

    public Task<JiraResponse> UpdateRemoteLinkAsync(string issue, string id, JsonObject request, CancellationToken cancellationToken) =>
        PutAsync($"rest/api/3/issue/{Escape(issue)}/remotelink/{Escape(id)}", request, cancellationToken);

    public Task<JiraResponse> DeleteRemoteLinkAsync(string issue, string id, CancellationToken cancellationToken) =>
        DeleteAsync($"rest/api/3/issue/{Escape(issue)}/remotelink/{Escape(id)}", cancellationToken);

    public Task<JiraResponse> GetBoardsAsync(int startAt, int maxResults, string? project, CancellationToken cancellationToken) =>
        GetAsync($"rest/agile/1.0/board{BuildQuery([
            new("startAt", startAt.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("maxResults", maxResults.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("projectKeyOrId", project ?? string.Empty)])}", cancellationToken);

    public Task<JiraResponse> GetBoardAsync(string id, CancellationToken cancellationToken) =>
        GetAsync($"rest/agile/1.0/board/{Escape(id)}", cancellationToken);

    public Task<JiraResponse> GetBoardBacklogAsync(string id, int startAt, int maxResults, CancellationToken cancellationToken) =>
        GetAsync($"rest/agile/1.0/board/{Escape(id)}/backlog?startAt={startAt}&maxResults={maxResults}", cancellationToken);

    public Task<JiraResponse> GetSprintsAsync(string boardId, int startAt, int maxResults, string? state, CancellationToken cancellationToken) =>
        GetAsync($"rest/agile/1.0/board/{Escape(boardId)}/sprint{BuildQuery([
            new("startAt", startAt.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("maxResults", maxResults.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("state", state ?? string.Empty)])}", cancellationToken);

    public Task<JiraResponse> GetSprintAsync(string id, CancellationToken cancellationToken) =>
        GetAsync($"rest/agile/1.0/sprint/{Escape(id)}", cancellationToken);

    public Task<JiraResponse> GetSprintIssuesAsync(string id, int startAt, int maxResults, CancellationToken cancellationToken) =>
        GetAsync($"rest/agile/1.0/sprint/{Escape(id)}/issue?startAt={startAt}&maxResults={maxResults}", cancellationToken);

    public Task<JiraResponse> MoveToSprintAsync(string id, JsonObject request, CancellationToken cancellationToken) =>
        PostAsync($"rest/agile/1.0/sprint/{Escape(id)}/issue", request, safeRead: false, cancellationToken);

    public Task<JiraResponse> MoveToBacklogAsync(string? boardId, JsonObject request, CancellationToken cancellationToken) =>
        PostAsync(boardId is null ? "rest/agile/1.0/backlog/issue" : $"rest/agile/1.0/backlog/{Escape(boardId)}/issue", request, safeRead: false, cancellationToken);

    public Task<JiraResponse> GetAsync(string path, CancellationToken cancellationToken) =>
        transport.SendAsync(HttpMethod.Get, path, null, safeToRetry: true, cancellationToken);

    public Task<JiraResponse> PostAsync(string path, JsonNode body, bool safeRead, CancellationToken cancellationToken) =>
        transport.SendAsync(HttpMethod.Post, path, body, safeRead, cancellationToken);

    public Task<JiraResponse> PutAsync(string path, JsonNode body, CancellationToken cancellationToken) =>
        transport.SendAsync(HttpMethod.Put, path, body, safeToRetry: false, cancellationToken);

    public Task<JiraResponse> DeleteAsync(string path, CancellationToken cancellationToken) =>
        transport.SendAsync(HttpMethod.Delete, path, null, safeToRetry: false, cancellationToken);

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static string BuildQuery(IEnumerable<KeyValuePair<string, string>> values)
    {
        var included = values.Where(pair => !string.IsNullOrEmpty(pair.Value)).ToArray();
        return included.Length == 0
            ? string.Empty
            : "?" + string.Join('&', included.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }
}
