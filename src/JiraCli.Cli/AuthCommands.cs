using System.CommandLine;
using System.Text.Json.Nodes;

namespace JiraCli.Cli;

internal static class AuthCommands
{
    public static Command Build(CliRuntime runtime, GlobalOptions globals)
    {
        var auth = new Command("auth", "Validate authentication and diagnose safe connection details.");
        var check = new Command("check", "Verify the selected identity and Jira connectivity.");
        check.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var response = await context.Client.GetMyselfAsync(token);
            var body = response.Body as JsonObject;
            return context.Output.Success(new
            {
                identity = new
                {
                    accountId = body?["accountId"]?.GetValue<string>(),
                    displayName = body?["displayName"]?.GetValue<string>(),
                    active = body?["active"]?.GetValue<bool?>()
                },
                site = context.SiteUrl,
                endpoint = context.Transport.BaseUri.GetLeftPart(UriPartial.Authority),
                credentialSource = context.CredentialSource
            }, "Authentication succeeded.");
        }));

        var doctor = new Command("doctor", "Check credential resolution, endpoint safety, identity, and server connectivity.");
        doctor.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var identity = await context.Client.GetMyselfAsync(token);
            var server = await context.Client.GetServerInfoAsync(token);
            var identityBody = identity.Body as JsonObject;
            var serverBody = server.Body as JsonObject;
            return context.Output.Success(new
            {
                profile = context.ProfileName,
                site = context.SiteUrl,
                endpoint = context.Transport.BaseUri.ToString(),
                credentialSource = context.CredentialSource,
                readOnly = context.Safety.ReadOnly,
                identity = new
                {
                    accountId = identityBody?["accountId"]?.GetValue<string>(),
                    displayName = identityBody?["displayName"]?.GetValue<string>(),
                    active = identityBody?["active"]?.GetValue<bool?>()
                },
                server = new
                {
                    baseUrl = serverBody?["baseUrl"]?.GetValue<string>(),
                    deploymentType = serverBody?["deploymentType"]?.GetValue<string>(),
                    version = serverBody?["version"]?.GetValue<string>()
                }
            }, "Jira connectivity checks succeeded.");
        }));

        auth.Subcommands.Add(check);
        auth.Subcommands.Add(doctor);
        return auth;
    }
}
