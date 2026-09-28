using System.Collections.Immutable;
using System.Linq;

namespace Cilica.Metadata;

/// <summary>
/// Everything <see cref="AssemblyInspector"/> reports about one input file.
/// Collections are in deterministic order: sorted by name, except fields,
/// which keep declaration order because it determines type layout.
/// </summary>
public sealed record AssemblyReport(
    string InputName,
    AssemblyIdentityReport? Identity,
    string? ModuleName,
    string? TargetFramework,
    ImmutableArray<string> ImageFlags,
    string? EntryPoint,
    ImmutableArray<string> Attributes,
    ImmutableArray<AssemblyIdentityReport> References,
    ImmutableArray<ExportedTypeReport> ExportedTypes,
    ImmutableArray<TypeReport> Types,
    ImmutableArray<Diagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

    internal static AssemblyReport Failed(string inputName, Diagnostic diagnostic) => new(
        inputName, null, null, null, [], null, [], [], [], [], [diagnostic]);
}

public sealed record AssemblyIdentityReport(string Name, string Version, string Culture, string? PublicKeyToken)
{
    public string DisplayName =>
        $"{Name}, Version={Version}, Culture={Culture}, PublicKeyToken={PublicKeyToken ?? "null"}";
}

/// <param name="Target">The assembly a forwarder points to, or the file of a type exported from another module.</param>
public sealed record ExportedTypeReport(string Name, string Target, bool IsForwarder);

public sealed record TypeReport(
    string Name,
    string Kind,
    string Visibility,
    ImmutableArray<string> Flags,
    string? Layout,
    string? BaseType,
    ImmutableArray<string> Interfaces,
    ImmutableArray<GenericParameterReport> GenericParameters,
    ImmutableArray<string> Attributes,
    ImmutableArray<FieldReport> Fields,
    ImmutableArray<MethodReport> Methods,
    ImmutableArray<PropertyReport> Properties,
    ImmutableArray<EventReport> Events,
    ImmutableArray<string> Overrides);

/// <param name="Constraints">Special constraints (class, valuetype, new(), byreflike) followed by type constraints.</param>
public sealed record GenericParameterReport(string Name, string? Variance, ImmutableArray<string> Constraints);

public sealed record FieldReport(
    string Name,
    string Type,
    string Visibility,
    ImmutableArray<string> Flags,
    string? Value,
    int? Offset,
    ImmutableArray<string> Attributes);

/// <param name="Signature">Return type, name, generic parameters and parameters.</param>
/// <param name="Import">Platform-invoke target, when the method is imported from native code.</param>
public sealed record MethodReport(
    string Name,
    string Signature,
    string Visibility,
    ImmutableArray<string> Flags,
    string? Import,
    bool HasBody,
    ImmutableArray<string> Attributes);

public sealed record PropertyReport(string Name, string Signature, ImmutableArray<string> Accessors, ImmutableArray<string> Attributes);

public sealed record EventReport(string Name, string Type, ImmutableArray<string> Accessors, ImmutableArray<string> Attributes);
