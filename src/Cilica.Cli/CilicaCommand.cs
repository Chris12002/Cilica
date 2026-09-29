using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Output;

namespace Cilica.Cli;

/// <summary>Parses command-line arguments and runs <c>cilica</c> commands.</summary>
public static partial class CilicaCommand
{
    public const int Success = 0;
    public const int ErrorsReported = 1;
    public const int UsageError = 2;
    public const int InternalError = 3;

    private const string Usage =
        "Usage:\n" +
        "  cilica inspect <assembly> [--format text|json] [--output <file>]\n" +
        "  cilica il <assembly> [--method <name>] [--format text|json] [--output <file>]\n" +
        "  cilica audit <assembly> [--reference <directory>]... [--runtime-library <assembly>]\n" +
        "    (--public-surface | --root <hex-token>... | --root-type <metadata-name>...)\n" +
        "    [--format text|json] [--output <file>]\n" +
        "  cilica --help\n" +
        "  cilica --version\n" +
        "\n" +
        "Commands:\n" +
        "  inspect   Report an assembly's metadata without loading or running it.\n" +
        "  il        Decode and validate method bodies: instructions, basic blocks,\n" +
        "            exception regions and stack depths. --method selects methods by\n" +
        "            name, Type::Name or Type::Name(parameter types).\n" +
        "  audit     Trace explicit roots, dependencies, feature evidence and analysis gaps.\n" +
        "            --root and --root-type may be combined; nested types use '/'.\n" +
        "            Reference directories are explicit; no runtime probing occurs.\n" +
        "            --runtime-library selects primitive/string receiver mappings.\n" +
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
            case "audit":
                return RunAudit(args, stdout, stderr);
            case "inspect":
            case "il":
                return RunReportCommand(args, stdout, stderr);
            default:
                stderr.Write($"cilica: unknown command '{args[0]}'.\n\n{Usage}");
                return UsageError;
        }
    }

    private sealed record Options(string Input, string Format, string? Output, string? Method);

    private static int RunReportCommand(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr)
    {
        string command = args[0];
        string? input = null;
        string format = "text";
        string? output = null;
        string? method = null;

        for (int index = 1; index < args.Count; index++)
        {
            string argument = args[index];
            switch (argument)
            {
                case "-h" or "--help":
                    stdout.Write(Usage);
                    return Success;
                case "--format" or "--output" or "--method":
                    if (argument == "--method" && command != "il")
                    {
                        return Fail(stderr, $"option '--method' is only valid for 'il'.");
                    }
                    if (index + 1 >= args.Count)
                    {
                        return Fail(stderr, $"option '{argument}' needs a value.");
                    }
                    string value = args[++index];
                    switch (argument)
                    {
                        case "--format" when value is not ("text" or "json"):
                            return Fail(stderr, $"unknown format '{value}'; use 'text' or 'json'.");
                        case "--format":
                            format = value;
                            break;
                        case "--output":
                            output = value;
                            break;
                        default:
                            method = value;
                            break;
                    }
                    break;
                default:
                    if (argument.StartsWith('-'))
                    {
                        return Fail(stderr, $"unknown option '{argument}'.");
                    }
                    if (input is not null)
                    {
                        return Fail(stderr, $"{command} takes exactly one assembly.");
                    }
                    input = argument;
                    break;
            }
        }

        if (input is null)
        {
            return Fail(stderr, $"{command} needs an assembly path.");
        }

        var options = new Options(input, format, output, method);
        return command == "il" ? Il(options, stdout, stderr) : Inspect(options, stdout, stderr);
    }

    private static int Inspect(Options options, TextWriter stdout, TextWriter stderr) =>
        Execute(() =>
        {
            AssemblyReport report = AssemblyInspector.Inspect(options.Input);
            string rendered = options.Format == "json" ? JsonReportWriter.Write(report) : TextReportWriter.Write(report);
            return (rendered, report.HasErrors);
        }, options, stdout, stderr);

    private static int Il(Options options, TextWriter stdout, TextWriter stderr)
    {
        Func<string, bool>? filter = options.Method is null ? null : name => MethodMatches(name, options.Method);
        bool noMatch = false;
        int result = Execute(() =>
        {
            ILReport report = MethodBodyInspector.Inspect(options.Input, filter);
            noMatch = filter is not null && report.Methods.IsEmpty && !report.HasErrors;
            string rendered = options.Format == "json" ? ILJsonWriter.Write(report) : ILTextWriter.Write(report);
            return (rendered, report.HasErrors);
        }, options, stdout, stderr);
        if (noMatch)
        {
            stderr.Write($"cilica: no method with a body matches '{options.Method}'.\n");
            return ErrorsReported;
        }
        return result;
    }

    /// <summary>Matches a method by simple name, <c>Type::Name</c> or its full <c>Type::Name(parameter types)</c>.</summary>
    internal static bool MethodMatches(string name, string pattern)
    {
        if (name == pattern)
        {
            return true;
        }
        int parameters = name.IndexOf('(', StringComparison.Ordinal);
        string qualified = parameters < 0 ? name : name[..parameters];
        int generics = qualified.IndexOf('<', qualified.LastIndexOf("::", StringComparison.Ordinal) + 2);
        string withoutGenerics = generics < 0 ? qualified : qualified[..generics];
        int separator = withoutGenerics.LastIndexOf("::", StringComparison.Ordinal);
        string simple = separator < 0 ? withoutGenerics : withoutGenerics[(separator + 2)..];
        return pattern == qualified || pattern == withoutGenerics || pattern == simple;
    }

    private static int Execute(Func<(string Rendered, bool HasErrors)> produce, Options options, TextWriter stdout, TextWriter stderr)
    {
        string rendered;
        bool hasErrors;
        try
        {
            (rendered, hasErrors) = produce();
        }
#pragma warning disable CA1031 // Unexpected failures are reported as internal errors instead of stack traces.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            stderr.Write($"cilica: internal error ({exception.GetType().FullName}): {exception.Message}\n" +
                "Please report this with the input that caused it.\n");
            return InternalError;
        }

        if (options.Output is null)
        {
            stdout.Write(rendered);
        }
        else
        {
            try
            {
                File.WriteAllText(options.Output, rendered, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                stderr.Write($"cilica: cannot write '{options.Output}': {exception.Message}\n");
                return ErrorsReported;
            }
        }

        return hasErrors ? ErrorsReported : Success;
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
