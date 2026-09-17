using System.Text.Json;
using System.Text.Json.Nodes;
using JiraCli.Core;

namespace JiraCli.Cli;

internal sealed class OutputWriter(bool json, bool quiet, TextWriter standardOutput, TextWriter standardError)
{
    private static readonly JsonSerializerOptions Compact = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions Indented = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public void Warning(string message)
    {
        if (!json && !quiet)
        {
            standardError.WriteLine($"warning: {message}");
        }
    }

    public int Success(object? data, string? message = null)
    {
        if (json)
        {
            standardOutput.WriteLine(JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                ok = true,
                data,
                message
            }, Compact));
        }
        else if (!quiet)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                standardOutput.WriteLine(message);
            }
            if (data is not null)
            {
                if (data is string text)
                {
                    standardOutput.WriteLine(text);
                }
                else
                {
                    standardOutput.WriteLine(JsonSerializer.Serialize(data, Indented));
                }
            }
        }
        return (int)CliExitCode.Success;
    }

    public int Failure(JiraCliException exception)
    {
        var message = SecretRedactor.Redact(exception.Message);
        if (json)
        {
            standardError.WriteLine(JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                ok = false,
                error = new
                {
                    code = exception.ErrorCode,
                    message,
                    details = exception.Details,
                    exitCode = (int)exception.ExitCode
                }
            }, Compact));
        }
        else
        {
            standardError.WriteLine($"error [{exception.ErrorCode}]: {message}");
            if (exception.Details is not null)
            {
                standardError.WriteLine(JsonSerializer.Serialize(exception.Details, Indented));
            }
        }
        return (int)exception.ExitCode;
    }

    public int Failure(string code, string message, CliExitCode exitCode)
        => Failure(new JiraCliException(code, message, exitCode));
}
