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
    public const string Profile = "object-frames-native-v2";

    public static CppArtifact EmitProgram(ObjectIrProgram program)
    {
        var plans = ObjectRootAssignment.Assign(program).ToDictionary(p => p.Method);
        var methods = program.Flow.Methods.ToDictionary(m => IrMethodId.From(m.Source));
        var bindings = program.Bindings.ToDictionary(b => b.Method);
        var layouts = methods.ToDictionary(p => p.Key, p => new ObjectFrameLayout(p.Value, plans[p.Key]));
        var entry = methods[program.Flow.Entry];
        string symbol = CppEmitter.Symbol(program.Flow.Entry) + "_objects_packed", scope = symbol + "_closure";
        var text = new StringBuilder();
        text.Append("// Cilica ").Append(Profile).Append("\n#include <array>\n#include <bit>\n#include <cstddef>\n#include <cstdint>\n#include <new>\n#include <type_traits>\n")
            .Append(ObjectCppRuntime.DiagnosticIncludes).Append("namespace ")
            .Append(scope).Append(" {\n").Append(ObjectCppRuntime.Text).Append('\n');
        foreach (var layout in program.Flow.Types.Types.OrderBy(t => t.Source.ContentHash, StringComparer.Ordinal).ThenBy(t => t.Source.Token))
        {
            string type = TypeSymbol(ObjectTypeId.From(layout.Source));
            text.Append("struct ").Append(type).Append(" { Object header;\n");
            foreach (var field in layout.Fields)
                text.Append("    ").Append(field.Storage is ObjectStorage.Int32 ? "std::uint32_t" : "Object*").Append(' ').Append(FieldSymbol(field.Id)).Append(";\n");
            text.Append("};\nstatic_assert(std::is_standard_layout_v<").Append(type).Append("> && std::is_trivially_destructible_v<")
                .Append(type).Append("> && offsetof(").Append(type).Append(", header) == 0);\n")
                .Append("static constexpr std::array<std::size_t, ").Append(N(layout.ReferenceSlots.Length)).Append("> ").Append(type).Append("_references = {")
                .Append(string.Join(", ", layout.Fields.Where(f => f.Storage is ObjectStorage.Reference).Select(f => "offsetof(" + type + ", " + FieldSymbol(f.Id) + ")")))
                .Append("};\n[[maybe_unused]] static constexpr Descriptor ").Append(type).Append("_descriptor = { sizeof(").Append(type).Append("), ")
                .Append("alignof(").Append(type).Append("), ").Append(type).Append("_references.data(), ").Append(N(layout.ReferenceSlots.Length))
                .Append(", [](Object* p) noexcept { delete reinterpret_cast<").Append(type).Append("*>(p); } };\n");
        }
        foreach (var method in methods.Values.OrderBy(m => m.Source.ContentHash, StringComparer.Ordinal).ThenBy(m => m.Source.Token))
        {
            var layout = layouts[IrMethodId.From(method.Source)];
            text.Append("struct ").Append(FrameType(method)).Append(" {\n    Frame header;\n    const Argument* incoming = nullptr; Argument result;\n")
                .Append("    std::array<Object*, ").Append(N(layout.RootCount)).Append("> roots{};\n")
                .Append("    std::array<std::uint32_t, ").Append(N(layout.IntegerCount)).Append("> integers{};\n")
                .Append("    std::array<Argument, ").Append(N(layout.CallArgumentCount)).Append("> arguments{};\n")
                .Append("    std::int64_t signed_scratch = 0; std::uint64_t unsigned_scratch = 0;\n};\n");
            Header(method); text.Append(";\n");
        }
        foreach (var method in methods.Values.OrderBy(m => m.Source.ContentHash, StringComparer.Ordinal).ThenBy(m => m.Source.Token)) AppendMethod(method);
        text.Append("}\n// Private experimental boundary; valid disjoint outputs, references remain internal.\n")
            .Append("// Parameters must point to count readable Int32 values; exact arity required, null allowed only for zero arity.\n")
            .Append("// 0 success, 1 divide-by-zero, 2 overflow, 3 invalid boundary arguments, 4 null, 5 allocation, 6 ownership/type, 8 call-depth, 9 execution storage.\n")
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
            void Check(string value, ObjectTypeId type, bool nullCheck, int offset)
            {
                if (nullCheck) Guard(value + " == nullptr", "4u", offset);
                Guard("!context.instance.owns(" + value + ")", "6u", offset);
                if (type != program.Flow.Types.ObjectBase)
                    Guard(value + " != nullptr && " + value + "->type != &" + TypeSymbol(type) + "_descriptor", "6u", offset);
            }
            string Call(IrMethodId target, ImmutableArray<int> args, string? receiver = null)
            {
                var parameters = methods[target].Parameters;
                int start = receiver is null ? 0 : 1;
                if (receiver is not null) code.Append("    f->arguments[0].reference = ").Append(receiver).Append(";\n");
                for (int i = 0; i < args.Length; i++)
                {
                    code.Append("    f->arguments[").Append(N(i + start)).Append(parameters[i + start].Kind == IrTypeKind.Int32 ? "].bits = " : "].reference = ")
                        .Append(Value(args[i])).Append(";\n");
                    if ((i + 1) % CopiesPerStep == 0) Flush();
                }
                Flush();
                return CppEmitter.Symbol(target) + "(context, f->arguments.data())";
            }
            void Pending() => code.Append("    if (context.pending != 0u) ").Append(Exit()).Append('\n');
            string Field(ObjectFieldId field, int receiver) => "reinterpret_cast<" + TypeSymbol(field.Owner) + "*>(" + Value(receiver) + ")->" + FieldSymbol(field);
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
                    switch (statement)
                    {
                        case IrStatement.Define define:
                        {
                            var value = define.Value; string expression;
                            code.Append("    // IL_").Append(value.ILOffset.ToString("x4", CultureInfo.InvariantCulture)).Append('\n');
                            switch (value.Expression)
                            {
                                case IrExpression.NewObject allocation:
                                    string type = TypeSymbol(bindings[allocation.Constructor].Owner);
                                    expression = "allocate<" + type + ">(context, " + type + "_descriptor)"; break;
                                case IrExpression.LoadField field:
                                    Check(Value(field.Receiver), field.Field.Owner, true, value.ILOffset);
                                    expression = Field(field.Field, field.Receiver); break;
                                case IrExpression.Null: expression = "nullptr"; break;
                                case IrExpression.Parameter p: expression = layout.Storage(ObjectRootKind.Parameter, p.Index); break;
                                case IrExpression.Local l: expression = layout.Storage(ObjectRootKind.Local, l.Index); break;
                                case IrExpression.StackInput input: expression = layout.Storage(ObjectRootKind.BlockInput, input.Index, block.Id); break;
                                case IrExpression.Call call:
                                    if (call.NullCheckReceiver) Guard(Value(call.Arguments[0]) + " == nullptr", "4u", value.ILOffset);
                                    expression = Call(call.Target, call.Arguments);
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
                            else if (value.Expression is IrExpression.Call) Pending();
                            break;
                        }
                        case IrStatement.StoreLocal store:
                            code.Append("    ").Append(layout.Storage(ObjectRootKind.Local, store.Local)).Append(" = ").Append(Value(store.Value)).Append(";\n"); break;
                        case IrStatement.Invoke invoke:
                            if (invoke.NullCheckReceiver) Guard(Value(invoke.Arguments[0]) + " == nullptr", "4u", invoke.ILOffset);
                            string invocation = Call(invoke.Target, invoke.Arguments);
                            code.Append("    ").Append(invocation).Append(";\n"); Pending(); break;
                        case IrStatement.StoreField store:
                            Check(Value(store.Receiver), store.Field.Owner, true, store.ILOffset);
                            if (values[store.Value].Type.Kind == IrTypeKind.Reference)
                                Check(Value(store.Value), values[store.Value].Type.Class!, false, store.ILOffset);
                            code.Append("    ").Append(Field(store.Field, store.Receiver)).Append(" = ").Append(Value(store.Value)).Append(";\n"); break;
                    }
                    if (++statementsInStep == StatementsPerStep) { Flush(); statementsInStep = 0; }
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
    private static string TypeSymbol(ObjectTypeId type) => "t_" + type.ContentHash + "_" + type.Token.ToString("x8", CultureInfo.InvariantCulture);
    private static string FieldSymbol(ObjectFieldId field) => "f_" + field.Token.ToString("x8", CultureInfo.InvariantCulture);
}
