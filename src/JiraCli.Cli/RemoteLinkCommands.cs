using System.CommandLine;
using System.Text.Json.Nodes;
using JiraCli.Core;

namespace JiraCli.Cli;

internal static class RemoteLinkCommands
{
    public static Command Build(CliRuntime runtime)
    {
        var root = new Command("remote-link", "List, read, create, update, or delete remote issue links.");
        root.Subcommands.Add(BuildList(runtime));
        root.Subcommands.Add(BuildGet(runtime));
        root.Subcommands.Add(BuildWrite(runtime, update: false));
        root.Subcommands.Add(BuildWrite(runtime, update: true));
        root.Subcommands.Add(BuildDelete(runtime));
        return root;
    }

    private static Command BuildList(CliRuntime runtime)
    {
        var command = new Command("list", "List remote links for an issue.");
        var issue = CommandUtilities.Argument<string>("issue", "Issue key or ID.");
        command.Arguments.Add(issue);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var response = await context.Client.GetRemoteLinksAsync(parse.GetValue(issue)!, token);
            return context.Output.Success(response.Body);
        }));
        return command;
    }

    private static Command BuildGet(CliRuntime runtime)
    {
        var command = new Command("get", "Read one remote link.");
        var issue = CommandUtilities.Argument<string>("issue", "Issue key or ID.");
        var id = CommandUtilities.Argument<string>("link-id", "Remote link ID.");
        command.Arguments.Add(issue);
        command.Arguments.Add(id);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var response = await context.Client.GetRemoteLinkAsync(parse.GetValue(issue)!, parse.GetValue(id)!, token);
            return context.Output.Success(response.Body);
        }));
        return command;
    }

    private static Command BuildWrite(CliRuntime runtime, bool update)
    {
        var command = new Command(update ? "update" : "create", update ? "Update a remote link." : "Create a remote link.");
        var issue = CommandUtilities.Argument<string>("issue", "Issue key or ID.");
        var id = CommandUtilities.Argument<string>("link-id", "Remote link ID.");
        var json = CommandUtilities.Option<string?>("--body-json", "Complete remote-link JSON object or @file path.");
        var url = CommandUtilities.Option<string?>("--remote-url", "Remote object URL (HTTP or HTTPS).");
        var title = CommandUtilities.Option<string?>("--title", "Remote object title.");
        var relationship = CommandUtilities.Option<string?>("--relationship", "Relationship text.");
        var globalId = CommandUtilities.Option<string?>("--global-id", "Stable global ID used for idempotent replacement.");
        command.Arguments.Add(issue);
        if (update) command.Arguments.Add(id);
        command.Options.Add(json);
        command.Options.Add(url);
        command.Options.Add(title);
        command.Options.Add(relationship);
        command.Options.Add(globalId);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var key = parse.GetValue(issue)!;
            new SafetyPolicy().EnsureMutationAllowed(context.Safety, $"remote-link {(update ? "update" : "create")} {key}");
            if (parse.GetValue(json) is not null && (parse.GetValue(url) is not null || parse.GetValue(title) is not null || parse.GetValue(relationship) is not null || parse.GetValue(globalId) is not null))
            {
                throw new JiraCliException("conflicting_link_input", "Use --body-json alone or the remote-link convenience options.", CliExitCode.UsageOrConfiguration);
            }
            JsonObject request;
            if (parse.GetValue(json) is { } raw)
            {
                request = await CommandUtilities.ReadJsonObjectAsync(raw, token);
            }
            else
            {
                var urlValue = CommandUtilities.Require(parse.GetValue(url), "--remote-url");
                if (!Uri.TryCreate(urlValue, UriKind.Absolute, out var remoteUri) || remoteUri.Scheme is not ("http" or "https"))
                {
                    throw new JiraCliException("invalid_remote_url", "Remote-link URLs must be absolute HTTP or HTTPS URLs.", CliExitCode.UsageOrConfiguration);
                }
                request = new JsonObject
                {
                    ["object"] = new JsonObject
                    {
                        ["url"] = remoteUri.AbsoluteUri,
                        ["title"] = CommandUtilities.Require(parse.GetValue(title), "--title")
                    }
                };
                if (parse.GetValue(relationship) is { } relationshipValue) request["relationship"] = relationshipValue;
                if (parse.GetValue(globalId) is { } globalValue) request["globalId"] = globalValue;
            }
            var path = update
                ? $"/rest/api/3/issue/{Uri.EscapeDataString(key)}/remotelink/{Uri.EscapeDataString(parse.GetValue(id)!)}"
                : $"/rest/api/3/issue/{Uri.EscapeDataString(key)}/remotelink";
            if (context.Safety.DryRun)
            {
                return context.Output.Success(CommandUtilities.Preview(update ? "PUT" : "POST", path, request), "Dry run only; Jira did not validate or write this remote link.");
            }
            var response = update
                ? await context.Client.UpdateRemoteLinkAsync(key, parse.GetValue(id)!, request, token)
                : await context.Client.CreateRemoteLinkAsync(key, request, token);
            var resultId = update ? parse.GetValue(id) : response.Body?["id"]?.ToString();
            if (resultId is not null)
            {
                try
                {
                    var readBack = await context.Client.GetRemoteLinkAsync(key, resultId, token);
                    return context.Output.Success(new { result = readBack.Body, verification = "confirmed-by-read-back" }, update ? "Remote link updated." : "Remote link created.");
                }
                catch (JiraCliException exception)
                {
                    throw new JiraCliException(
                        "write_verification_failed",
                        "The remote-link write succeeded, but reading it back failed.",
                        CliExitCode.UncertainWrite,
                        new { issue = key, linkId = resultId, writeSucceeded = true, verification = "failed", cause = exception.ErrorCode },
                        exception);
                }
            }
            return context.Output.Success(new { result = response.Body, verification = "confirmed-by-write-response" }, "Remote link created; Jira did not return a link ID for read-back.");
        }));
        return command;
    }

    private static Command BuildDelete(CliRuntime runtime)
    {
        var command = new Command("delete", "Delete a remote link with exact-target confirmation.");
        var issue = CommandUtilities.Argument<string>("issue", "Issue key or ID.");
        var id = CommandUtilities.Argument<string>("link-id", "Remote link ID.");
        var confirm = CommandUtilities.Option<string?>("--confirm", "Exact confirmation in ISSUE/remote-link/ID form.");
        command.Arguments.Add(issue);
        command.Arguments.Add(id);
        command.Options.Add(confirm);
        command.SetAction((parse, cancellationToken) => runtime.ExecuteAsync(parse, cancellationToken, async (context, token) =>
        {
            var key = parse.GetValue(issue)!;
            var linkId = parse.GetValue(id)!;
            var target = $"{key}/remote-link/{linkId}";
            await new SafetyPolicy().ConfirmDeletionAsync(target, parse.GetValue(confirm), false, null, context.Safety, Console.In, Console.Error, token);
            if (context.Safety.DryRun)
            {
                return context.Output.Success(CommandUtilities.Preview("DELETE", $"/rest/api/3/issue/{Uri.EscapeDataString(key)}/remotelink/{Uri.EscapeDataString(linkId)}", null), "Dry run only; the remote link was not deleted.");
            }
            await context.Client.DeleteRemoteLinkAsync(key, linkId, token);
            return context.Output.Success(new { target, deleted = true }, "Remote link deleted.");
        }));
        return command;
    }
}
