using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Cilica.Metadata;
using Cilica.Metadata.Output;

namespace Cilica.Cli;

/// <summary>Parses command-line arguments and runs <c>cilica</c> commands.</summary>
public static class CilicaCommand
{
    public const int Success = 0;
    public const int ErrorsReported = 1;
    public const int UsageError = 2;
    public const int InternalError = 3;

    private const string Usage =
        "Usage:\n" +
        "  cilica inspect <assembly> [--format text|json] [--output <file>]\n" +
        "  cilica --help\n" +
        "  cilica --version\n" +
        "\n" +
        "Commands:\n" +
        "  inspect   Report an assembly's metadata without loading or running it.\n" +
        "\n" +
        "Exit codes: 0 success, 1 errors reported, 2 usage error, 3 internal error.\n";

    public static int Run(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (args.Count == 0)
        {
            stderr.Write(Usage);
            return UsageError;
        }

        switch (args[0])
        {
            case "-h" or "--help" or "help":
                stdout.Write(Usage);
                return Success;
            case "--version":
                stdout.Write(Version() + "\n");
                return Success;
            case "inspect":
                return Inspect(args, stdout, stderr);
            default:
                stderr.Write($"cilica: unknown command '{args[0]}'.\n\n{Usage}");
                return UsageError;
        }
    }

    private static int Inspect(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr)
    {
        string? input = null;
        string format = "text";
        string? output = null;

        for (int index = 1; index < args.Count; index++)
        {
            string argument = args[index];
            switch (argument)
            {
                case "-h" or "--help":
                    stdout.Write(Usage);
                    return Success;
                case "--format" or "--output":
                    if (index + 1 >= args.Count)
                    {
                        return Fail(stderr, $"option '{argument}' needs a value.");
                    }
                    string value = args[++index];
                    if (argument == "--format")
                    {
                        if (value is not ("text" or "json"))
                        {
                            return Fail(stderr, $"unknown format '{value}'; use 'text' or 'json'.");
                        }
                        format = value;
                    }
                    else
                    {
                        output = value;
                    }
                    break;
                default:
                    if (argument.StartsWith('-'))
                    {
                        return Fail(stderr, $"unknown option '{argument}'.");
                    }
                    if (input is not null)
                    {
                        return Fail(stderr, "inspect takes exactly one assembly.");
                    }
                    input = argument;
                    break;
            }
        }

        if (input is null)
        {
            return Fail(stderr, "inspect needs an assembly path.");
        }

        AssemblyReport report;
        string rendered;
        try
        {
            report = AssemblyInspector.Inspect(input);
            rendered = format == "json" ? JsonReportWriter.Write(report) : TextReportWriter.Write(report);
        }
#pragma warning disable CA1031 // Unexpected failures are reported as internal errors instead of stack traces.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            stderr.Write($"cilica: internal error ({exception.GetType().FullName}): {exception.Message}\n" +
                "Please report this with the input that caused it.\n");
            return InternalError;
        }

        if (output is null)
        {
            stdout.Write(rendered);
        }
        else
        {
            try
            {
                File.WriteAllText(output, rendered, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                stderr.Write($"cilica: cannot write '{output}': {exception.Message}\n");
                return ErrorsReported;
            }
        }

        return report.HasErrors ? ErrorsReported : Success;
    }

    private static int Fail(TextWriter stderr, string message)
    {
        stderr.Write($"cilica: {message}\n\n{Usage}");
        return UsageError;
    }

    private static string Version() =>
        "cilica " + (typeof(CilicaCommand).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown");
}
