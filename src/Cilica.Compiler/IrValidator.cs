using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Cilica.Compiler;

internal static class IrValidator
{
    internal static IrMethod Validate(IrProgram program, ObjectTypeCatalog? catalog = null, ObjectIrRules? objectRules = null,
        ValueIrRules? valueRules = null, ManagedLocationRules? locationRules = null, ScalarBoxingRules? boxedRules = null, ManagedExceptionRules? exceptions = null, bool integers64 = false)
    {
        void Require([DoesNotReturnIf(false)] bool condition, string message)
        {
            if (!condition) throw new ArgumentException((catalog is null ? "Invalid Int32 program IR: " : "Invalid reference program IR: ") + message, nameof(program));
        }
        Require(program.Entry is not null && !program.Methods.IsDefault && program.Methods.Length is > 0 and <= ProgramLimits.MaximumMethods,
            "entry and bounded methods are required.");
        var methods = new Dictionary<IrMethodId, IrMethod>();
        foreach (IrMethod method in program.Methods)
        {
            Require(method is not null && method.Source is not null, "method source is missing.");
            // Validate hash shape before deriving a native name/key.
            Require(method.Source.ContentHash is { Length: 64 } hash && hash.All(char.IsAsciiHexDigit), "invalid source hash.");
            Require(methods.TryAdd(IrMethodId.From(method.Source), method), "duplicate method identity/native symbol.");
        }
        Require(methods.TryGetValue(program.Entry, out IrMethod? entry), "entry is outside the program.");
        var resources = new ProgramResources();
        var types = new IrTypeRules(catalog, valueRules?.Catalog, locationRules is not null || valueRules?.Locations is not null, integers64);
        foreach (IrMethod method in program.Methods)
        {
            Validate(method, methods, catalog, objectRules, types, valueRules, locationRules, boxedRules, exceptions);
            Require(resources.Add(method), "aggregate program IR limit exceeded.");
        }
        var visited = new HashSet<IrMethodId>(); var pending = new Stack<IrMethodId>(); pending.Push(program.Entry);
        while (pending.TryPop(out IrMethodId? key))
            if (visited.Add(key))
                foreach (IrStatement statement in methods[key].Blocks.SelectMany(b => b.Statements))
                    switch (statement)
                    {
                        case IrStatement.Define { Value.Expression: IrExpression.Call call }:
                            if (objectRules is null || !objectRules.Interface(call.Target)) pending.Push(call.Target);
                            if (call.VirtualDispatch) foreach (var target in objectRules!.DispatchTargets(call.Target)) pending.Push(target);
                            break;
                        case IrStatement.Define { Value.Expression: IrExpression.NewObject allocated }: pending.Push(allocated.Constructor); break;
                        case IrStatement.Define { Value.Expression: IrExpression.NewDelegate capture }: pending.Push(capture.Target); break;
                        case IrStatement.Invoke call:
                            if (objectRules is null || !objectRules.Interface(call.Target)) pending.Push(call.Target);
                            if (call.VirtualDispatch) foreach (var target in objectRules!.DispatchTargets(call.Target)) pending.Push(target);
                            break;
                    }
        Require(visited.Count == methods.Count, "program contains methods unreachable from entry.");
        return entry;
    }

    internal static void Validate(IrMethod method, IReadOnlyDictionary<IrMethodId, IrMethod>? methods = null, ObjectTypeCatalog? catalog = null,
        ObjectIrRules? objectRules = null, IrTypeRules? sharedTypes = null, ValueIrRules? valueRules = null, ManagedLocationRules? locationRules = null,
        ScalarBoxingRules? boxedRules = null, ManagedExceptionRules? exceptions = null)
    {
        int locationOffset = 0;
        void Require([DoesNotReturnIf(false)] bool condition, string message)
        {
            if (condition) return;
            string origin = objectRules is null ? "" : " " + method.Source?.Name + " token 0x" +
                method.Source?.Token.ToString("x8", System.Globalization.CultureInfo.InvariantCulture) + " IL_" + locationOffset.ToString("x4", System.Globalization.CultureInfo.InvariantCulture);
            if (!condition) throw new ArgumentException((catalog is null ? "Invalid Int32 IR: " : "Invalid reference IR: ") + message + origin, nameof(method));
        }
        var types = sharedTypes ?? new IrTypeRules(catalog);
        var locations = locationRules ?? valueRules?.Locations;
        Require(method.Source is not null, "source is missing.");
        Require(method.Source.AssemblyIdentity is not null, "assembly identity is missing.");
        Require(method.Source.ContentHash is { Length: 64 } hash && hash.All(char.IsAsciiHexDigit), "source hash must be SHA-256 hexadecimal.");
        Require(method.Source.Specialization is { } key && (key.Length == 0 || key.Length == 64 && key.All(c => char.IsAsciiDigit(c) || c is >= 'a' and <= 'f')),
            "specialization key must be empty or lowercase SHA-256 hexadecimal.");
        Require((uint)method.Source.Token >> 24 == 0x06 && (method.Source.Token & 0xffffff) != 0, "source needs a non-nil MethodDef token.");
        Require(!method.Parameters.IsDefault && method.Parameters.Length <= IrLimits.MaximumParameters && method.Parameters.All(p => types.Valid(p, storage: true))
            && method.ReturnType?.Kind != IrTypeKind.ManagedLocation
            && (types.Valid(method.ReturnType, storage: true) || (objectRules is not null || locations is not null) && method.ReturnType == IrType.Void), "signature is outside the bounded value profile.");
        Require(!method.Locals.IsDefault && method.Locals.Length <= IrLimits.MaximumLocals
            && method.Locals.All(l => types.Valid(l, storage: true) && l.Kind != IrTypeKind.ManagedLocation), "locals must be bounded typed storage; locations cannot escape into locals.");
        Require(!method.Blocks.IsDefault && method.Blocks.Length is > 0 and <= IrLimits.MaximumBlocks, "invalid block count.");
        int inputs = 0, statements = 0, edgeArguments = 0, callArguments = 0, previousBlockOffset = -1;
        for (int id = 0; id < method.Blocks.Length; id++)
        {
            IrBlock block = method.Blocks[id];
            Require(block is not null && block.Id == id && block.ILOffset > previousBlockOffset, "blocks need consecutive IDs and increasing IL offsets.");
            Require(!block.Inputs.IsDefault && block.Inputs.Length <= IrLimits.MaximumInputs
                && block.Inputs.All(t => types.Valid(t, storage: false) && t.Kind != IrTypeKind.ManagedLocation), "invalid block inputs; location joins require later lifetime analysis.");
            inputs += block.Inputs.Length;
            Require(inputs <= IrLimits.MaximumInputs, "aggregate block-input limit exceeded.");
            previousBlockOffset = block.ILOffset;
        }
        Require(method.Blocks[0].ILOffset == 0 && method.Blocks[0].Inputs.IsEmpty, "entry block starts at zero with an empty stack.");
        Require(!method.ExceptionRegions.IsDefault && (exceptions is not null || method.ExceptionRegions.IsEmpty), "exception regions require the managed-exception profile.");
        exceptions?.ValidateRegions(method);
        if (exceptions is not null) foreach (var region in method.ExceptionRegions)
            if (region.CatchType is { } caught) Require(objectRules?.TypeTest(method, caught) == true, "catch type is inaccessible.");
        var successors = new List<int>[method.Blocks.Length];
        foreach (IrBlock block in method.Blocks)
        {
            var targets = new List<int>(); successors[block.Id] = targets;
            Require(!block.Statements.IsDefault && block.Statements.Length <= IrLimits.MaximumStatements, "invalid statement array.");
            statements += block.Statements.Length;
            Require(statements <= IrLimits.MaximumStatements, "aggregate statement limit exceeded.");
            var valueTypes = new List<IrType>();
            var expressions = new List<IrExpression>();
            int values = 0, previousOffset = block.ILOffset;
            int end = block.Id + 1 < method.Blocks.Length ? method.Blocks[block.Id + 1].ILOffset : int.MaxValue;
            bool Earlier(int value) => value >= 0 && value < values;
            bool Local(int local) => local >= 0 && local < method.Locals.Length;
            bool Call(IrExpression.Call call, IrType result)
            {
                if (call.Target is null || call.Arguments.IsDefault || call.Arguments.Length > IrLimits.MaximumParameters || !call.Arguments.All(Earlier)) return false;
                if (objectRules is not null) return result == objectRules.ReturnType(call.Target)
                    && objectRules.Call(method, call.Target, call.Arguments, call.NullCheckReceiver, valueTypes, virtualDispatch: call.VirtualDispatch, expressions: expressions);
                return methods is not null && methods.TryGetValue(call.Target, out var target) && !target.Parameters.IsDefault
                    && call.Arguments.Length == target.Parameters.Length && result == target.ReturnType && !call.NullCheckReceiver && !call.VirtualDispatch
                    && call.Arguments.Select((argument, index) => types.Assignable(valueTypes[argument], target.Parameters[index])).All(valid => valid);
            }
            bool TypeTest(ObjectTypeId target, int operand, IrType result) => objectRules is not null
                && objectRules.TypeTest(method, target) && result == IrType.Reference(target)
                && Earlier(operand) && valueTypes[operand].Kind is IrTypeKind.Reference or IrTypeKind.NullReference;
            void Offset(int offset)
            {
                Require(offset >= previousOffset && offset < end, "statement/terminator offset is outside its block or out of order.");
                previousOffset = offset; locationOffset = offset;
            }
            foreach (IrStatement statement in block.Statements)
            {
                switch (statement)
                {
                    case IrStatement.Define define:
                        IrValue value = define.Value;
                        Require(value is not null && value.Id == values && types.Valid(value.Type, storage: false), "values need consecutive IDs and valid types.");
                        Offset(value.ILOffset);
                        Require(value.Expression switch
                        {
                            IrExpression.NewException creation => exceptions is not null && exceptions.Creation(creation, value.Type, valueTypes) && objectRules?.TypeTest(method, creation.Type) == true,
                            IrExpression.ExceptionInnerException inner => exceptions is not null && exceptions.InnerException(inner, value.Type, valueTypes),
                            IrExpression.ExceptionTypeName name => exceptions is not null && exceptions.TypeName(name, value.Type, valueTypes),
                            IrExpression.ExceptionParameterName parameter => exceptions is not null && exceptions.ParameterName(parameter, value.Type, valueTypes),
                            IrExpression.ExceptionMessage message => exceptions is not null && exceptions.Message(message, value.Type, valueTypes),
                            IrExpression.Constant => value.Type == IrType.Int32,
                            IrExpression.Constant64 => value.Type == IrType.Int64,
                            IrExpression.Binary64 b => value.Type == IrType.Int64 && Enum.IsDefined(b.Operation)
                                && Earlier(b.Left) && Earlier(b.Right) && valueTypes[b.Left] == IrType.Int64
                                && valueTypes[b.Right] == (Int64Operations.Shift(b.Operation) ? IrType.Int32 : IrType.Int64),
                            IrExpression.Not64 n => value.Type == IrType.Int64 && Earlier(n.Operand) && valueTypes[n.Operand] == IrType.Int64,
                            IrExpression.ConvertInteger c => Earlier(c.Operand) && Int64Operations.Convert(c.Conversion, valueTypes[c.Operand], value.Type),
                            IrExpression.IntegerCast c => types.IntegerCasts && Enum.IsDefined(c.Target) && Earlier(c.Operand)
                                && valueTypes[c.Operand] is var castInput && (castInput == IrType.Int32 || castInput == IrType.Int64)
                                && value.Type == IntegerCasts.Result(c.Target),
                            IrExpression.Null => catalog is not null && value.Type == IrType.NullReference,
                            IrExpression.Parameter p => p.Index >= 0 && p.Index < method.Parameters.Length && value.Type == method.Parameters[p.Index],
                            IrExpression.Local l => Local(l.Index) && value.Type == method.Locals[l.Index],
                            IrExpression.StackInput s => s.Index >= 0 && s.Index < block.Inputs.Length && value.Type == block.Inputs[s.Index],
                            IrExpression.Binary b => value.Type == IrType.Int32 && (b.Operation is Int32Operation.AddWrapping or Int32Operation.SubtractWrapping or Int32Operation.MultiplyWrapping
                                or Int32Operation.BitwiseAnd or Int32Operation.BitwiseOr or Int32Operation.BitwiseXor
                                or Int32Operation.ShiftLeft or Int32Operation.ShiftRightSigned or Int32Operation.ShiftRightUnsigned
                                or Int32Operation.DivideSigned or Int32Operation.DivideUnsigned
                                or Int32Operation.RemainderSigned or Int32Operation.RemainderUnsigned
                                or Int32Operation.AddCheckedSigned or Int32Operation.SubtractCheckedSigned or Int32Operation.MultiplyCheckedSigned
                                or Int32Operation.AddCheckedUnsigned or Int32Operation.SubtractCheckedUnsigned or Int32Operation.MultiplyCheckedUnsigned)
                                && Earlier(b.Left) && Earlier(b.Right) && valueTypes[b.Left] == IrType.Int32 && valueTypes[b.Right] == IrType.Int32,
                            IrExpression.Unary u => value.Type == IrType.Int32 && u.Operation == Int32UnaryOperation.BitwiseNot && Earlier(u.Operand) && valueTypes[u.Operand] == IrType.Int32,
                            IrExpression.Compare c => value.Type == IrType.Int32 && BinaryComparison(c.Comparison) && Earlier(c.Left) && Earlier(c.Right)
                                && types.Comparable(c.Comparison, valueTypes[c.Left], valueTypes[c.Right]),
                            IrExpression.Call c => Call(c, value.Type),
                            IrExpression.NewDelegate d => objectRules is not null && objectRules.DelegateCapture(method, d, value.Type, valueTypes),
                            IrExpression.CallDelegate d => objectRules is not null && objectRules.DelegateCall(method, d.Type, d.Delegate, d.Arguments, value.Type, valueTypes),
                            IrExpression.NewObject n => objectRules is not null && value.Type == objectRules.AllocationType(n.Constructor)
                                && objectRules.Call(method, n.Constructor, n.Arguments, false, valueTypes, allocation: true),
                            IrExpression.IntegerTextFind cached => types.IntegerCasts && objectRules?.StringType == value.Type
                                && Earlier(cached.Index) && valueTypes[cached.Index] == IrType.Int32,
                            IrExpression.IntegerTextPublish cached => types.IntegerCasts && objectRules?.StringType == value.Type
                                && Earlier(cached.Index) && valueTypes[cached.Index] == IrType.Int32 && objectRules.StringOperand(cached.Text, valueTypes),
                            IrExpression.InvariantCulture culture => objectRules is not null && objectRules.InvariantCulture(culture.Type, value.Type),
                            IrExpression.StringLiteral literal => objectRules is not null && value.Type == objectRules.StringType
                                && objectRules.StringLiteral(literal.CodeUnits),
                            IrExpression.StringLength text => objectRules is not null && value.Type == IrType.Int32
                                && objectRules.StringOperand(text.Operand, valueTypes),
                            IrExpression.StringCodeUnit text => objectRules is not null && value.Type == IrType.Int32
                                && objectRules.StringOperand(text.Operand, valueTypes) && Earlier(text.Index) && valueTypes[text.Index] == IrType.Int32,
                            IrExpression.StringEquals text => objectRules is not null && value.Type == IrType.Int32
                                && objectRules.StringOperand(text.Left, valueTypes) && objectRules.StringOperand(text.Right, valueTypes),
                            IrExpression.StringFromCodeUnits text => types.IntegerCasts && objectRules is not null && value.Type == objectRules.StringType
                                && Earlier(text.Array) && types.Assignable(valueTypes[text.Array], IrType.Reference(ObjectTypeId.Vector(catalog!.ObjectBase, ObjectArrayElement.Int32))),
                            IrExpression.StringConcat text => objectRules is not null && value.Type == objectRules.StringType
                                && objectRules.StringOperand(text.Left, valueTypes) && objectRules.StringOperand(text.Right, valueTypes),
                            IrExpression.EmptyArray a => types.IntegerCasts && objectRules is not null && objectRules.ArrayAllocation(method, a.ArrayType)
                                && value.Type == IrType.Reference(a.ArrayType),
                            IrExpression.NewArray a => objectRules is not null && objectRules.ArrayAllocation(method, a.ArrayType)
                                && value.Type == IrType.Reference(a.ArrayType) && Earlier(a.Length) && valueTypes[a.Length] == IrType.Int32,
                            IrExpression.ArrayLength a => objectRules is not null && value.Type == IrType.Int32
                                && objectRules.ArrayLength(a.Array, valueTypes),
                            IrExpression.LoadElement a => objectRules is not null && objectRules.Element(a.ArrayType, a.Array, a.Index, valueTypes)
                                && value.Type == objectRules.ArrayElement(a.ArrayType),
                            IrExpression.LoadStatic f => objectRules is not null && objectRules.StaticField(method, f.Field, false, out var staticType) && value.Type == staticType,
                            IrExpression.LoadField f => objectRules is not null && objectRules.Field(method, f.Field, f.Receiver, valueTypes, out var storage)
                                && value.Type == storage,
                            IrExpression.CastClass c => TypeTest(c.Target, c.Operand, value.Type),
                            IrExpression.IsInstance t => TypeTest(t.Target, t.Operand, value.Type),
                            IrExpression.DefaultValue d => valueRules is not null && value.Type == IrType.Value(d.Type),
                            IrExpression.BoxValue b => valueRules is not null && Earlier(b.Operand)
                                && valueRules.Box(b.Type, valueTypes[b.Operand], value.Type),
                            IrExpression.BoxScalars b => boxedRules is not null && boxedRules.Box(b, value.Type, valueTypes),
                            IrExpression.CheckBox b => boxedRules is not null && boxedRules.Check(b.Type, b.Operand, value.Type, valueTypes),
                            IrExpression.AddressBoxSlot b => boxedRules is not null && boxedRules.Slot(b, value.Type, valueTypes),
                            IrExpression.UnboxValue u => valueRules is not null && Earlier(u.Operand)
                                && valueRules.Unbox(u.Type, valueTypes[u.Operand], value.Type, borrow: false),
                            IrExpression.UnboxLocation u => valueRules is not null && Earlier(u.Operand)
                                && valueRules.Unbox(u.Type, valueTypes[u.Operand], value.Type, borrow: true),
                            IrExpression.ValueField f => valueRules is not null && Earlier(f.Operand)
                                && valueRules.Field(valueTypes[f.Operand], f.Field) == value.Type,
                            IrExpression.WithValueField f => valueRules is not null && Earlier(f.Operand) && Earlier(f.Value)
                                && value.Type == valueTypes[f.Operand] && valueRules.Field(value.Type, f.Field) is { } fieldType
                                && types.Assignable(valueTypes[f.Value], fieldType),
                            IrExpression.AddressLocal a => locations is not null && Local(a.Index)
                                && ManagedLocationRules.Address(method.Locals[a.Index], value.Type),
                            IrExpression.AddressParameter a => locations is not null && a.Index >= 0 && a.Index < method.Parameters.Length
                                && ManagedLocationRules.Address(method.Parameters[a.Index], value.Type),
                            IrExpression.AddressField f => locations is not null && Earlier(f.Receiver)
                                && locations.HeapField(valueTypes[f.Receiver], f.Field, value.Type)
                                && (objectRules is null || objectRules.Field(method, f.Field, f.Receiver, valueTypes, out _)),
                            IrExpression.LocationField f => locations is not null && Earlier(f.Location)
                                && locations.NestedField(valueTypes[f.Location], f.Field, value.Type,
                                    objectRules?.InitializesValueField(method, f.Field, expressions[f.Location]) == true),
                            IrExpression.ReadOnlyLocation view => locations is not null && Earlier(view.Location)
                                && value.Type.Kind == IrTypeKind.ManagedLocation && value.Type.ReadOnly
                                && valueTypes[view.Location].Kind == IrTypeKind.ManagedLocation
                                && valueTypes[view.Location].Element == value.Type.Element,
                            IrExpression.LoadLocation l => locations is not null && Earlier(l.Location)
                                && ManagedLocationRules.Load(valueTypes[l.Location], value.Type),
                            _ => false,
                        }, "expression, parameter or operand is invalid.");
                        bool throwing = value.Expression is IrExpression.NewException or IrExpression.ExceptionMessage or IrExpression.ExceptionInnerException or IrExpression.ExceptionTypeName or IrExpression.ExceptionParameterName or IrExpression.NewDelegate or IrExpression.CallDelegate or IrExpression.Call or IrExpression.NewObject or IrExpression.LoadStatic or IrExpression.LoadField
                            or IrExpression.StringLiteral or IrExpression.InvariantCulture or IrExpression.IntegerTextPublish or IrExpression.IntegerTextFind or IrExpression.StringLength or IrExpression.StringCodeUnit or IrExpression.StringEquals or IrExpression.StringFromCodeUnits or IrExpression.StringConcat
                            or IrExpression.CastClass or IrExpression.IsInstance or IrExpression.NewArray or IrExpression.EmptyArray or IrExpression.ArrayLength or IrExpression.LoadElement
                            or IrExpression.AddressField or IrExpression.LoadLocation
                            or IrExpression.BoxValue or IrExpression.UnboxValue or IrExpression.UnboxLocation
                            or IrExpression.BoxScalars or IrExpression.CheckBox or IrExpression.AddressBoxSlot ||
                            value.Expression is IrExpression.Binary binary && Int32Operations.MayFault(binary.Operation)
                            || value.Expression is IrExpression.Binary64 wide && Int64Operations.MayFault(wide.Operation)
                            || value.Expression is IrExpression.IntegerCast { Checked: true };
                        Require(define.Exceptional == (throwing ? IrExceptionTarget.MethodExit : null),
                            "throwing definitions need an explicit method-exit edge; pure definitions cannot have one.");
                        if (value.Expression is IrExpression.Call call)
                        {
                            callArguments += call.Arguments.Length;
                            Require(callArguments <= ProgramLimits.MaximumCallArguments, "call-argument limit exceeded.");
                        }
                        if (value.Expression is IrExpression.NewObject allocated)
                        {
                            callArguments += allocated.Arguments.Length;
                            Require(callArguments <= ProgramLimits.MaximumCallArguments, "call-argument limit exceeded.");
                        }
                        if (value.Expression is IrExpression.NewDelegate or IrExpression.CallDelegate)
                        {
                            callArguments += value.Expression is IrExpression.CallDelegate invoked ? invoked.Arguments.Length + 1 : 1;
                            Require(callArguments <= ProgramLimits.MaximumCallArguments, "delegate argument limit exceeded.");
                        }
                        if (value.Expression is IrExpression.BoxScalars boxed)
                        {
                            callArguments += boxed.Values.Length;
                            Require(callArguments <= ProgramLimits.MaximumCallArguments, "boxed packet argument limit exceeded.");
                        }
                        valueTypes.Add(value.Type); expressions.Add(value.Expression); values++;
                        break;
                    case IrStatement.StoreLocal store:
                        Offset(store.ILOffset);
                        Require(Local(store.Local) && Earlier(store.Value) && types.Assignable(valueTypes[store.Value], method.Locals[store.Local]), "invalid local store."); break;
                    case IrStatement.Invoke invoke:
                        Offset(invoke.ILOffset);
                        Require(invoke.Exceptional == IrExceptionTarget.MethodExit
                            && invoke.Target is not null
                            && !invoke.Arguments.IsDefault && invoke.Arguments.Length <= IrLimits.MaximumParameters
                            && (Call(new(invoke.Target, invoke.Arguments, invoke.NullCheckReceiver, invoke.VirtualDispatch), IrType.Void)
                                || objectRules is not null && objectRules.BaseCall(method, invoke, valueTypes, expressions)), "invalid void call or base initialization.");
                        callArguments += invoke.Arguments.Length;
                        Require(callArguments <= ProgramLimits.MaximumCallArguments, "call-argument limit exceeded."); break;
                    case IrStatement.InvokeDelegate invoke:
                        Offset(invoke.ILOffset);
                        Require(invoke.Exceptional == IrExceptionTarget.MethodExit && objectRules is not null
                            && objectRules.DelegateCall(method, invoke.Type, invoke.Delegate, invoke.Arguments, IrType.Void, valueTypes), "invalid void delegate invocation.");
                        callArguments += invoke.Arguments.Length + 1;
                        Require(callArguments <= ProgramLimits.MaximumCallArguments, "delegate argument limit exceeded."); break;
                    case IrStatement.StoreStatic store:
                        Offset(store.ILOffset);
                        Require(objectRules is not null && objectRules.StaticStore(method, store, valueTypes), "invalid static field store, access or fault edge."); break;
                    case IrStatement.StoreField store:
                        Offset(store.ILOffset);
                        Require(objectRules is not null && objectRules.Store(method, store, valueTypes, expressions), "invalid field store, access or readonly receiver."); break;
                    case IrStatement.StoreElement store:
                        Offset(store.ILOffset);
                        Require(objectRules is not null && objectRules.StoreElement(store, valueTypes), "invalid array element store or missing fault edge."); break;
                    case IrStatement.StoreLocation store:
                        Offset(store.ILOffset);
                        Require(locations is not null && store.Exceptional == IrExceptionTarget.MethodExit
                            && Earlier(store.Location) && Earlier(store.Value) && locations.Store(valueTypes[store.Location], valueTypes[store.Value]),
                            "invalid location store, readonly borrow or missing fault edge."); break;
                    case IrStatement.InitializeLocation initialize:
                        Offset(initialize.ILOffset);
                        Require(locations is not null && initialize.Exceptional == IrExceptionTarget.MethodExit
                            && Earlier(initialize.Location) && ManagedLocationRules.Writable(valueTypes[initialize.Location]),
                            "invalid location initialization, readonly borrow or missing fault edge."); break;
                    default: Require(false, "missing or unknown statement."); break;
                }
            }
            Require(block.Terminator is not null, "missing block terminator."); Offset(block.Terminator.ILOffset);
            switch (block.Terminator)
            {
                case IrThrow thrown:
                    Require(exceptions is not null && Earlier(thrown.Value) && exceptions.Throwable(valueTypes[thrown.Value]), "throw needs a managed exception reference or null."); break;
                case IrRethrow:
                    Require(exceptions is not null && exceptions.CatchAt(method, block.Id) >= 0, "rethrow requires an active catch handler."); break;
                case IrLeave leave:
                    Require(exceptions is not null && leave.Edge is not null && leave.Edge.Arguments.IsEmpty, "leave requires an empty-stack exceptional profile edge."); Edge(leave.Edge); break;
                case IrEndFinally:
                    Require(exceptions is not null && exceptions.CleanupAt(method, block.Id) >= 0, "endfinally requires an active cleanup handler."); break;
                case IrEndFilter ended:
                    Require(exceptions is not null && exceptions.FilterAt(method, block.Id) >= 0 && Earlier(ended.Value) && valueTypes[ended.Value] == IrType.Int32, "endfilter requires an Int32 result inside a filter."); break;
                case IrReturn returned: Require(Earlier(returned.Value) && types.Assignable(valueTypes[returned.Value], method.ReturnType), "invalid returned value."); break;
                case IrVoidReturn: Require((objectRules is not null || locations is not null) && method.ReturnType == IrType.Void, "invalid void return."); break;
                case IrJump jump: Edge(jump.Edge); break;
                case IrBranch branch:
                    Require(Earlier(branch.Left) && (branch.Comparison is Int32Comparison.Zero or Int32Comparison.Nonzero
                        ? branch.Right is null && IrTypeRules.TruthTest(valueTypes[branch.Left]) : BinaryComparison(branch.Comparison) && branch.Right is int right && Earlier(right) && types.Comparable(branch.Comparison, valueTypes[branch.Left], valueTypes[right])), "invalid branch condition.");
                    Edge(branch.Taken); Edge(branch.NotTaken); break;
                default: Require(false, "unknown block terminator."); break;
            }
            void Edge(IrEdge edge)
            {
                Require(edge is not null && edge.Target >= 0 && edge.Target < method.Blocks.Length, "invalid edge target.");
                Require(!edge.Arguments.IsDefault && edge.Arguments.Length == method.Blocks[edge.Target].Inputs.Length
                    && edge.Arguments.All(Earlier) && edge.Arguments.Select((argument, index) => types.Assignable(valueTypes[argument], method.Blocks[edge.Target].Inputs[index])).All(ok => ok), "edge arguments must match the target's typed inputs.");
                edgeArguments += edge.Arguments.Length;
                Require(edgeArguments <= IrLimits.MaximumEdgeArguments, "aggregate edge-argument limit exceeded.");
                targets.Add(edge.Target);
            }
        }
        var visited = new HashSet<int>(); var pending = new Stack<int>(); pending.Push(0);
        if (exceptions is not null) foreach (var region in method.ExceptionRegions)
        { pending.Push(region.HandlerStart); if (region.FilterStart is int filter) pending.Push(filter); }
        while (pending.TryPop(out int id))
            if (visited.Add(id)) foreach (int target in successors[id]) pending.Push(target);
        Require(visited.Count == method.Blocks.Length, "all blocks must be reachable from entry.");
        exceptions?.ValidateFlow(method);
        objectRules?.Method(method);
    }

    internal static bool BinaryComparison(Int32Comparison comparison) => (uint)comparison < (uint)Int32Comparison.Zero;
}
