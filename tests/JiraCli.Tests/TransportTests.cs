using System.Net;
using JiraCli.Core;
using JiraCli.Credentials;
using JiraCli.JiraCloud;

namespace JiraCli.Tests;

public sealed class TransportTests
{
    [Fact]
    public async Task Redirect_is_not_followed_or_forwarded()
    {
        var handler = new TestHttpHandler((_, _, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Found)
        {
            Headers = { Location = new Uri("https://evil.example/steal") }
        }));
        using var token = SecretMaterial.FromString("synthetic-secret");
        using var transport = CreateTransport(handler, token);
        var exception = await Assert.ThrowsAsync<JiraCliException>(() => transport.SendAsync(HttpMethod.Get, "rest/api/3/myself", null, true, CancellationToken.None));
        Assert.Equal("redirect_refused", exception.ErrorCode);
        Assert.Single(handler.Requests);
        Assert.Equal("example.atlassian.net", handler.Requests[0].Uri.Host);
    }

    [Fact]
    public async Task Safe_read_honors_rate_limit_and_retries()
    {
        var handler = new TestHttpHandler((_, call, _) =>
        {
            var response = TestHttpHandler.Json(call == 1 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK);
            if (call == 1) response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
            return Task.FromResult(response);
        });
        using var token = SecretMaterial.FromString("synthetic-secret");
        using var transport = CreateTransport(handler, token);
        await transport.SendAsync(HttpMethod.Get, "rest/api/3/myself", null, true, CancellationToken.None);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Ambiguous_write_network_failure_is_not_retried()
    {
        var handler = new TestHttpHandler((_, _, _) => throw new HttpRequestException("synthetic failure"));
        using var token = SecretMaterial.FromString("synthetic-secret");
        using var transport = CreateTransport(handler, token);
        var exception = await Assert.ThrowsAsync<JiraCliException>(() => transport.SendAsync(HttpMethod.Post, "rest/api/3/issue", new System.Text.Json.Nodes.JsonObject(), false, CancellationToken.None));
        Assert.Equal(CliExitCode.UncertainWrite, exception.ExitCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Cancellation_is_propagated()
    {
        var handler = new TestHttpHandler(async (_, _, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return TestHttpHandler.Json();
        });
        using var token = SecretMaterial.FromString("synthetic-secret");
        using var transport = CreateTransport(handler, token);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transport.SendAsync(HttpMethod.Get, "rest/api/3/myself", null, true, cancellation.Token));
    }

    [Fact]
    public async Task Authentication_failure_does_not_disclose_token()
    {
        var handler = new TestHttpHandler((_, _, _) => Task.FromResult(TestHttpHandler.Json(HttpStatusCode.Unauthorized, "{\"message\":\"denied\"}")));
        const string secret = "synthetic-secret";
        using var token = SecretMaterial.FromString(secret);
        using var transport = CreateTransport(handler, token);
        var exception = await Assert.ThrowsAsync<JiraCliException>(() => transport.SendAsync(HttpMethod.Get, "rest/api/3/myself", null, true, CancellationToken.None));
        Assert.DoesNotContain(secret, exception.ToString(), StringComparison.Ordinal);
        Assert.Equal(CliExitCode.Authentication, exception.ExitCode);
    }

    [Fact]
    public async Task Oversized_response_is_rejected()
    {
        var handler = new TestHttpHandler((_, _, _) => Task.FromResult(TestHttpHandler.Json(json: "{\"value\":\"0123456789\"}")));
        using var token = SecretMaterial.FromString("synthetic-secret");
        using var transport = new JiraTransport(
            new JiraConnectionOptions("https://example.atlassian.net", "user@example.com", ApiTokenMode.Unscoped, null, TimeSpan.FromSeconds(2), 8),
            token,
            handler);
        var exception = await Assert.ThrowsAsync<JiraCliException>(() => transport.SendAsync(HttpMethod.Get, "rest/api/3/myself", null, true, TestContext.Current.CancellationToken));
        Assert.Equal("response_too_large", exception.ErrorCode);
    }

    private static JiraTransport CreateTransport(HttpMessageHandler handler, SecretMaterial token) => new(
        new JiraConnectionOptions("https://example.atlassian.net", "user@example.com", ApiTokenMode.Unscoped, null, TimeSpan.FromSeconds(2), 1024 * 1024),
        token,
        handler);
}
