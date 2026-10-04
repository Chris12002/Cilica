using System.Collections.Immutable;
using System.Reflection.Metadata;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

public sealed record IntegerTranslationResult(IntegerIrProgram? Program, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Program is not null;
}

/// <summary>Explicit static Int32/UInt32/Int64/UInt64 CIL closures; no heap, generic or native-pointer semantics.</summary>
public static class IntegerTranslator
{
    public const string Profile = "integer-arithmetic-cil-v1";
    public static IntegerTranslationResult TranslateProgram(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle entry, MethodReadLimits? limits = null) =>
        Int32Translator.TranslateIntegerProgram(resolver, assembly, entry, limits);
}
