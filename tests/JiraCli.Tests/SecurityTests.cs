using JiraCli.Core;
using JiraCli.Credentials;
using JiraCli.JiraCloud;

namespace JiraCli.Tests;

public sealed class SecurityTests
{
    [Fact]
    public void Redactor_removes_authorization_and_known_secrets()
    {
        const string secret = "token-value-123";
        var input = $"Authorization: Basic abcdef token={secret} bearer token-value-123";
        var result = SecretRedactor.Redact(input, [secret]);
        Assert.DoesNotContain(secret, result, StringComparison.Ordinal);
        Assert.DoesNotContain("abcdef", result, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://example.atlassian.net")]
    [InlineData("https://evil.example")]
    [InlineData("https://user@example.atlassian.net")]
    [InlineData("https://example.atlassian.net:444")]
    public void Endpoint_rejects_unsafe_sites(string url)
    {
        var options = new JiraConnectionOptions(url, "user@example.com", ApiTokenMode.Unscoped, null, TimeSpan.FromSeconds(5));
        var exception = Assert.Throws<JiraCliException>(() => JiraEndpoint.CreateBaseUri(options));
        Assert.Equal(CliExitCode.SafetyRefusal, exception.ExitCode);
    }

    [Fact]
    public void Scoped_endpoint_requires_cloud_id_and_uses_gateway()
    {
        var id = Guid.NewGuid();
        var options = new JiraConnectionOptions("https://example.atlassian.net", "user@example.com", ApiTokenMode.Scoped, id.ToString(), TimeSpan.FromSeconds(5));
        var uri = JiraEndpoint.CreateBaseUri(options);
        Assert.Equal("api.atlassian.com", uri.Host);
        Assert.Contains(id.ToString(), uri.AbsolutePath, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("abc\ndef")]
    [InlineData("abc\0def")]
    [InlineData("")]
    public void Secret_rejects_malformed_values(string value)
    {
        Assert.Throws<JiraCliException>(() => SecretMaterial.FromString(value));
    }

    [Fact]
    public async Task Read_only_blocks_mutation_and_deletion_requires_exact_target()
    {
        var policy = new SafetyPolicy();
        var readOnly = new SafetyContext(true, false, true);
        var exception = Assert.Throws<JiraCliException>(() => policy.EnsureMutationAllowed(readOnly, "issue create"));
        Assert.Equal(CliExitCode.SafetyRefusal, exception.ExitCode);

        var normal = new SafetyContext(false, false, true);
        var confirmation = await Assert.ThrowsAsync<JiraCliException>(() => policy.ConfirmDeletionAsync(
            "DEMO-1",
            "DEMO-2",
            false,
            null,
            normal,
            TextReader.Null,
            TextWriter.Null,
            CancellationToken.None));
        Assert.Equal("confirmation_required", confirmation.ErrorCode);
    }
}
