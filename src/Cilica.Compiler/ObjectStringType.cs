using System;
using System.Reflection.Metadata;
using System.Collections.Immutable;
using System.Linq;
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
    private ImmutableArray<ObjectTypeId> _interfaces;
    internal int CheckInterfaces(ImmutableArray<ObjectInterface> interfaces, int maximumWork)
    {
        int work = 0;
        if (_interfaces.IsDefault)
        {
            int available = Math.Min(maximumWork, budget.Remaining);
            if (available <= 0) throw new Failure(new(DiagnosticIds.TranslationLimit, DiagnosticSeverity.Error, "String interface metadata budget exhausted."));
            var limited = new MemberResolutionBudget(available);
            var result = resolver.ReadIntrinsicStringInterfaces(Require(), limited);
            work = available - limited.Remaining;
            _ = budget.TryConsume(work);
            if (result.Value is null) throw new Failure(result.Diagnostics[0]);
            _interfaces = [.. result.Value.Types.Select(ObjectModelBuilder.Id)];
        }
        if (interfaces.Any(i => _interfaces.Contains(ObjectTypeId.From(i.Source))))
            throw new Failure(new(DiagnosticIds.ExecutionUnsupported, DiagnosticSeverity.Error,
                "This program uses an interface implemented by the selected String. String interface conversions and array stores require later lowering."));
        return work;
    }
    internal sealed class Failure(Diagnostic diagnostic) : Exception(diagnostic.Message)
    {
        internal Diagnostic Diagnostic { get; } = diagnostic;
    }
}
