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
    public const string MalformedMethodBody = "CIL1010";
    public const string InvalidControlTransfer = "CIL1011";
    public const string InvalidExceptionRegion = "CIL1012";
    public const string InconsistentStack = "CIL1013";
    public const string InvalidOperand = "CIL1014";
    public const string ExecutionUnsupported = "CIL2001";
    public const string ExecutionSignatureUnsupported = "CIL2002";
    public const string TranslationLimit = "CIL2003";
    public const string ReferenceDirectoryInvalid = "CIL3001";
    public const string ReferenceImageInvalid = "CIL3002";
    public const string AssemblyNotFound = "CIL3003";
    public const string AssemblyConflict = "CIL3004";
    public const string TypeNotFound = "CIL3005";
    public const string TypeConflict = "CIL3006";
    public const string TypeResolutionCycle = "CIL3007";
    public const string ResolutionUnsupported = "CIL3008";
    public const string InvalidSignature = "CIL3009";
    public const string MemberNotFound = "CIL3010";
    public const string MemberConflict = "CIL3011";
    public const string ReachabilityIncomplete = "CIL3012";
    public const string ReachabilityLimit = "CIL3013";
}

internal static class MalformedInput
{
    /// <summary>
    /// Exceptions System.Reflection.Metadata raises for damaged input. It mostly throws
    /// BadImageFormatException, but corrupted sizes and counts can surface as arithmetic overflow.
    /// </summary>
    public static bool IsMalformed(Exception exception) => exception is BadImageFormatException or OverflowException;
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
