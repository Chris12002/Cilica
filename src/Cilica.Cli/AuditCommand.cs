using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.Analysis;
using Cilica.Metadata.Output;
using Cilica.Metadata.Resolution;

namespace Cilica.Cli;

public static partial class CilicaCommand
{
    private static int RunAudit(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr)
    {
        string? input = null, output = null, runtimeLibraryPath = null;
        string format = "text";
        bool publicSurface = false;
        var references = new List<string>();
        var contracts = new List<string>();
        var roots = new List<EntityHandle>();
        var types = new List<string>();
        for (int index = 1; index < args.Count; index++)
        {
            string argument = args[index];
            switch (argument)
            {
                case "--help" or "-h": stdout.Write(Usage); return Success;
                case "--public-surface": publicSurface = true; break;
                case "--reference" or "--root" or "--root-type" or "--format" or "--output" or "--runtime-library" or "--contract-only":
                    if (++index >= args.Count || args[index].StartsWith("--", StringComparison.Ordinal))
                        return Fail(stderr, $"option '{argument}' needs a value.");
                    string value = args[index];
                    switch (argument)
                    {
                        case "--reference": references.Add(value); break;
                        case "--contract-only":
                            if (contracts.Count == 256) return Fail(stderr, "at most 256 --contract-only selections are supported.");
                            contracts.Add(value); break;
                        case "--runtime-library":
                            if (runtimeLibraryPath is not null) return Fail(stderr, "--runtime-library may be supplied only once.");
                            runtimeLibraryPath = value;
                            break;
                        case "--root-type": types.Add(value); break;
                        case "--root":
                            string hex = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
                            if (!int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int token)
                                || (uint)token >> 24 is not (0x02 or 0x04 or 0x06) || (token & 0xffffff) == 0)
                                return Fail(stderr, "--root needs a non-nil hexadecimal TypeDef, MethodDef or FieldDef token.");
                            roots.Add(MetadataTokens.EntityHandle(token));
                            break;
                        case "--format":
                            if (value is not ("text" or "json")) return Fail(stderr, "--format needs text or json.");
                            format = value;
                            break;
                        case "--output": output = value; break;
                    }
                    break;
                default:
                    if (argument.StartsWith('-')) return Fail(stderr, $"unknown option '{argument}'.");
                    if (input is not null) return Fail(stderr, "audit takes exactly one assembly.");
                    input = argument;
                    break;
            }
        }
        if (input is null) return Fail(stderr, "audit needs an assembly path.");
        bool explicitRoots = roots.Count != 0 || types.Count != 0;
        if (publicSurface == explicitRoots) return Fail(stderr, "select --public-surface OR one or more --root/--root-type values.");
        var options = new Options(input, format, output, null);
        return Execute(() =>
        {
            AuditReport report;
            var created = AssemblyResolver.Create(references);
            if (created.Value is not AssemblyResolver resolver) report = Failure(created.Diagnostics);
            else
            {
                using (resolver)
                {
                    report = Inspect(resolver);
                }
            }
            return (format == "json" ? AuditReportWriter.Json(report) : AuditReportWriter.Text(report), report.HasErrors);
        }, options, stdout, stderr);

        AuditReport Inspect(AssemblyResolver resolver)
        {
            var opened = resolver.OpenInput(input);
            if (opened.Value is not ResolvedAssembly assembly) return Failure(opened.Diagnostics);
            ResolvedAssembly? runtimeLibrary = null;
            if (runtimeLibraryPath is not null)
            {
                var selected = resolver.OpenInput(runtimeLibraryPath);
                if (selected.Value is null) return Failure(selected.Diagnostics);
                runtimeLibrary = selected.Value;
            }
            var libraries = new List<ResolvedAssembly>();
            foreach (string path in contracts)
            {
                var selected = resolver.OpenInput(path);
                if (selected.Value is not { } library) return Failure(selected.Diagnostics);
                if (library.Identity.Equals(assembly.Identity) && library.ContentHash == assembly.ContentHash)
                    return Failure([new(DiagnosticIds.ResolutionUnsupported, DiagnosticSeverity.Error,
                        "The input assembly cannot be a library contract boundary.")]);
                libraries.Add(library);
            }
            return resolver.Audit(assembly, roots, types, publicSurface, runtimeLibrary: runtimeLibrary, contractAssemblies: libraries);
        }

        AuditReport Failure(IEnumerable<Diagnostic> diagnostics) => new(Path.GetFileName(input),
            publicSurface ? ["public-surface"] : [.. roots.Distinct().OrderBy(MetadataTokens.GetToken)
                .Select(h => "token:0x" + MetadataTokens.GetToken(h).ToString("x8", CultureInfo.InvariantCulture))
                .Concat(types.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(t => "type:" + t))],
            new(), [], [], [], [], [], [.. diagnostics.Select(d => new ReachabilityIssue(null, null, d))]);
    }
}
