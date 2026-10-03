using System;
using System.Reflection.Metadata;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

/// <summary>Lazy selected-runtime identity; primitive-only profiles need not provide String.</summary>
internal sealed class ObjectStringType(AssemblyResolver resolver, ResolvedAssembly library, MemberResolutionBudget budget)
{
    private ResolutionResult<ResolvedType>? _selected, _validated;
    internal int ValidationWork { get; private set; }
    private ResolutionResult<ResolvedType> Selected => _selected ??= resolver.ResolveRuntimeType(library, PrimitiveTypeCode.String);
    internal bool Matches(ResolvedType type) => Selected.Value == type;
    internal ResolvedType Require(int maximumWork = ObjectModelLimits.MaximumMetadataMembers)
    {
        if (_validated is null)
        {
            int available = Math.Min(maximumWork, budget.Remaining);
            if (available <= 0) throw new Failure(new(DiagnosticIds.TranslationLimit, DiagnosticSeverity.Error, "String metadata budget exhausted."));
            var limited = new MemberResolutionBudget(available);
            _validated = resolver.ResolveIntrinsicStringType(library, limited);
            ValidationWork = available - limited.Remaining;
            _ = budget.TryConsume(ValidationWork);
        }
        if (ValidationWork > maximumWork)
            throw new Failure(new(DiagnosticIds.TranslationLimit, DiagnosticSeverity.Error, "String metadata exceeds the remaining catalog budget."));
        return _validated.Value ?? throw new Failure(_validated.Diagnostics[0]);
    }
    internal sealed class Failure(Diagnostic diagnostic) : Exception(diagnostic.Message)
    {
        internal Diagnostic Diagnostic { get; } = diagnostic;
    }
}
