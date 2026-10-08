using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata.Resolution;
using Cilica.Metadata;

namespace Cilica.Compiler;

public enum ObjectMethodKind { Static, Instance, Constructor, ObjectBaseConstructor, ValueConstructor, ValueInstance, PrimitiveInstance }

/// <summary>Detached declaration facts; instance signatures include the explicit receiver at parameter zero.</summary>
public sealed record ObjectMethodBinding(IrMethodId Method, ObjectTypeId Owner, ObjectMethodKind Kind,
    MethodAttributes Attributes, bool OwnerExternallyVisible)
{
    public ImmutableArray<LexicalTypeOwner> LexicalOwners { get; init; } = [];
    public string? TypeInitializerName { get; init; }
    public bool InvariantProviderOnly { get; init; }
    public PrimitiveReceiver? Primitive { get; init; }
    public PrimitiveReceiver? IntegerParser { get; init; }
    public bool ThrowingIntegerParser { get; init; }
    public ObjectVirtualSlot? VirtualSlot { get; init; }
    public string? VirtualName { get; init; }
    public ClosedGenericEvidence? GenericInterfaceMethod { get; init; }
    public bool OwnerAssemblyVisible { get; init; }
}

/// <summary>Exact selected primitive identity; its receiver is an existing scalar borrow, never an object layout.</summary>
public sealed record PrimitiveReceiver(TypeSource Source, PrimitiveTypeCode Code)
{
    public IrType Storage => Code is PrimitiveTypeCode.Char or PrimitiveTypeCode.Int32 or PrimitiveTypeCode.UInt32 ? IrType.Int32 : IrType.Int64;
    public IrType ReceiverType => IrType.Location(Storage, narrow: Code == PrimitiveTypeCode.Char ? IntegerCastTarget.UInt16 : null);
}

/// <summary>A class virtual signature with no executable body. Its binding must be abstract.</summary>
public sealed record ObjectAbstractMethodDeclaration(Cilica.Metadata.IL.MethodSource Source,
    ImmutableArray<IrType> Parameters, IrType ReturnType)
{
    internal IrMethod Signature => new(Source, Parameters, ReturnType, [], []);
}

public sealed record ObjectVirtualSlot(IrMethodId Method, ObjectTypeId Owner);
public sealed record ObjectVirtualTarget(ObjectTypeId Receiver, IrMethodId Implementation);
public sealed record ObjectVirtualDispatch(IrMethodId Declaration, ImmutableArray<ObjectVirtualTarget> Targets)
{
    // Empty means every class compatible with the declaration. Otherwise calls
    // must prove one of these static receiver types, including valid subclasses.
    public ImmutableArray<ObjectTypeId> ReceiverBounds { get; init; } = [];
}
public static class ObjectDispatchLimits
{
    public const int MaximumTargets = 65536;
}

/// <summary>Detached object operations and declaration bindings, validated before native lowering.</summary>
public sealed record ObjectIrProgram(ReferenceIrProgram Flow, ImmutableArray<ObjectMethodBinding> Bindings)
{
    public const string Profile = "object-operations-ir-v59";
    public ImmutableArray<ObjectAbstractMethodDeclaration> AbstractMethods { get; init; } = [];
    public ImmutableArray<ObjectVirtualDispatch> Dispatch { get; init; } = [];
    public ObjectInterfaceDispatchPlan? InterfaceDispatch { get; init; }
}

public sealed record ObjectTranslationResult(ObjectIrProgram? Program, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Program is not null;
}
