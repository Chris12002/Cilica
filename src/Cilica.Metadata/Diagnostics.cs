using System;

namespace Cilica.Metadata;

public enum DiagnosticSeverity
{
    Error,
    Warning,
    Info,
}

/// <summary>A stable, actionable finding about an input assembly.</summary>
/// <param name="Location">The affected metadata entity, or null for the whole input.</param>
public sealed record Diagnostic(string Id, DiagnosticSeverity Severity, string Message, string? Location = null);

/// <summary>
/// Diagnostic identifiers. Ranges: CIL1xxx input and metadata problems,
/// CIL2xxx unsupported semantics, CIL3xxx resolution failures.
/// Identifiers are never reused for a different meaning.
/// </summary>
public static class DiagnosticIds
{
    public const string InputUnreadable = "CIL1001";
    public const string NotPortableExecutable = "CIL1002";
    public const string NoCliMetadata = "CIL1003";
    public const string MalformedMetadata = "CIL1004";
    public const string ContainsNativeCode = "CIL1005";
    public const string MalformedEntity = "CIL1006";
    public const string NoAssemblyManifest = "CIL1007";
    public const string ReferenceAssembly = "CIL1008";
}

public static class DiagnosticSeverityExtensions
{
    public static string ToDisplayString(this DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => "error",
        DiagnosticSeverity.Warning => "warning",
        DiagnosticSeverity.Info => "info",
        _ => throw new ArgumentOutOfRangeException(nameof(severity)),
    };
}
