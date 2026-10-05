using System.Collections.Immutable;
using System.Linq;
using Cilica.Metadata;
using Cilica.Metadata.IL;

namespace Cilica.Compiler;

public enum IrTypeKind { Int32, Reference, NullReference, Void, Value, ManagedLocation, Int64 }

/// <summary>References and inline values retain distinct kinds and content-scoped identities; null is a stack value.</summary>
public sealed record IrType(IrTypeKind Kind, ObjectTypeId? Class = null, IrType? Element = null, bool ReadOnly = false)
{
    public static IrType Int32 { get; } = new(IrTypeKind.Int32);
    public static IrType Int64 { get; } = new(IrTypeKind.Int64);
    public static IrType NullReference { get; } = new(IrTypeKind.NullReference);
    public static IrType Void { get; } = new(IrTypeKind.Void);
    public static IrType Reference(ObjectTypeId type) => new(IrTypeKind.Reference, type);
    public static IrType Value(ObjectTypeId type) => new(IrTypeKind.Value, type);
    // A borrowed storage location, never an object identity or unmanaged pointer.
    public static IrType Location(IrType element, bool readOnly = false) => new(IrTypeKind.ManagedLocation, Element: element, ReadOnly: readOnly);
}

/// <summary>32-bit operations with explicit wrapping/checked arithmetic; shifts mask the count to five bits.</summary>
public enum Int32Operation
{
    AddWrapping, SubtractWrapping, MultiplyWrapping, BitwiseAnd, BitwiseOr, BitwiseXor,
    ShiftLeft, ShiftRightSigned, ShiftRightUnsigned,
    DivideSigned, DivideUnsigned,
    AddCheckedSigned, SubtractCheckedSigned, MultiplyCheckedSigned,
    AddCheckedUnsigned, SubtractCheckedUnsigned, MultiplyCheckedUnsigned,
    RemainderSigned, RemainderUnsigned,
}

public enum Int32UnaryOperation { BitwiseNot }

/// <summary>Current primitive profile propagates faults to the method's caller.</summary>
public enum IrExceptionTarget { MethodExit }
public enum Int32Fault { None = 0, DivideByZero = 1, Overflow = 2 }

public enum Int32Comparison
{
    Equal, NotEqual, LessSigned, GreaterSigned, LessOrEqualSigned, GreaterOrEqualSigned,
    LessUnsigned, GreaterUnsigned, LessOrEqualUnsigned, GreaterOrEqualUnsigned, Zero, Nonzero,
}

public abstract record IrExpression
{
    public sealed record Constant(int Value) : IrExpression;
    public sealed record Constant64(long Value) : IrExpression;
    public sealed record Binary64(Int64Operation Operation, int Left, int Right) : IrExpression;
    public sealed record Not64(int Operand) : IrExpression;
    public sealed record ConvertInteger(IntegerConversion Conversion, int Operand) : IrExpression;
    // Narrow results are normalized to the CIL Int32 evaluation stack. Source
    // signedness is explicit because .un checked conversions reinterpret bits.
    public sealed record IntegerCast(IntegerCastTarget Target, int Operand, bool Checked, bool UnsignedSource) : IrExpression;
    public sealed record Null : IrExpression;
    public sealed record Parameter(int Index) : IrExpression;
    public sealed record Local(int Index) : IrExpression;
    public sealed record StackInput(int Index) : IrExpression;
    public sealed record Binary(Int32Operation Operation, int Left, int Right) : IrExpression;
    public sealed record Unary(Int32UnaryOperation Operation, int Operand) : IrExpression;
    public sealed record Compare(Int32Comparison Comparison, int Left, int Right) : IrExpression;
    public sealed record Call(IrMethodId Target, ImmutableArray<int> Arguments, bool NullCheckReceiver = false,
        bool VirtualDispatch = false) : IrExpression;
    public sealed record NewException(ObjectTypeId Type, int? Message = null, int? ParameterName = null) : IrExpression;
    public sealed record ExceptionMessage(int Operand) : IrExpression;
    public sealed record ExceptionParameterName(int Operand) : IrExpression;
    public sealed record NewDelegate(ObjectTypeId Type, IrMethodId Target, int Receiver) : IrExpression;
    public sealed record CallDelegate(ObjectTypeId Type, int Delegate, ImmutableArray<int> Arguments) : IrExpression;
    public sealed record NewObject(IrMethodId Constructor, ImmutableArray<int> Arguments) : IrExpression;
    // Vector lengths/indices use bounded Int32 values. Allocation zero-initializes
    // elements; negative length and allocation failure leave via the fault edge.
    public sealed record NewArray(ObjectTypeId ArrayType, int Length) : IrExpression;
    public sealed record ArrayLength(int Array) : IrExpression;
    // ArrayType is the static view; access checks null before bounds.
    public sealed record LoadElement(ObjectTypeId ArrayType, int Array, int Index) : IrExpression;
    // UTF-16 code units, including embedded zero and unpaired surrogates. Equal
    // literal contents share instance-local identity; no host string encoding.
    public sealed record StringLiteral(ImmutableArray<ushort> CodeUnits) : IrExpression;
    public sealed record InvariantCulture(ObjectTypeId Type) : IrExpression;
    public sealed record StringLength(int Operand) : IrExpression;
    // Result is the zero-extended UTF-16 code unit, not a Unicode scalar value.
    public sealed record StringCodeUnit(int Operand, int Index) : IrExpression;
    // Ordinal content equality; null == null. Returns Int32 0 or 1.
    public sealed record StringEquals(int Left, int Right) : IrExpression;
    // Null is empty. Reuse the nonempty operand if the other is empty; otherwise
    // return a new immutable string, or the canonical empty string if both empty.
    public sealed record StringConcat(int Left, int Right) : IrExpression;
    public sealed record LoadField(ObjectFieldId Field, int Receiver) : IrExpression;
    public sealed record CastClass(ObjectTypeId Target, int Operand) : IrExpression;
    public sealed record IsInstance(ObjectTypeId Target, int Operand) : IrExpression;
    // Pure inline operations: copy the operand rather than alias mutable storage.
    public sealed record DefaultValue(ObjectTypeId Type) : IrExpression;
    public sealed record ValueField(ObjectFieldId Field, int Operand) : IrExpression;
    public sealed record WithValueField(ObjectFieldId Field, int Operand, int Value) : IrExpression;
    // Named struct boxing: fresh object identity, copying the complete payload.
    // The result has the selected Object view; no implicit value/reference conversion.
    public sealed record BoxValue(ObjectTypeId Type, int Operand) : IrExpression;
    // Both check null, then exact payload identity. Copy and mutable borrow differ:
    // UnboxValue returns an independent value; UnboxLocation retains the box owner.
    public sealed record UnboxValue(ObjectTypeId Type, int Operand) : IrExpression;
    public sealed record UnboxLocation(ObjectTypeId Type, int Operand) : IrExpression;
    // Explicit scalar boxing backend contract; accepted only with a validated
    // boxed payload catalog. CheckBox also retains empty-payload owner roots.
    public sealed record BoxScalars(ObjectTypeId Type, ImmutableArray<int> Values) : IrExpression;
    public sealed record CheckBox(ObjectTypeId Type, int Operand) : IrExpression;
    public sealed record AddressBoxSlot(ObjectTypeId Type, int Receiver, int Slot) : IrExpression;
    public sealed record AddressLocal(int Index) : IrExpression;
    public sealed record AddressParameter(int Index) : IrExpression;
    public sealed record AddressField(ObjectFieldId Field, int Receiver) : IrExpression;
    public sealed record LocationField(ObjectFieldId Field, int Location) : IrExpression;
    public sealed record ReadOnlyLocation(int Location) : IrExpression;
    public sealed record LoadLocation(int Location) : IrExpression;
}

/// <summary>Block-local typed temporary; operands always refer to earlier values in that block.</summary>
public sealed record IrValue(int Id, IrType Type, int ILOffset, IrExpression Expression);
public abstract record IrStatement
{
    public sealed record InvokeDelegate(int ILOffset, ObjectTypeId Type, int Delegate, ImmutableArray<int> Arguments,
        IrExceptionTarget Exceptional = IrExceptionTarget.MethodExit) : IrStatement;
    public sealed record Define(IrValue Value, IrExceptionTarget? Exceptional = null) : IrStatement;
    public sealed record StoreLocal(int ILOffset, int Local, int Value) : IrStatement;
    public sealed record Invoke(int ILOffset, IrMethodId Target, ImmutableArray<int> Arguments,
        bool NullCheckReceiver = false, IrExceptionTarget Exceptional = IrExceptionTarget.MethodExit,
        bool VirtualDispatch = false) : IrStatement;
    public sealed record StoreField(int ILOffset, ObjectFieldId Field, int Receiver, int Value,
        IrExceptionTarget Exceptional = IrExceptionTarget.MethodExit) : IrStatement;
    // Reference stores additionally check the allocation's actual element type,
    // after null/bounds checks; static covariance never removes that obligation.
    public sealed record StoreElement(int ILOffset, ObjectTypeId ArrayType, int Array, int Index, int Value,
        IrExceptionTarget Exceptional = IrExceptionTarget.MethodExit) : IrStatement;
    public sealed record StoreLocation(int ILOffset, int Location, int Value,
        IrExceptionTarget Exceptional = IrExceptionTarget.MethodExit) : IrStatement;
    public sealed record InitializeLocation(int ILOffset, int Location,
        IrExceptionTarget Exceptional = IrExceptionTarget.MethodExit) : IrStatement;
}

/// <summary>Arguments are immutable value IDs, ordered from stack bottom to top.</summary>
public sealed record IrEdge(int Target, ImmutableArray<int> Arguments);
public abstract record IrTerminator(int ILOffset);
public sealed record IrReturn(int ILOffset, int Value) : IrTerminator(ILOffset);
public sealed record IrVoidReturn(int ILOffset) : IrTerminator(ILOffset);
public sealed record IrThrow(int ILOffset, int Value) : IrTerminator(ILOffset);
public sealed record IrRethrow(int ILOffset) : IrTerminator(ILOffset);
public sealed record IrLeave(int ILOffset, IrEdge Edge) : IrTerminator(ILOffset);
public sealed record IrEndFinally(int ILOffset) : IrTerminator(ILOffset);
public sealed record IrEndFilter(int ILOffset, int Value) : IrTerminator(ILOffset);
public sealed record IrJump(int ILOffset, IrEdge Edge) : IrTerminator(ILOffset);
public sealed record IrBranch(int ILOffset, Int32Comparison Comparison, int Left, int? Right,
    IrEdge Taken, IrEdge NotTaken) : IrTerminator(ILOffset);

public sealed record IrBlock(int Id, int ILOffset, ImmutableArray<IrType> Inputs,
    ImmutableArray<IrStatement> Statements, IrTerminator Terminator)
{
    public ImmutableArray<IrValue> Values => Statements.IsDefault ? default :
        [.. Statements.OfType<IrStatement.Define>().Select(s => s.Value)];
}

/// <summary>Detached translation; no readers, assembly execution or generated native code.</summary>
public sealed record IrMethod(MethodSource Source, ImmutableArray<IrType> Parameters,
    IrType ReturnType, ImmutableArray<IrBlock> Blocks, ImmutableArray<IrType> Locals)
{
    public ImmutableArray<IrExceptionRegion> ExceptionRegions { get; init; } = [];
    // Kept for existing Int32 translation callers; reference contracts use ReferenceIrProgram.Profile.
    public const string Profile = IrProgram.Profile;
}

/// <summary>Aggregate bounds also cover synthetic stack inputs and edge copies.</summary>
public static class IrLimits
{
    public const int MaximumBlocks = 4096;
    public const int MaximumStatements = 65536;
    public const int MaximumInputs = 65536;
    public const int MaximumEdgeArguments = 65536;
    public const int MaximumParameters = 4096;
    public const int MaximumLocals = 4096;
}

/// <summary>A failed translation returns diagnostics and no consumable partial IR.</summary>
public sealed record TranslationResult(IrMethod? Method, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Method is not null;
}

/// <summary>Content-scoped MethodDef identity, independent of display names and readers.</summary>
public sealed record IrMethodId(string ContentHash, int Token, string Specialization = "")
{
    public static IrMethodId From(MethodSource source) =>
        new(source.ContentHash.ToLowerInvariant(), source.Token, source.Specialization);
}

/// <summary>One explicit entry and its complete reachable direct-call closure.</summary>
public sealed record IrProgram(IrMethodId Entry, ImmutableArray<IrMethod> Methods)
{
    public const string Profile = "int32-pending-fault-v1";
}

/// <summary>Typed value-flow contract; object declaration/access validation is supplied by ObjectIrProgram.</summary>
public sealed record ReferenceIrProgram(IrMethodId Entry, ImmutableArray<IrMethod> Methods, ObjectTypeCatalog Types)
{
    public const string Profile = "typed-reference-ir-v5";
}

/// <summary>Detached copy semantics; no CIL access checks, mutable managed locations or object operations.</summary>
public sealed record ValueIrProgram(IrMethodId Entry, ImmutableArray<IrMethod> Methods, ValueTypeCatalog Types)
{
    public const string Profile = "typed-value-ir-v1";
}

/// <summary>Detached synchronous borrows, with checked frame/heap origins; not native addresses or CIL lowering.</summary>
public sealed record ManagedLocationIrProgram(ValueIrProgram Flow)
{
    public const string Profile = "managed-location-ir-v1";
}

public static class ProgramLimits
{
    public const int MaximumMethods = 256;
    public const int MaximumBodyBytes = 1048576;
    public const int MaximumInstructions = 65536;
    public const int MaximumCallArguments = 65536;
    // Existing IR limits also apply to totals across all methods in a program.
}

public sealed record ProgramTranslationResult(IrProgram? Program, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Program is not null;
}
