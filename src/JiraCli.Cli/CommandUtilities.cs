using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Nodes;
using JiraCli.Core;

namespace JiraCli.Cli;

internal static class CommandUtilities
{
    public static Option<T> Option<T>(string name, string description, bool required = false, T? defaultValue = default)
    {
        var option = new Option<T>(name) { Description = description, Required = required };
        if (defaultValue is not null)
        {
            option.DefaultValueFactory = _ => defaultValue;
        }
        return option;
    }

    public static Argument<T> Argument<T>(string name, string description)
        => new(name) { Description = description };

    public static string Require(string? value, string optionName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JiraCliException("missing_value", $"{optionName} is required.", CliExitCode.UsageOrConfiguration);
        }
        return value;
    }

    public static string[] SplitCsv(string[]? values) => values?
        .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Where(value => value.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray() ?? [];

    public static async Task<JsonObject> ReadJsonObjectAsync(string value, CancellationToken cancellationToken)
    {
        string json;
        if (value.StartsWith('@'))
        {
            var path = value[1..];
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                throw new JiraCliException("input_file_not_found", $"JSON input file was not found: {path}", CliExitCode.UsageOrConfiguration);
            }
            if (info.Length > 16 * 1024 * 1024)
            {
                throw new JiraCliException("input_too_large", "JSON input exceeds the 16 MiB safety limit.", CliExitCode.UsageOrConfiguration);
            }
            json = await File.ReadAllTextAsync(path, cancellationToken);
        }
        else
        {
            json = value;
        }

        try
        {
            return JsonNode.Parse(json) as JsonObject
                ?? throw new JiraCliException("json_object_required", "Input JSON must be an object.", CliExitCode.UsageOrConfiguration);
        }
        catch (JsonException exception)
        {
            throw new JiraCliException("invalid_json", "Input is not valid JSON.", CliExitCode.UsageOrConfiguration, innerException: exception);
        }
    }

    public static JsonObject Preview(string method, string path, JsonNode? body) => new()
    {
        ["dryRun"] = true,
        ["serverValidated"] = false,
        ["method"] = method,
        ["path"] = path,
        ["body"] = body?.DeepClone()
    };

    public static JsonArray RequireArray(JsonNode? node, string property)
        => node?[property] as JsonArray ?? [];
}

internal sealed class ContentOptions
{
    public Option<string?> Text { get; } = CommandUtilities.Option<string?>("--text", "Inline plain text, Markdown, or ADF JSON.");
    public Option<string?> File { get; } = CommandUtilities.Option<string?>("--file", "Read content from a file.");
    public Option<bool> StandardInput { get; } = CommandUtilities.Option<bool>("--stdin", "Read content from standard input.");
    public Option<string> Format { get; } = CommandUtilities.Option("--format", "Content format: plain, markdown, or adf.", defaultValue: "plain");

    public void AddTo(Command command, bool required)
    {
        command.Options.Add(Text);
        command.Options.Add(File);
        command.Options.Add(StandardInput);
        command.Options.Add(Format);
        Required = required;
    }

    public bool Required { get; private set; }

    public async Task<(JsonObject? Document, IReadOnlyList<string> Warnings)> ReadAsync(
        ParseResult parseResult,
        bool tokenUsesStandardInput,
        CancellationToken cancellationToken)
    {
        var text = parseResult.GetValue(Text);
        var file = parseResult.GetValue(File);
        var useStdin = parseResult.GetValue(StandardInput);
        var sourceCount = (text is null ? 0 : 1) + (file is null ? 0 : 1) + (useStdin ? 1 : 0);
        if (sourceCount == 0)
        {
            if (Required)
            {
                throw new JiraCliException("content_required", "Supply one of --text, --file, or --stdin.", CliExitCode.UsageOrConfiguration);
            }
            return (null, []);
        }
        if (sourceCount > 1)
        {
            throw new JiraCliException("conflicting_content_sources", "Use only one of --text, --file, or --stdin.", CliExitCode.UsageOrConfiguration);
        }
        if (useStdin && tokenUsesStandardInput)
        {
            throw new JiraCliException("stdin_conflict", "Standard input cannot provide both the API token and command content.", CliExitCode.UsageOrConfiguration);
        }

        string content;
        if (text is not null)
        {
            content = text;
        }
        else if (file is not null)
        {
            var info = new FileInfo(file);
            if (!info.Exists)
            {
                throw new JiraCliException("input_file_not_found", $"Content file was not found: {file}", CliExitCode.UsageOrConfiguration);
            }
            if (info.Length > 16 * 1024 * 1024)
            {
                throw new JiraCliException("input_too_large", "Content exceeds the 16 MiB safety limit.", CliExitCode.UsageOrConfiguration);
            }
            content = await System.IO.File.ReadAllTextAsync(file, cancellationToken);
        }
        else
        {
            content = await Console.In.ReadToEndAsync(cancellationToken);
        }

        return parseResult.GetValue(Format)!.ToLowerInvariant() switch
        {
            "plain" => (AtlassianDocument.FromPlainText(content), []),
            "adf" => (AtlassianDocument.Parse(content), []),
            "markdown" => ConvertMarkdown(content),
            _ => throw new JiraCliException("invalid_content_format", "--format must be plain, markdown, or adf.", CliExitCode.UsageOrConfiguration)
        };
    }

    private static (JsonObject, IReadOnlyList<string>) ConvertMarkdown(string content)
    {
        var result = AtlassianDocument.FromMarkdown(content);
        return (result.Document, result.Warnings);
    }
}

internal sealed class PagingOptions
{
    public Option<int> PageSize { get; } = CommandUtilities.Option("--page-size", "Items requested per page (1-100).", defaultValue: 50);
    public Option<bool> All { get; } = CommandUtilities.Option<bool>("--all", "Retrieve every page.");

    public void AddTo(Command command)
    {
        command.Options.Add(PageSize);
        command.Options.Add(All);
    }

    public (int PageSize, bool All) Read(ParseResult parseResult, int profileDefault)
    {
        var configured = parseResult.GetValue(PageSize);
        var pageSize = configured == 50 && profileDefault != 50 ? profileDefault : configured;
        if (pageSize is < 1 or > 100)
        {
            throw new JiraCliException("invalid_page_size", "--page-size must be between 1 and 100.", CliExitCode.UsageOrConfiguration);
        }
        return (pageSize, parseResult.GetValue(All));
    }
}
