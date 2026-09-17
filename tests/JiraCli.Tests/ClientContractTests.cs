using System.Net;
using System.Text.Json.Nodes;
using JiraCli.Credentials;
using JiraCli.JiraCloud;

namespace JiraCli.Tests;

public sealed class ClientContractTests
{
    [Fact]
    public async Task Required_operations_map_to_supported_cloud_endpoints()
    {
        var handler = new TestHttpHandler((_, _, _) => Task.FromResult(TestHttpHandler.Json(HttpStatusCode.OK)));
        using var token = SecretMaterial.FromString("synthetic-secret");
        using var transport = new JiraTransport(
            new JiraConnectionOptions("https://example.atlassian.net", "user@example.com", ApiTokenMode.Unscoped, null, TimeSpan.FromSeconds(2)),
            token,
            handler);
        var client = new JiraCloudClient(transport);
        var json = new JsonObject();
        var ct = TestContext.Current.CancellationToken;

        await client.GetMyselfAsync(ct);
        await client.SearchProjectsAsync(0, 50, null, ct);
        await client.GetCreateIssueTypesAsync("DEMO", ct);
        await client.GetCreateFieldsAsync("DEMO", "10001", ct);
        await client.GetEditFieldsAsync("DEMO-1", ct);
        await client.GetProjectComponentsAsync("DEMO", 0, 50, ct);
        await client.GetProjectVersionsAsync("DEMO", 0, 50, ct);
        await client.GetPrioritiesAsync(0, 50, ct);
        await client.FindUsersAsync("alex", false, null, ct);
        await client.GetIssueAsync("DEMO-1", ["summary"], ["names"], ct);
        await client.SearchIssuesAsync(new JsonObject { ["jql"] = "project=DEMO" }, ct);
        await client.CountIssuesAsync("project=DEMO", ct);
        await client.CreateIssueAsync(json, ct);
        await client.UpdateIssueAsync("DEMO-1", json, ct);
        await client.AssignIssueAsync("DEMO-1", "account", ct);
        await client.GetTransitionsAsync("DEMO-1", true, ct);
        await client.TransitionIssueAsync("DEMO-1", json, ct);
        await client.DeleteIssueAsync("DEMO-1", false, ct);
        await client.GetCommentsAsync("DEMO-1", 0, 50, ct);
        await client.GetCommentAsync("DEMO-1", "1", ct);
        await client.AddCommentAsync("DEMO-1", json, ct);
        await client.UpdateCommentAsync("DEMO-1", "1", json, ct);
        await client.DeleteCommentAsync("DEMO-1", "1", ct);
        await client.GetWorklogsAsync("DEMO-1", 0, 50, ct);
        await client.GetWorklogAsync("DEMO-1", "1", ct);
        await client.AddWorklogAsync("DEMO-1", json, ct);
        await client.UpdateWorklogAsync("DEMO-1", "1", json, ct);
        await client.DeleteWorklogAsync("DEMO-1", "1", ct);
        await client.GetRemoteLinksAsync("DEMO-1", ct);
        await client.GetRemoteLinkAsync("DEMO-1", "1", ct);
        await client.CreateRemoteLinkAsync("DEMO-1", json, ct);
        await client.UpdateRemoteLinkAsync("DEMO-1", "1", json, ct);
        await client.DeleteRemoteLinkAsync("DEMO-1", "1", ct);
        await client.GetBoardsAsync(0, 50, "DEMO", ct);
        await client.GetBoardBacklogAsync("1", 0, 50, ct);
        await client.GetSprintsAsync("1", 0, 50, "active,future", ct);
        await client.GetSprintIssuesAsync("2", 0, 50, ct);
        await client.MoveToSprintAsync("2", json, ct);
        await client.MoveToBacklogAsync("1", json, ct);

        Assert.Contains(handler.Requests, request => request.Method == HttpMethod.Post && request.Uri.AbsolutePath.EndsWith("/rest/api/3/search/jql", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, request => request.Method == HttpMethod.Delete && request.Uri.AbsolutePath.EndsWith("/rest/api/3/issue/DEMO-1", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, request => request.Uri.AbsolutePath.EndsWith("/rest/agile/1.0/sprint/2/issue", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, request => request.Uri.AbsolutePath.EndsWith("/rest/agile/1.0/backlog/1/issue", StringComparison.Ordinal));
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal("https", request.Uri.Scheme);
            Assert.Equal("example.atlassian.net", request.Uri.Host);
            Assert.Equal("Basic", request.AuthScheme);
            Assert.NotNull(request.AuthParameter);
        });
    }
}
