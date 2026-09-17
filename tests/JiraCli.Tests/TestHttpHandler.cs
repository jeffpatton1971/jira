using System.Net;

namespace JiraCli.Tests;

internal sealed class TestHttpHandler(Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
{
    private int _calls;
    public List<CapturedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new CapturedRequest(
            request.Method,
            request.RequestUri!,
            request.Headers.Authorization?.Scheme,
            request.Headers.Authorization?.Parameter,
            body));
        var call = Interlocked.Increment(ref _calls);
        return await responder(request, call, cancellationToken);
    }

    public static HttpResponseMessage Json(HttpStatusCode status = HttpStatusCode.OK, string json = "{}") => new(status)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };
}

internal sealed record CapturedRequest(HttpMethod Method, Uri Uri, string? AuthScheme, string? AuthParameter, string? Body);
