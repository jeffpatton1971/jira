using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using JiraCli.Core;
using JiraCli.Credentials;

namespace JiraCli.JiraCloud;

public sealed record JiraResponse(HttpStatusCode StatusCode, JsonNode? Body, string? RequestId);

public sealed class JiraTransport : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;
    private readonly JiraConnectionOptions _options;
    private readonly Uri _baseUri;
    private readonly SecretMaterial _token;

    public JiraTransport(JiraConnectionOptions options, SecretMaterial token, HttpMessageHandler? handler = null)
    {
        _options = options;
        _baseUri = JiraEndpoint.CreateBaseUri(options);
        _token = token;
        if (handler is null)
        {
            handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                ConnectTimeout = TimeSpan.FromSeconds(Math.Min(15, Math.Max(1, options.Timeout.TotalSeconds)))
            };
            _ownsClient = true;
        }

        _httpClient = new HttpClient(handler, disposeHandler: _ownsClient)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("JiraCli/0.1");
    }

    public Uri BaseUri => _baseUri;

    public async Task<JiraResponse> SendAsync(
        HttpMethod method,
        string relativePath,
        JsonNode? body,
        bool safeToRetry,
        CancellationToken cancellationToken)
    {
        if (relativePath.StartsWith("//", StringComparison.Ordinal) || Uri.TryCreate(relativePath, UriKind.Absolute, out _))
        {
            throw new JiraCliException("invalid_api_path", "API paths must be relative to the validated Jira endpoint.", CliExitCode.SafetyRefusal);
        }

        var destination = new Uri(_baseUri, relativePath.TrimStart('/'));
        JiraEndpoint.ValidateRequestDestination(_baseUri, destination);
        var attempts = safeToRetry ? 3 : 1;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.Timeout);
            using var request = CreateRequest(method, destination, body);
            try
            {
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if (IsRedirect(response.StatusCode))
                {
                    throw new JiraCliException(
                        "redirect_refused",
                        "Jira returned a redirect. Credentials were not forwarded.",
                        CliExitCode.NetworkOrProtocol,
                        new { status = (int)response.StatusCode });
                }

                if (safeToRetry && attempt < attempts && IsRetryable(response.StatusCode))
                {
                    await DelayForRetryAsync(response, attempt, cancellationToken);
                    continue;
                }

                var responseBody = await ReadBoundedJsonAsync(response.Content, timeout.Token);
                if (!response.IsSuccessStatusCode)
                {
                    throw CreateApiException(response.StatusCode, responseBody, response.Headers);
                }

                var requestId = response.Headers.TryGetValues("X-Arequestid", out var requestIds)
                    ? requestIds.FirstOrDefault()
                    : null;
                return new JiraResponse(response.StatusCode, responseBody, requestId);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                if (!safeToRetry)
                {
                    throw Uncertain("The Jira write timed out; its outcome is uncertain.", destination, exception);
                }

                if (attempt == attempts)
                {
                    throw new JiraCliException("request_timeout", "The Jira request timed out.", CliExitCode.NetworkOrProtocol, innerException: exception);
                }
            }
            catch (HttpRequestException exception)
            {
                if (!safeToRetry)
                {
                    throw Uncertain("The Jira write failed during transport; its outcome is uncertain.", destination, exception);
                }

                if (attempt == attempts)
                {
                    throw new JiraCliException(
                        "network_failure",
                        SecretRedactor.Redact(exception.Message),
                        CliExitCode.NetworkOrProtocol,
                        innerException: exception);
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
        }

        throw new JiraCliException("request_failed", "The Jira request failed.", CliExitCode.NetworkOrProtocol);
    }

    public void Dispose() => _httpClient.Dispose();

    private HttpRequestMessage CreateRequest(HttpMethod method, Uri destination, JsonNode? body)
    {
        var request = new HttpRequestMessage(method, destination);
        request.Headers.Authorization = CreateAuthorizationHeader();
        if (body is not null)
        {
            request.Content = new StringContent(body.ToJsonString(JsonOptions), Encoding.UTF8, "application/json");
        }
        return request;
    }

    private AuthenticationHeaderValue CreateAuthorizationHeader()
    {
        var userBytes = Encoding.UTF8.GetBytes(_options.User);
        var tokenByteCount = _token.GetUtf8ByteCount();
        var credentials = ArrayPool<byte>.Shared.Rent(userBytes.Length + 1 + tokenByteCount);
        try
        {
            userBytes.CopyTo(credentials, 0);
            credentials[userBytes.Length] = (byte)':';
            _token.CopyUtf8To(credentials.AsSpan(userBytes.Length + 1, tokenByteCount), out var written);
            var encoded = Convert.ToBase64String(credentials, 0, userBytes.Length + 1 + written);
            return new AuthenticationHeaderValue("Basic", encoded);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(credentials);
            ArrayPool<byte>.Shared.Return(credentials);
            CryptographicOperations.ZeroMemory(userBytes);
        }
    }

    private async Task<JsonNode?> ReadBoundedJsonAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > _options.MaximumResponseBytes)
        {
            throw new JiraCliException("response_too_large", "Jira returned a response larger than the configured safety limit.", CliExitCode.NetworkOrProtocol);
        }

        await using var input = await content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            long total = 0;
            while (true)
            {
                var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read == 0)
                {
                    break;
                }
                total += read;
                if (total > _options.MaximumResponseBytes)
                {
                    throw new JiraCliException("response_too_large", "Jira returned a response larger than the configured safety limit.", CliExitCode.NetworkOrProtocol);
                }
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            if (output.Length == 0)
            {
                return null;
            }

            output.Position = 0;
            try
            {
                return await JsonNode.ParseAsync(output, cancellationToken: cancellationToken);
            }
            catch (JsonException exception)
            {
                throw new JiraCliException("invalid_json_response", "Jira returned an invalid JSON response.", CliExitCode.NetworkOrProtocol, innerException: exception);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static bool IsRedirect(HttpStatusCode statusCode) => (int)statusCode is >= 300 and < 400;
    private static bool IsRetryable(HttpStatusCode statusCode) => statusCode == HttpStatusCode.TooManyRequests || statusCode is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private static async Task DelayForRetryAsync(HttpResponseMessage response, int attempt, CancellationToken cancellationToken)
    {
        var delay = response.Headers.RetryAfter?.Delta;
        if (delay is null && response.Headers.RetryAfter?.Date is { } date)
        {
            delay = date - DateTimeOffset.UtcNow;
        }
        delay ??= TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt - 1));
        delay = TimeSpan.FromMilliseconds(Math.Clamp(delay.Value.TotalMilliseconds, 0, 30_000));
        await Task.Delay(delay.Value, cancellationToken);
    }

    private static JiraCliException CreateApiException(HttpStatusCode statusCode, JsonNode? body, HttpResponseHeaders headers)
    {
        var safeBody = statusCode == HttpStatusCode.Unauthorized ? string.Empty : body?.ToJsonString() ?? string.Empty;
        if (safeBody.Length > 4_096)
        {
            safeBody = safeBody[..4_096] + "…";
        }
        safeBody = SecretRedactor.Redact(safeBody);
        var exitCode = statusCode == HttpStatusCode.Unauthorized
            ? CliExitCode.Authentication
            : statusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound
                ? CliExitCode.AuthorizationOrNotFound
                : CliExitCode.NetworkOrProtocol;
        var errorCode = statusCode switch
        {
            HttpStatusCode.Unauthorized => "jira_authentication_failed",
            HttpStatusCode.Forbidden => "jira_access_denied",
            HttpStatusCode.NotFound => "jira_not_found",
            HttpStatusCode.TooManyRequests => "jira_rate_limited",
            _ => "jira_api_error"
        };
        var requestId = headers.TryGetValues("X-Arequestid", out var requestIds) ? requestIds.FirstOrDefault() : null;
        return new JiraCliException(
            errorCode,
            $"Jira returned HTTP {(int)statusCode} ({statusCode}).",
            exitCode,
            new { status = (int)statusCode, requestId, response = safeBody });
    }

    private static JiraCliException Uncertain(string message, Uri destination, Exception exception) => new(
        "write_outcome_uncertain",
        message,
        CliExitCode.UncertainWrite,
        new { endpoint = destination.AbsolutePath, instruction = "Inspect the target before retrying." },
        exception);
}
