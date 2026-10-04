using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Cilica.Compiler;

/// <summary>Emits the bounded typed object profile; references never cross its Int32 C boundary.</summary>
public static class ObjectCppEmitter
{
    // Small native helpers also bound unoptimized register spills and sanitizer
    // instrumentation. Inlining them would undo that bound; measure per target.
    private const int StatementsPerStep = 2;
    private const int CopiesPerStep = 4;
    public const string Profile = "object-frames-native-v14";

    public static CppArtifact EmitProgram(ObjectIrProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(program.Flow);
        ArgumentNullException.ThrowIfNull(program.Flow.Types);
        var plans = ObjectRootAssignment.Assign(program).ToDictionary(p => p.Method);
        var methods = program.Flow.Methods.ToDictionary(m => IrMethodId.From(m.Source));
        var signatures = methods.ToDictionary(p => p.Key, p => (p.Value.Parameters, p.Value.ReturnType));
        if (program.InterfaceDispatch is { } interfacePlan)
            foreach (var declaration in interfacePlan.Declarations)
                signatures.Add(IrMethodId.From(declaration.Source), (declaration.Parameters, declaration.ReturnType));
        var bindings = program.Bindings.ToDictionary(b => b.Method);
        var native = new ObjectNativeLayout(program.Flow.Types);
        var types = program.Flow.Types.Types.ToDictionary(t => ObjectTypeId.From(t.Source));
        var fields = program.Flow.Types.Types.SelectMany(t => t.Fields).ToDictionary(f => f.Id);
        var typeRules = new IrTypeRules(program.Flow.Types);
        var contracts = program.Flow.Types.Interfaces.Select(i => ObjectTypeId.From(i.Source)).ToHashSet();
        var arrays = program.Flow.Types.Arrays;
        var concrete = types.Keys.Concat(arrays).Concat(program.Flow.Types.String is { } selectedString ? [selectedString] : Array.Empty<ObjectTypeId>()).ToArray();
        // Empty has a reserved identity even when produced only by Concat(null, null).
        var literals = new Dictionary<string, ImmutableArray<ushort>>(StringComparer.Ordinal) { [""] = [] };
        string LiteralKey(ImmutableArray<ushort> units) => new(units.Select(u => (char)u).ToArray());
        foreach (var literal in methods.Values.SelectMany(m => m.Blocks).SelectMany(b => b.Values).Select(v => v.Expression).OfType<IrExpression.StringLiteral>())
            literals.TryAdd(LiteralKey(literal.CodeUnits), literal.CodeUnits);
        var literalIds = literals.Keys.Select((key, index) => (key, index)).ToDictionary(p => p.key, p => p.index, StringComparer.Ordinal);
        var referenceTypes = concrete.Concat(contracts).ToArray();
        string Predicate(ObjectTypeId type) => contracts.Contains(type) ? ContractSymbol(type) : TypeSymbol(type) + "_accepts";
        var layouts = methods.ToDictionary(p => p.Key, p => new ObjectFrameLayout(p.Value, plans[p.Key]));
        var entry = methods[program.Flow.Entry];
        string symbol = CppEmitter.Symbol(program.Flow.Entry) + "_objects_packed", scope = symbol + "_closure";
        var text = new StringBuilder();
        text.Append("// Cilica ").Append(Profile).Append("\n#include <array>\n#include <atomic>\n#include <bit>\n#include <cstddef>\n#include <cstdint>\n#include <new>\n#include <limits>\n#include <type_traits>\n")
            .Append(ObjectCppRuntime.DiagnosticIncludes).Append("namespace ")
            .Append(scope).Append(" {\n").Append(ObjectCppRuntime.Text).Append('\n').Append(ObjectCppTracing.Text).Append('\n').Append(ObjectCppMarking.Text).Append('\n').Append(ObjectCppHandles.Text).Append('\n');
        foreach (var type in referenceTypes)
            text.Append("[[maybe_unused, gnu::noinline]] static bool ").Append(Predicate(type)).Append("(const Descriptor*) noexcept;\n");
        foreach (var composed in native.Fields)
        {
            var layout = types[composed.Key];
            string type = TypeSymbol(composed.Key);
            string Member(ObjectField field) => field.Id.Owner == composed.Key ? FieldSymbol(field.Id)
                : TypeSymbol(field.Id.Owner) + "_" + FieldSymbol(field.Id);
            string Offset(ObjectField field) => "offsetof(" + type + ", " + Member(field) + ")";
            var references = composed.Value.Where(f => f.Storage is ObjectStorage.Reference).ToArray();
            text.Append("struct ").Append(type).Append(" { Object header;\n");
            foreach (var field in composed.Value)
                text.Append("    ").Append(field.Storage is ObjectStorage.Int32 ? "std::uint32_t" : "Object*").Append(' ').Append(Member(field)).Append(";\n");
            text.Append("};\nstatic_assert(std::is_standard_layout_v<").Append(type).Append("> && std::is_trivially_destructible_v<")
                .Append(type).Append("> && offsetof(").Append(type).Append(", header) == 0);\n")
                .Append("static constexpr std::array<std::size_t, ").Append(N(references.Length)).Append("> ").Append(type).Append("_references = {")
                .Append(string.Join(", ", references.Select(Offset))).Append("};\n")
                .Append("static constexpr std::array<std::size_t, ").Append(N(composed.Value.Length)).Append("> ").Append(type).Append("_fields = {")
                .Append(string.Join(", ", composed.Value.Select(Offset))).Append("};\n");
            // Typed member expressions preserve subobject provenance under C++20;
            // offsets select members, rather than manufacture pointers with byte arithmetic.
            for (int index = 0; index < composed.Value.Length; index++)
                text.Append("[[maybe_unused, gnu::noinline]] static void* ").Append(type).Append("_field_").Append(N(index))
                    .Append("(Object* p) noexcept { return &reinterpret_cast<").Append(type).Append("*>(p)->")
                    .Append(Member(composed.Value[index])).Append("; }\n");
            text.Append("static constexpr std::array<FieldAccessor, ").Append(N(composed.Value.Length)).Append("> ").Append(type).Append("_accessors = {")
                .Append(string.Join(", ", Enumerable.Range(0, composed.Value.Length).Select(i => type + "_field_" + N(i)))).Append("};\n")
                .Append("[[maybe_unused]] static constexpr Descriptor ").Append(type).Append("_descriptor = { sizeof(").Append(type).Append("), ")
                .Append("alignof(").Append(type).Append("), ").Append(type).Append("_references.data(), ").Append(N(references.Length))
                .Append(", [](Object* p) noexcept { delete reinterpret_cast<").Append(type).Append("*>(p); }, ")
                .Append(layout.BaseType is null ? "nullptr" : "&" + TypeSymbol(layout.BaseType) + "_descriptor")
                .Append(", ").Append(type).Append("_fields.data(), ").Append(N(composed.Value.Length))
                .Append(", ").Append(type).Append("_accessors.data(), field_by_offset };\n");
        }
        if (program.Flow.Types.String is { } stringType)
        {
            text.Append("[[maybe_unused]] static constexpr Descriptor ").Append(TypeSymbol(stringType)).Append("_descriptor = { sizeof(StringObject), alignof(StringObject), nullptr, 0, destroy_string, &")
                .Append(TypeSymbol(program.Flow.Types.ObjectBase)).Append("_descriptor, nullptr, 0, nullptr, nullptr };\n");
            foreach (var literal in literals)
                text.Append("[[maybe_unused]] static constexpr std::array<std::uint16_t, ").Append(N(literal.Value.Length)).Append("> literal_")
                    .Append(N(literalIds[literal.Key])).Append(" = {").Append(string.Join(", ", literal.Value.Select(u => N(u) + "u"))).Append("};\n");
        }
        foreach (var array in arrays)
        {
            bool reference = array.ArrayElement!.Kind == ObjectArrayElementKind.Reference;
            text.Append("[[maybe_unused]] static constexpr Descriptor ").Append(TypeSymbol(array)).Append("_descriptor = { sizeof(ArrayObject), alignof(ArrayObject), nullptr, 0, destroy_array, &")
                .Append(TypeSymbol(program.Flow.Types.ObjectBase)).Append("_descriptor, nullptr, 0, nullptr, nullptr, ")
                .Append(reference ? "2, " + Predicate(array.ArrayElement.Type!) + ", array_reference_count, array_references" : "1, nullptr, nullptr, nullptr").Append(" };\n");
        }
        // Exact membership compares untrusted descriptor addresses without dereferencing them.
        // This covers nominal/interface ancestry, vector covariance and widening to Object.
        foreach (var expected in referenceTypes)
        {
            string name = Predicate(expected);
            var receivers = concrete.Where(t => typeRules.Assignable(IrType.Reference(t), IrType.Reference(expected))).ToArray();
            text.Append("static constexpr std::array<const Descriptor*, ").Append(N(receivers.Length)).Append("> ")
                .Append(name).Append("_types = {").Append(string.Join(", ", receivers.Select(t => "&" + TypeSymbol(t) + "_descriptor"))).Append("};\n")
                .Append("[[maybe_unused, gnu::noinline]] static bool ").Append(name).Append("(const Descriptor* type) noexcept {\n")
                .Append("    for (const auto* receiver : ").Append(name).Append("_types) if (receiver == type) return true;\n    return false;\n}\n");
        }
        text.Append("[[maybe_unused]] static bool known_type(const Descriptor* type) noexcept { return ")
            .Append(Predicate(program.Flow.Types.ObjectBase)).Append("(type); }\n")
            .Append("[[maybe_unused, gnu::noinline]] static bool array_type(const Descriptor* type) noexcept {\n");
        foreach (var array in arrays)
            text.Append("    if (type == &").Append(TypeSymbol(array)).Append("_descriptor) return true;\n");
        text.Append("    (void)type; return false;\n}\n");
        text.Append("[[maybe_unused]] static bool string_type(const Descriptor* type) noexcept { ")
            .Append(program.Flow.Types.String is { } textType ? "return type == &" + TypeSymbol(textType) + "_descriptor;" : "(void)type; return false;")
            .Append(" }\n");
        foreach (var method in methods.Values.OrderBy(m => m.Source.ContentHash, StringComparer.Ordinal).ThenBy(m => m.Source.Token))
        {
            var layout = layouts[IrMethodId.From(method.Source)];
            text.Append("struct ").Append(FrameType(method)).Append(" {\n    Frame header;\n    const Argument* incoming = nullptr; Argument result;\n")
                .Append("    std::array<Object*, ").Append(N(layout.RootCount)).Append("> roots{};\n")
                .Append("    std::array<std::uint32_t, ").Append(N(layout.IntegerCount)).Append("> integers{};\n")
                .Append("    std::array<Argument, ").Append(N(layout.CallArgumentCount)).Append("> arguments{};\n")
                .Append("    std::int64_t signed_scratch = 0; std::uint64_t unsigned_scratch = 0;\n");
            foreach (var kind in layout.DispatchKinds.OrderBy(k => k))
                text.Append("    ").Append(CType(new(kind))).Append(" (*dispatch_").Append(kind.ToString().ToLowerInvariant())
                    .Append(")(Context&, const Argument*) noexcept = nullptr;\n");
            text.Append("};\n");
            Header(method); text.Append(";\n");
        }
        var dispatch = program.Dispatch.Concat(program.InterfaceDispatch is { } prepared
            ? prepared.Dispatch.Select(m => new ObjectVirtualDispatch(m.Declaration,
                [.. m.Targets.Select(t => new ObjectVirtualTarget(t.Receiver, t.Implementation))])) : []);
        foreach (var map in dispatch)
        {
            string name = DispatchSymbol(map.Declaration);
            text.Append("using ").Append(name).Append("_body = ").Append(CType(signatures[map.Declaration].ReturnType))
                .Append(" (*)(Context&, const Argument*) noexcept;\nstruct ").Append(name)
                .Append("_entry { const Descriptor* type; ").Append(name).Append("_body body; };\n")
                .Append("static constexpr std::array<").Append(name).Append("_entry, ").Append(N(map.Targets.Length)).Append("> ")
                .Append(name).Append("_table = {{\n");
            foreach (var target in map.Targets)
                text.Append("    { &").Append(TypeSymbol(target.Receiver)).Append("_descriptor, ")
                    .Append(CppEmitter.Symbol(target.Implementation)).Append(" },\n");
            text.Append("}};\n[[maybe_unused, gnu::noinline]] static ").Append(name).Append("_body ").Append(name)
                .Append("(const Descriptor* type) noexcept {\n    for (const auto& entry : ").Append(name)
                .Append("_table) if (entry.type == type) return entry.body;\n    return nullptr;\n}\n");
        }
        foreach (var method in methods.Values.OrderBy(m => m.Source.ContentHash, StringComparer.Ordinal).ThenBy(m => m.Source.Token)) AppendMethod(method);
        text.Append("}\n// Private experimental boundary; valid disjoint outputs, references remain internal.\n")
            .Append("// Parameters must point to count readable Int32 values; exact arity required, null allowed only for zero arity.\n")
            .Append("// 0 success, 1 divide-by-zero, 2 overflow, 3 invalid boundary arguments, 4 null, 5 allocation, 6 ownership/type, 7 invalid cast, 8 call-depth, 9 execution storage, 10 index bounds, 11 array type mismatch.\n")
            .Append("// Result unchanged on failure; origin text borrows the generated library lifetime.\n")
            .Append("extern \"C\" std::uint32_t ").Append(symbol).Append("_try(std::int32_t* result, std::uint32_t* method, std::uint32_t* il, const char** image, const std::int32_t* parameters, std::size_t count) noexcept {\n")
            .Append("    if (result == nullptr || method == nullptr || il == nullptr || image == nullptr || count != ").Append(N(entry.Parameters.Length))
            .Append("u || (count != 0 && parameters == nullptr)) return 3u;\n    using namespace ")
            .Append(scope).Append(";\n    Instance heap;\n    Context context(heap);\n    std::int32_t value = 0;\n    {\n")
            .Append("    Storage<std::array<Argument, ").Append(N(entry.Parameters.Length)).Append(">> arguments(context);\n")
            .Append("    if (arguments.value == nullptr) { context.pending = 9u; context.image = \"").Append(entry.Source.ContentHash.ToLowerInvariant())
            .Append("\"; context.method = ").Append(N(entry.Source.Token)).Append("u; context.il = ").Append(N(entry.Blocks[0].ILOffset)).Append("u; } else {\n");
        text.Append("    for (std::size_t i = 0; i < count; ++i) (*arguments.value)[i].bits = std::bit_cast<std::uint32_t>(parameters[i]);\n");
        text.Append("    value = ").Append(CppEmitter.Symbol(program.Flow.Entry)).Append("(context, arguments.value->data());\n    }\n    }\n")
            .Append("    if (context.pending == 0u) *result = value;\n    *method = context.method; *il = context.il; *image = context.image;\n    return context.pending;\n}\n");
        return new(entry.Source, symbol + "_try", text.ToString()) { FaultEntryPoint = symbol + "_try", MayThrow = true };

        void Header(IrMethod method)
        {
            text.Append("[[maybe_unused]] static ").Append(CType(method.ReturnType)).Append(' ').Append(CppEmitter.Symbol(IrMethodId.From(method.Source)))
                .Append("([[maybe_unused]] Context& context, [[maybe_unused]] const Argument* arguments) noexcept");
        }

        string FrameType(IrMethod method) => CppEmitter.Symbol(IrMethodId.From(method.Source)) + "_storage";

        void AppendMethod(IrMethod method)
        {
            var layout = layouts[IrMethodId.From(method.Source)];
            int currentBlock = 0;
            var output = text;
            var code = new StringBuilder();
            var steps = new List<string>();
            var labels = new Dictionary<string, int>();
            void Flush(bool terminal = false)
            {
                if (code.Length == 0) return;
                if (!terminal) code.Append("    return ").Append(N(steps.Count + 1)).Append("u;\n");
                steps.Add(code.ToString()); code.Clear();
            }
            void Label(string label) { Flush(); labels.Add(label, steps.Count); }
            bool inStep = true;
            string Value(int id) => layout.Storage(ObjectRootKind.Value, id, currentBlock);
            string Exit() => inStep ? "return frame_done;" : method.ReturnType.Kind switch { IrTypeKind.Void => "return;", IrTypeKind.Reference => "return nullptr;", _ => "return 0;" };
            void Fault(string status, int offset)
            {
                code.Append("        context.pending = ").Append(status).Append("; context.image = \"").Append(method.Source.ContentHash.ToLowerInvariant())
                    .Append("\"; context.method = 0x").Append(method.Source.Token.ToString("x8", CultureInfo.InvariantCulture)).Append("u; context.il = ")
                    .Append(N(offset)).Append("u; ").Append(Exit()).Append('\n');
            }
            void Guard(string condition, string status, int offset)
            {
                code.Append("    if (").Append(condition).Append(") {\n"); Fault(status, offset); code.Append("    }\n");
            }
            string Matches(string value, ObjectTypeId type) => Predicate(type) + "(" + value + "->type)";
            void Check(string value, ObjectTypeId type, bool nullCheck, int offset)
            {
                if (nullCheck) Guard(value + " == nullptr", "4u", offset);
                Guard("!context.instance.owns(" + value + ")", "6u", offset);
                if (type != program.Flow.Types.ObjectBase)
                    Guard(value + " != nullptr && !" + Matches(value, type), "6u", offset);
            }
            void ArrayCheck(int receiver, ObjectTypeId? view, int? index, int offset)
            {
                string value = Value(receiver);
                Guard(value + " == nullptr", "4u", offset);
                Guard("!context.instance.owns(" + value + ")", "6u", offset);
                Guard("!" + (view is null ? "array_type(" + value + "->type)" : Matches(value, view)), "6u", offset);
                Flush();
                if (index is { } element)
                {
                    Guard(Value(element) + " >= array_length(" + value + ")", "10u", offset);
                    Flush();
                }
            }
            string Call(IrMethodId target, ImmutableArray<int> args, string? receiver = null, bool dispatch = false)
            {
                var parameters = signatures[target].Parameters;
                int start = receiver is null ? 0 : 1;
                if (receiver is not null) code.Append("    f->arguments[0].reference = ").Append(receiver).Append(";\n");
                for (int i = 0; i < args.Length; i++)
                {
                    code.Append("    f->arguments[").Append(N(i + start)).Append(parameters[i + start].Kind == IrTypeKind.Int32 ? "].bits = " : "].reference = ")
                        .Append(Value(args[i])).Append(";\n");
                    if ((i + 1) % CopiesPerStep == 0) Flush();
                }
                Flush();
                return (dispatch ? DispatchPointer(signatures[target].ReturnType.Kind) : CppEmitter.Symbol(target)) + "(context, f->arguments.data())";
            }
            string DispatchPointer(IrTypeKind kind) => "f->dispatch_" + kind.ToString().ToLowerInvariant();
            void SelectDispatch(IrMethodId target, ImmutableArray<int> arguments, int offset)
            {
                Check(Value(arguments[0]), signatures[target].Parameters[0].Class!, true, offset); Flush();
                string pointer = DispatchPointer(signatures[target].ReturnType.Kind);
                code.Append("    ").Append(pointer).Append(" = ").Append(DispatchSymbol(target)).Append('(')
                    .Append(Value(arguments[0])).Append("->type);\n");
                Guard(pointer + " == nullptr", "6u", offset); Flush();
            }
            void Pending() => code.Append("    if (context.pending != 0u) ").Append(Exit()).Append('\n');
            string Field(ObjectFieldId field, int receiver)
            {
                string value = Value(receiver);
                string storage = fields[field].Storage is ObjectStorage.Int32 ? "std::uint32_t" : "Object*";
                return "*static_cast<" + storage + "*>(field_address(" + value + ", " + N(native.Indices[field]) + "))";
            }
            for (int i = 0; i < method.Parameters.Length; i++)
            {
                code.Append("    ").Append(layout.Storage(ObjectRootKind.Parameter, i)).Append(" = f->incoming[").Append(N(i))
                    .Append(method.Parameters[i].Kind == IrTypeKind.Int32 ? "].bits;\n" : "].reference;\n");
                if ((i + 1) % CopiesPerStep == 0) Flush();
            }
            Flush();
            for (int i = 0; i < method.Parameters.Length; i++)
                if (method.Parameters[i].Kind == IrTypeKind.Reference)
                {
                    Check(layout.Storage(ObjectRootKind.Parameter, i), method.Parameters[i].Class!, false, method.Blocks[0].ILOffset);
                    Flush();
                }
            foreach (var block in method.Blocks)
            {
                currentBlock = block.Id;
                var values = block.Values.ToDictionary(v => v.Id);
                Label("b" + N(block.Id));
                int statementsInStep = 0;
                foreach (var statement in block.Statements)
                {
                    bool isolatedOperation = statement is IrStatement.StoreField or IrStatement.StoreElement
                        or IrStatement.Define { Value.Expression: IrExpression.NewArray or IrExpression.ArrayLength or IrExpression.LoadElement or IrExpression.LoadField or IrExpression.CastClass or IrExpression.IsInstance
                            or IrExpression.StringLiteral or IrExpression.StringLength or IrExpression.StringCodeUnit or IrExpression.StringEquals or IrExpression.StringConcat
                            or IrExpression.Call { VirtualDispatch: true } }
                        or IrStatement.Invoke { VirtualDispatch: true };
                    // Array/field access, type tests and virtual calls include ownership/type checks.
                    // Keep them in separate steps to retain the O0 sanitizer stack bound.
                    if (isolatedOperation) { Flush(); statementsInStep = 0; }
                    switch (statement)
                    {
                        case IrStatement.Define define:
                        {
                            var value = define.Value; string expression;
                            code.Append("    // IL_").Append(value.ILOffset.ToString("x4", CultureInfo.InvariantCulture)).Append('\n');
                            switch (value.Expression)
                            {
                                case IrExpression.StringLiteral literal:
                                    int literalId = literalIds[LiteralKey(literal.CodeUnits)];
                                    expression = "string_literal(context, " + TypeSymbol(program.Flow.Types.String!) + "_descriptor, " + N(literalId)
                                        + ", literal_" + N(literalId) + ".data(), " + N(literal.CodeUnits.Length) + "u)"; break;
                                case IrExpression.StringLength length:
                                    Check(Value(length.Operand), program.Flow.Types.String!, true, value.ILOffset); Flush();
                                    expression = "string_length(" + Value(length.Operand) + ")"; break;
                                case IrExpression.StringCodeUnit unit:
                                    Check(Value(unit.Operand), program.Flow.Types.String!, true, value.ILOffset); Flush();
                                    Guard(Value(unit.Index) + " >= string_length(" + Value(unit.Operand) + ")", "10u", value.ILOffset); Flush();
                                    expression = "string_code_unit(" + Value(unit.Operand) + ", " + Value(unit.Index) + ")"; break;
                                case IrExpression.StringEquals equal:
                                    Check(Value(equal.Left), program.Flow.Types.String!, false, value.ILOffset); Flush();
                                    Check(Value(equal.Right), program.Flow.Types.String!, false, value.ILOffset); Flush();
                                    expression = "string_equals(" + Value(equal.Left) + ", " + Value(equal.Right) + ")"; break;
                                case IrExpression.StringConcat concat:
                                    Check(Value(concat.Left), program.Flow.Types.String!, false, value.ILOffset); Flush();
                                    Check(Value(concat.Right), program.Flow.Types.String!, false, value.ILOffset); Flush();
                                    expression = "string_concat(context, " + TypeSymbol(program.Flow.Types.String!) + "_descriptor, " + Value(concat.Left) + ", " + Value(concat.Right) + ")"; break;
                                case IrExpression.NewObject allocation:
                                    string type = TypeSymbol(bindings[allocation.Constructor].Owner);
                                    expression = "allocate<" + type + ">(context, " + type + "_descriptor)"; break;
                                case IrExpression.NewArray array:
                                    expression = "allocate_array(context, " + TypeSymbol(array.ArrayType) + "_descriptor, " + Value(array.Length) + ")"; break;
                                case IrExpression.ArrayLength length:
                                    ArrayCheck(length.Array, null, null, value.ILOffset);
                                    expression = "array_length(" + Value(length.Array) + ")"; break;
                                case IrExpression.LoadElement element:
                                    ArrayCheck(element.Array, element.ArrayType, element.Index, value.ILOffset);
                                    expression = (value.Type.Kind == IrTypeKind.Int32 ? "load_integer_element(" : "load_reference_element(")
                                        + Value(element.Array) + ", " + Value(element.Index) + ")"; break;
                                case IrExpression.LoadField field:
                                    Check(Value(field.Receiver), field.Field.Owner, true, value.ILOffset);
                                    expression = Field(field.Field, field.Receiver); break;
                                case IrExpression.CastClass cast:
                                    Guard("!context.instance.owns(" + Value(cast.Operand) + ")", "6u", value.ILOffset);
                                    Guard(Value(cast.Operand) + " != nullptr && !" + Matches(Value(cast.Operand), cast.Target), "7u", value.ILOffset);
                                    expression = Value(cast.Operand); break;
                                case IrExpression.IsInstance test:
                                    Guard("!context.instance.owns(" + Value(test.Operand) + ")", "6u", value.ILOffset);
                                    expression = Value(test.Operand) + " != nullptr && " + Matches(Value(test.Operand), test.Target)
                                        + " ? " + Value(test.Operand) + " : nullptr"; break;
                                case IrExpression.Null: expression = "nullptr"; break;
                                case IrExpression.Parameter p: expression = layout.Storage(ObjectRootKind.Parameter, p.Index); break;
                                case IrExpression.Local l: expression = layout.Storage(ObjectRootKind.Local, l.Index); break;
                                case IrExpression.StackInput input: expression = layout.Storage(ObjectRootKind.BlockInput, input.Index, block.Id); break;
                                case IrExpression.Call call:
                                    if (call.VirtualDispatch) SelectDispatch(call.Target, call.Arguments, value.ILOffset);
                                    else if (call.NullCheckReceiver) Guard(Value(call.Arguments[0]) + " == nullptr", "4u", value.ILOffset);
                                    expression = Call(call.Target, call.Arguments, dispatch: call.VirtualDispatch);
                                    if (value.Type.Kind == IrTypeKind.Int32) expression = "std::bit_cast<std::uint32_t>(" + expression + ")";
                                    break;
                                default:
                                    expression = CppEmitter.AppendValuePreparation(code, value, block.Id, (fault, offset) => Fault(N((int)fault) + "u", offset),
                                        Value, signed => signed ? "f->signed_scratch" : "f->unsigned_scratch"); break;
                            }
                            code.Append("    ").Append(Value(value.Id)).Append(" = ").Append(expression).Append(";\n");
                            if (value.Expression is IrExpression.NewObject created)
                            {
                                Guard(Value(value.Id) + " == nullptr", "context.pending == 0u ? 5u : context.pending", value.ILOffset);
                                // Publish the new reference in the caller frame before entering its constructor.
                                string constructorCall = Call(created.Constructor, created.Arguments, Value(value.Id));
                                code.Append("    ").Append(constructorCall).Append(";\n"); Pending();
                            }
                            else if (value.Expression is IrExpression.NewArray or IrExpression.StringLiteral or IrExpression.StringConcat)
                                Guard(Value(value.Id) + " == nullptr", "context.pending == 0u ? 5u : context.pending", value.ILOffset);
                            else if (value.Expression is IrExpression.LoadElement && value.Type.Kind == IrTypeKind.Reference)
                            {
                                Flush(); Check(Value(value.Id), value.Type.Class!, false, value.ILOffset);
                            }
                            else if (value.Expression is IrExpression.Call) Pending();
                            break;
                        }
                        case IrStatement.StoreLocal store:
                            code.Append("    ").Append(layout.Storage(ObjectRootKind.Local, store.Local)).Append(" = ").Append(Value(store.Value)).Append(";\n"); break;
                        case IrStatement.Invoke invoke:
                            if (invoke.VirtualDispatch) SelectDispatch(invoke.Target, invoke.Arguments, invoke.ILOffset);
                            else if (invoke.NullCheckReceiver) Guard(Value(invoke.Arguments[0]) + " == nullptr", "4u", invoke.ILOffset);
                            string invocation = Call(invoke.Target, invoke.Arguments, dispatch: invoke.VirtualDispatch);
                            code.Append("    ").Append(invocation).Append(";\n"); Pending(); break;
                        case IrStatement.StoreElement store:
                            ArrayCheck(store.Array, store.ArrayType, store.Index, store.ILOffset);
                            if (store.ArrayType.ArrayElement!.Kind == ObjectArrayElementKind.Int32)
                                code.Append("    store_integer_element(").Append(Value(store.Array)).Append(", ").Append(Value(store.Index)).Append(", ")
                                    .Append(Value(store.Value)).Append(");\n");
                            else
                                Guard("!store_reference_element(context, " + Value(store.Array) + ", " + Value(store.Index) + ", " + Value(store.Value) + ")", "context.pending", store.ILOffset);
                            break;
                        case IrStatement.StoreField store:
                            Check(Value(store.Receiver), store.Field.Owner, true, store.ILOffset);
                            if (values[store.Value].Type.Kind == IrTypeKind.Reference)
                                Check(Value(store.Value), values[store.Value].Type.Class!, false, store.ILOffset);
                            code.Append("    ").Append(Field(store.Field, store.Receiver)).Append(" = ").Append(Value(store.Value)).Append(";\n"); break;
                    }
                    if (isolatedOperation || ++statementsInStep == StatementsPerStep) { Flush(); statementsInStep = 0; }
                }
                Flush();
                switch (block.Terminator)
                {
                    case IrReturn returned:
                        code.Append(method.ReturnType.Kind == IrTypeKind.Int32 ? "    f->result.bits = " : "    f->result.reference = ")
                            .Append(Value(returned.Value)).Append(";\n    return frame_done;\n"); Flush(terminal: true); break;
                    case IrVoidReturn: code.Append("    return frame_done;\n"); Flush(terminal: true); break;
                    case IrJump jump: Edge(jump.Edge); break;
                    case IrBranch branch:
                        string taken = "taken" + N(block.Id), other = "other" + N(block.Id);
                        code.Append("    return (").Append(CppEmitter.Comparison(branch.Comparison, branch.Left, branch.Right,
                            values[branch.Left].Type.Kind != IrTypeKind.Int32, Value)).Append(") ? ").Append(taken).Append(" : ").Append(other).Append(";\n");
                        Flush(terminal: true);
                        Label(taken); Edge(branch.Taken); Label(other); Edge(branch.NotTaken); break;
                }
            }
            code = output;
            string stepScope = CppEmitter.Symbol(IrMethodId.From(method.Source)) + "_steps";
            code.Append("namespace ").Append(stepScope).Append(" {\n");
            foreach (var label in labels) code.Append("[[maybe_unused]] static constexpr std::uint32_t ").Append(label.Key).Append(" = ").Append(N(label.Value)).Append("u;\n");
            for (int i = 0; i < steps.Count; i++)
                code.Append("[[gnu::noinline]] static std::uint32_t s").Append(N(i)).Append("([[maybe_unused]] Context& context, [[maybe_unused]] ")
                    .Append(FrameType(method)).Append("* f) noexcept {\n").Append(steps[i]).Append("}\n");
            code.Append("static constexpr std::uint32_t (*table[])(Context&, ").Append(FrameType(method)).Append("*) noexcept = {")
                .Append(string.Join(", ", Enumerable.Range(0, steps.Count).Select(i => "s" + N(i)))).Append("};\n}\n");
            inStep = false;
            Header(method); code.Append(" {\n");
            Guard("context.depth >= 64u", "8u", method.Blocks[0].ILOffset);
            code.Append("    Storage<").Append(FrameType(method)).Append("> storage(context);\n");
            Guard("storage.value == nullptr", "9u", method.Blocks[0].ILOffset);
            code.Append("    auto* f = storage.value; f->incoming = arguments;\n")
                .Append("    RootScope roots(context, f->header, f->roots.data(), f->roots.size());\n")
                .Append("    std::uint32_t step = 0;\n    while (step != frame_done && context.pending == 0u) step = ").Append(stepScope).Append("::table[step](context, f);\n")
                .Append("    ").Append(method.ReturnType.Kind switch
                {
                    IrTypeKind.Void => "return;",
                    IrTypeKind.Int32 => "return std::bit_cast<std::int32_t>(f->result.bits);",
                    _ => "return f->result.reference;",
                }).Append("\n}\n");
            void Edge(IrEdge edge)
            {
                // Inputs and source values have distinct slots, including on self edges.
                for (int i = 0; i < edge.Arguments.Length; i++)
                {
                    code.Append("    ").Append(layout.Storage(ObjectRootKind.BlockInput, i, edge.Target)).Append(" = ").Append(Value(edge.Arguments[i])).Append(";\n");
                    if ((i + 1) % CopiesPerStep == 0) Flush();
                }
                code.Append("    return b").Append(N(edge.Target)).Append(";\n"); Flush(terminal: true);
            }
        }
    }

    private static string CType(IrType type) => type.Kind switch { IrTypeKind.Int32 => "std::int32_t", IrTypeKind.Void => "void", _ => "Object*" };
    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string TypeSymbol(ObjectTypeId type) => "t_" + type.ContentHash + "_" + type.Token.ToString("x8", CultureInfo.InvariantCulture)
        + (type.ArrayElement is null ? "" : type.ArrayElement.Kind == ObjectArrayElementKind.Int32 ? "_vector_i4"
            : "_vector_ref_" + TypeSymbol(type.ArrayElement.Type!));
    private static string DispatchSymbol(IrMethodId method) => CppEmitter.Symbol(method) + "_dispatch";
    private static string ContractSymbol(ObjectTypeId type) => TypeSymbol(type) + "_implements";
    private static string FieldSymbol(ObjectFieldId field) => "f_" + field.Token.ToString("x8", CultureInfo.InvariantCulture);
}
