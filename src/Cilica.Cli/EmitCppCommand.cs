using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;

namespace Cilica.Cli;

public static partial class CilicaCommand
{
    private static int RunEmitCpp(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr)
    {
        string? input = null, output = null;
        int? root = null;
        var references = new List<string>();
        for (int index = 1; index < args.Count; index++)
        {
            string argument = args[index];
            switch (argument)
            {
                case "--help" or "-h": stdout.Write(Usage); return Success;
                case "--reference" or "--root" or "--output":
                    if (++index >= args.Count || args[index].StartsWith('-')) return Fail(stderr, $"option '{argument}' needs a value.");
                    string value = args[index];
                    if (argument == "--reference") references.Add(value);
                    else if (argument == "--output")
                    {
                        if (output is not null) return Fail(stderr, "--output may be supplied only once.");
                        output = value;
                    }
                    else
                    {
                        if (root is not null) return Fail(stderr, "emit-cpp takes exactly one --root.");
                        string hex = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
                        if (!int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int token)
                            || (uint)token >> 24 != 0x06 || (token & 0xffffff) == 0)
                            return Fail(stderr, "--root needs a non-nil hexadecimal MethodDef token.");
                        root = token;
                    }
                    break;
                default:
                    if (argument.StartsWith('-')) return Fail(stderr, $"unknown option '{argument}'.");
                    if (input is not null) return Fail(stderr, "emit-cpp takes exactly one assembly.");
                    input = argument;
                    break;
            }
        }
        if (input is null || root is null) return Fail(stderr, "emit-cpp needs an assembly path and exactly one --root.");

        // Execute owns the unexpected-failure/write boundary. Diagnostics go to
        // stderr; a failed translation must never overwrite a C++ output file.
        return Execute(() =>
        {
            var created = AssemblyResolver.Create(references);
            if (created.Value is not AssemblyResolver resolver) return Failure(created.Diagnostics);
            using (resolver)
            {
                var opened = resolver.OpenInput(input);
                if (opened.Value is not ResolvedAssembly assembly) return Failure(opened.Diagnostics);
                TranslationResult translated = Int32Translator.Translate(resolver, assembly, MetadataTokens.MethodDefinitionHandle(root.Value & 0xffffff));
                if (translated.Method is null) return Failure(translated.Diagnostics);
                return (CppEmitter.Emit(translated.Method).Text, false);
            }
        }, new(input, "text", output, null), stdout, stderr, suppressOutputOnErrors: true);

        (string, bool) Failure(IEnumerable<Diagnostic> diagnostics)
        {
            foreach (Diagnostic diagnostic in diagnostics)
                stderr.Write($"{diagnostic.Id} {diagnostic.Severity.ToString().ToLowerInvariant()}: {diagnostic.Message} ({diagnostic.Location})\n");
            return ("", true);
        }
    }
}
