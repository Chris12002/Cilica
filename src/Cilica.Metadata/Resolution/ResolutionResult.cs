using System.Collections.Generic;
using System.Collections.Immutable;

namespace Cilica.Metadata.Resolution;

/// <summary>An expected resolution failure is data, not an exception or a partial success.</summary>
public sealed class ResolutionResult<T> where T : class
{
    private ResolutionResult(T? value, ImmutableArray<Diagnostic> diagnostics)
    {
        Value = value;
        Diagnostics = diagnostics;
    }

    public T? Value { get; }
    public ImmutableArray<Diagnostic> Diagnostics { get; }
    public bool Success => Value is not null;

    internal static ResolutionResult<T> Resolved(T value) => new(value, []);
    internal static ResolutionResult<T> Failed(IEnumerable<Diagnostic> diagnostics) => new(null, [.. diagnostics]);
    internal static ResolutionResult<T> Failed(string id, string message, string? location = null) =>
        new(null, [new Diagnostic(id, DiagnosticSeverity.Error, message, location)]);
}
