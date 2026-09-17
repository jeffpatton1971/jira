using System.CommandLine;
using JiraCli.Core;
using JiraCli.Credentials;

namespace JiraCli.Cli;

internal static class SiteCommands
{
    public static Command Build(CliRuntime runtime, GlobalOptions globals)
    {
        var site = new Command("site", "Inspect configured Jira Cloud sites.");
        var list = new Command("list", "List sites from local configuration without reading credentials.");
        list.SetAction(async (parse, cancellationToken) =>
        {
            var output = runtime.CreateOutput(parse);
            try
            {
                var path = parse.GetValue(globals.Config) ?? Environment.GetEnvironmentVariable("JIRACLI_CONFIG");
                var loaded = await ConfigurationLoader.LoadAsync(path, cancellationToken);
                return output.Success(new
                {
                    config = loaded.Path,
                    defaultProfile = loaded.Configuration.DefaultProfile,
                    sites = loaded.Configuration.Profiles.Select(pair => new
                    {
                        profile = pair.Key,
                        url = pair.Value.Url,
                        tokenMode = pair.Value.TokenMode.ToString().ToLowerInvariant(),
                        hasCloudId = pair.Value.CloudId is not null,
                        credentialProvider = pair.Value.Credential?.Provider
                    })
                });
            }
            catch (JiraCliException exception)
            {
                return output.Failure(exception);
            }
        });

        var check = new Command("check", "Validate the configured site and authenticated identity.");
        check.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var server = await context.Client.GetServerInfoAsync(token);
            var identity = await context.Client.GetMyselfAsync(token);
            return context.Output.Success(new
            {
                site = context.SiteUrl,
                server = server.Body,
                identity = new
                {
                    accountId = identity.Body?["accountId"]?.GetValue<string>(),
                    displayName = identity.Body?["displayName"]?.GetValue<string>()
                }
            });
        }));

        var discover = new Command("discover", "Explain or perform accessible-site discovery for the selected authentication method.");
        discover.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, (context, _) =>
            Task.FromResult(context.Output.Failure(new JiraCliException(
                "site_discovery_unavailable",
                "Atlassian API-token Basic authentication cannot enumerate every accessible Jira site. Use 'site list' for configured sites or 'site check' to validate one site. OAuth accessible-resource discovery is intentionally not implemented.",
                CliExitCode.UsageOrConfiguration,
                new { authentication = "api-token-basic", supported = false })))));

        site.Subcommands.Add(list);
        site.Subcommands.Add(check);
        site.Subcommands.Add(discover);
        return site;
    }
}
