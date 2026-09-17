using JiraCli.Credentials;
using JiraCli.Credentials.Linux;
using JiraCli.Credentials.Mac;
using JiraCli.Credentials.Windows;

namespace JiraCli.Tests;

public sealed class PlatformCredentialContractTests
{
    [Fact]
    public void Mac_adapter_disables_interaction_by_default_and_allows_explicit_opt_in()
    {
        Assert.False(new MacKeychainProvider().AllowsInteraction);
        Assert.True(new MacKeychainProvider(allowInteraction: true).AllowsInteraction);
    }

    [Fact]
    public async Task Adapters_report_invalid_reference_or_unsupported_platform_without_enumeration()
    {
        var reference = new CredentialStoreReference();
        var providers = new ICredentialStoreProvider[]
        {
            new WindowsCredentialManagerProvider(),
            new MacKeychainProvider(),
            new LinuxSecretServiceProvider()
        };

        foreach (var provider in providers)
        {
            var result = await provider.ReadAsync(reference, TestContext.Current.CancellationToken);
            Assert.Contains(result.Status, new[]
            {
                CredentialStoreStatus.InvalidReference,
                CredentialStoreStatus.UnsupportedPlatform
            });
            Assert.Null(result.Secret);
        }
    }
}
