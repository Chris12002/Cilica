using System.Collections.Immutable;
using System.Reflection;
using Cilica.Metadata.Resolution;

namespace Cilica.Metadata.IL;

/// <summary>Scoped provenance; strings and tokens do not retain a metadata reader.</summary>
public sealed record MethodSource(string AssemblyIdentity, string ContentHash, int Token, string Name);

/// <summary>Decoded instruction snapshot. Integer operands include implicit argument indices.</summary>
public sealed record MethodInstruction(int Offset, int Length, string OpCode, int? IntegerOperand,
    ImmutableArray<int> Targets);

/// <summary>
/// Bounded input for translation. Signatures use resolver-owned type definitions;
/// translation must finish before disposing that resolver.
/// </summary>
public sealed record MethodInput(MethodSource Source, MethodSignature Signature, MethodAttributes Attributes,
    MethodImplAttributes Implementation, int OwnerGenericArity, bool IsReferenceAssembly,
    bool HasTypeInitializer, bool HasModuleInitializer, ImmutableArray<TypeSignature> Locals,
    bool InitializeLocals, int ExceptionRegionCount,
    int CodeSize, int MaxStack, ImmutableArray<MethodInstruction> Instructions,
    int OwnerToken, bool OwnerExternallyVisible)
{
    public string DeclaredName { get; init; } = "";
    public int MetadataMembersRead { get; init; }
}

public sealed record MethodReadLimits(int MaximumBodyBytes = 1048576, int MaximumInstructions = 65536,
    int MaximumInitializerMembers = 65536, int MaximumOwnerDepth = 64);
