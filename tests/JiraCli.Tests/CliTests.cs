using JiraCli.Cli;
using JiraCli.Core;

namespace JiraCli.Tests;

public sealed class CliTests
{
    [Fact]
    public void Version_does_not_include_source_revision_metadata()
    {
        var version = typeof(CliApplication).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), inherit: false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>()
            .Single()
            .InformationalVersion;

        Assert.DoesNotContain('+', version);
    }

    [Fact]
    public async Task Create_dry_run_preserves_custom_fields_without_network_write()
    {
        var result = await RunAsync([
            "issue", "create",
            "--project", "DEMO",
            "--issue-type", "10001",
            "--summary", "Synthetic",
            "--fields-json", "{\"customfield_12345\":{\"value\":\"Blue\"}}",
            "--dry-run",
            "--url", "https://example.atlassian.net",
            "--user", "user@example.com",
            "--token", "synthetic-token",
            "--json"
        ]);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("customfield_12345", result.Output, StringComparison.Ordinal);
        Assert.Contains("\"serverValidated\":false", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-token", result.Output + result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Read_only_is_enforced_before_dry_run_mutation()
    {
        var result = await RunAsync([
            "issue", "create",
            "--project", "DEMO",
            "--issue-type", "10001",
            "--summary", "Synthetic",
            "--dry-run",
            "--read-only",
            "--url", "https://example.atlassian.net",
            "--user", "user@example.com",
            "--token", "synthetic-token",
            "--json"
        ]);
        Assert.Equal((int)CliExitCode.SafetyRefusal, result.ExitCode);
        Assert.Contains("read_only_violation", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_command_line_uses_documented_usage_exit_code()
    {
        var result = await RunAsync(["issue", "get"]);
        Assert.Equal((int)CliExitCode.UsageOrConfiguration, result.ExitCode);
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var output = new StringWriter();
        var error = new StringWriter();
        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            var exitCode = await CliApplication.RunAsync(args);
            return (exitCode, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
