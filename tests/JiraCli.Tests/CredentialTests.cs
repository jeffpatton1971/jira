using JiraCli.Core;
using JiraCli.Credentials;

namespace JiraCli.Tests;

public sealed class CredentialTests
{
    [Fact]
    public async Task Explicit_token_precedes_environment_and_store()
    {
        var provider = new FakeProvider(CredentialStoreStatus.Success, "store-token");
        var resolver = new CredentialResolver([provider]);
        using var result = await resolver.ResolveAsync(
            new CredentialOverrides("flag-token", false, false, "environment-token"),
            new JiraProfile { Credential = new CredentialStoreReference { Provider = provider.Name, Target = "x" } },
            TextReader.Null,
            CancellationToken.None);
        Assert.Equal("command-line token", result.SourceName);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task Environment_precedes_profile_plaintext_and_store()
    {
        var provider = new FakeProvider(CredentialStoreStatus.Success, "store-token");
        var resolver = new CredentialResolver([provider]);
        using var result = await resolver.ResolveAsync(
            new CredentialOverrides(null, false, false, "environment-token"),
            new JiraProfile { Token = "config-token", Credential = new CredentialStoreReference { Provider = provider.Name } },
            TextReader.Null,
            CancellationToken.None);
        Assert.Equal("JIRACLI_TOKEN environment variable", result.SourceName);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task Conflicting_explicit_sources_fail()
    {
        var resolver = new CredentialResolver([]);
        var exception = await Assert.ThrowsAsync<JiraCliException>(() => resolver.ResolveAsync(
            new CredentialOverrides("token", true, false, null),
            new JiraProfile(),
            new StringReader("second"),
            CancellationToken.None));
        Assert.Equal("conflicting_credential_sources", exception.ErrorCode);
    }

    [Theory]
    [InlineData(CredentialStoreStatus.ItemNotFound, "credential_store_item_not_found")]
    [InlineData(CredentialStoreStatus.StoreUnavailableOrLocked, "credential_store_unavailable_or_locked")]
    [InlineData(CredentialStoreStatus.AccessDenied, "credential_store_access_denied")]
    [InlineData(CredentialStoreStatus.InteractiveAuthorizationRequired, "credential_store_interactive_authorization_required")]
    [InlineData(CredentialStoreStatus.UnsupportedPlatform, "credential_store_unsupported_platform")]
    public async Task Store_failures_are_distinct_and_do_not_fallback(CredentialStoreStatus status, string code)
    {
        var provider = new FakeProvider(status, null);
        var resolver = new CredentialResolver([provider]);
        var exception = await Assert.ThrowsAsync<JiraCliException>(() => resolver.ResolveAsync(
            new CredentialOverrides(null, false, false, null),
            new JiraProfile { Token = null, Credential = new CredentialStoreReference { Provider = provider.Name } },
            TextReader.Null,
            CancellationToken.None));
        Assert.Equal(code, exception.ErrorCode);
        Assert.Equal(1, provider.Calls);
    }

    private sealed class FakeProvider(CredentialStoreStatus status, string? secret) : ICredentialStoreProvider
    {
        public string Name => "fake";
        public int Calls { get; private set; }

        public ValueTask<CredentialStoreResult> ReadAsync(CredentialStoreReference reference, CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult(new CredentialStoreResult(
                status,
                secret is null ? null : SecretMaterial.FromString(secret),
                "safe diagnostic"));
        }
    }
}
