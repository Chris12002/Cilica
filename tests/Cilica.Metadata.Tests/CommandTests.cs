using System;
using System.IO;
using Cilica.Cli;
using Cilica.Metadata.Output;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class CommandTests
{
    [Fact]
    public void No_arguments_is_a_usage_error()
    {
        (int exitCode, string stdout, string stderr) = Run();

        Assert.Equal(CilicaCommand.UsageError, exitCode);
        Assert.Empty(stdout);
        Assert.Contains("Usage:", stderr, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("frobnicate")]
    [InlineData("inspect")]
    [InlineData("inspect", "a.dll", "b.dll")]
    [InlineData("inspect", "a.dll", "--format")]
    [InlineData("inspect", "a.dll", "--format", "xml")]
    [InlineData("inspect", "a.dll", "--unknown")]
    [InlineData("inspect", "a.dll", "--method", "Add")]
    [InlineData("il")]
    [InlineData("il", "a.dll", "--method")]
    public void Invalid_arguments_are_usage_errors(params string[] args)
    {
        (int exitCode, string stdout, string stderr) = Run(args);

        Assert.Equal(CilicaCommand.UsageError, exitCode);
        Assert.Empty(stdout);
        Assert.StartsWith("cilica: ", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Help_and_version_succeed()
    {
        Assert.Equal(CilicaCommand.Success, Run("--help").ExitCode);
        (int exitCode, string stdout, _) = Run("--version");

        Assert.Equal(CilicaCommand.Success, exitCode);
        Assert.Equal("cilica 0.1.0\n", stdout);
    }

    [Fact]
    public void Inspect_writes_the_report_to_standard_output()
    {
        string path = Fixture.PathOf(Fixture.Basic);

        (int exitCode, string stdout, string stderr) = Run("inspect", path, "--format", "json");

        Assert.Equal(CilicaCommand.Success, exitCode);
        Assert.Empty(stderr);
        Assert.Equal(JsonReportWriter.Write(AssemblyInspector.Inspect(path)), stdout);
    }

    [Fact]
    public void Inspect_writes_the_report_to_a_file()
    {
        string path = Fixture.PathOf(Fixture.Basic);
        string output = Path.Combine(Path.GetTempPath(), "cilica-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            (int exitCode, string stdout, _) = Run("inspect", path, "--output", output);

            Assert.Equal(CilicaCommand.Success, exitCode);
            Assert.Empty(stdout);
            Assert.Equal(TextReportWriter.Write(AssemblyInspector.Inspect(path)), File.ReadAllText(output));
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public void Inspect_exits_with_errors_reported_for_malformed_input()
    {
        string path = Path.Combine(Path.GetTempPath(), "cilica-" + Guid.NewGuid().ToString("N") + ".dll");
        File.WriteAllText(path, "not an assembly");
        try
        {
            (int exitCode, string stdout, _) = Run("inspect", path);

            Assert.Equal(CilicaCommand.ErrorsReported, exitCode);
            Assert.Contains(DiagnosticIds.NotPortableExecutable + " error:", stdout, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("Add")]
    [InlineData("Cilica.Fixtures.Basic.Maths::Add")]
    [InlineData("Cilica.Fixtures.Basic.Maths::Add(int32, int32)")]
    public void Il_selects_methods_by_simple_qualified_or_full_name(string method)
    {
        (int exitCode, string stdout, string stderr) = Run("il", Fixture.PathOf(Fixture.Basic), "--method", method);

        Assert.Equal(CilicaCommand.Success, exitCode);
        Assert.Empty(stderr);
        Assert.Contains("methods: 1\n", stdout, StringComparison.Ordinal);
        Assert.Contains("method Cilica.Fixtures.Basic.Maths::Add(int32, int32)\n", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Il_selects_generic_methods_without_their_parameter_list()
    {
        (int exitCode, string stdout, _) = Run("il", Fixture.PathOf(Fixture.Basic), "--method", "Cilica.Fixtures.Basic.Maths::Swap");

        Assert.Equal(CilicaCommand.Success, exitCode);
        Assert.Contains("method Cilica.Fixtures.Basic.Maths::Swap<T>(!!T&, !!T&)\n", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Il_reports_when_no_method_matches()
    {
        (int exitCode, _, string stderr) = Run("il", Fixture.PathOf(Fixture.Basic), "--method", "Missing");

        Assert.Equal(CilicaCommand.ErrorsReported, exitCode);
        Assert.Equal("cilica: no method with a body matches 'Missing'.\n", stderr);
    }

    private static (int ExitCode, string Stdout, string Stderr) Run(params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        int exitCode = CilicaCommand.Run(args, stdout, stderr);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }
}
