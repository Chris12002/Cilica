using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Cilica.Compiler;

/// <summary>Emits the bounded typed object profile; references never cross its Int32 C boundary.</summary>
public static class ObjectCppEmitter
{
    public const string Profile = "object-control-flow-native-v1";

    public static CppArtifact EmitProgram(ObjectIrProgram program)
    {
        var plans = ObjectRootAssignment.Assign(program).ToDictionary(p => p.Method);
        var methods = program.Flow.Methods.ToDictionary(m => IrMethodId.From(m.Source));
        var bindings = program.Bindings.ToDictionary(b => b.Method);
        var entry = methods[program.Flow.Entry];
        string symbol = CppEmitter.Symbol(program.Flow.Entry) + "_objects", scope = symbol + "_closure";
        var text = new StringBuilder();
        text.Append("// Cilica ").Append(Profile).Append("\n#include <array>\n#include <bit>\n#include <cstddef>\n#include <cstdint>\n#include <new>\n#include <type_traits>\nnamespace ")
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
            Header(method); text.Append(";\n");
        }
        foreach (var method in methods.Values.OrderBy(m => m.Source.ContentHash, StringComparer.Ordinal).ThenBy(m => m.Source.Token)) AppendMethod(method);
        text.Append("}\n// Private experimental boundary; valid disjoint outputs, references remain internal.\n")
            .Append("// 0 success, 1 divide-by-zero, 2 overflow, 3 invalid outputs, 4 null, 5 allocation, 6 ownership/type, 8 call-depth.\n")
            .Append("// Result unchanged on failure; origin text borrows the generated library lifetime.\n")
            .Append("extern \"C\" std::uint32_t ").Append(symbol).Append("_try(std::int32_t* result, std::uint32_t* method, std::uint32_t* il, const char** image");
        for (int i = 0; i < entry.Parameters.Length; i++) text.Append(", std::int32_t p").Append(N(i));
        text.Append(") noexcept {\n    if (result == nullptr || method == nullptr || il == nullptr || image == nullptr) return 3u;\n    ")
            .Append(scope).Append("::Instance heap;\n    ").Append(scope).Append("::Context context(heap);\n    const std::int32_t value = ")
            .Append(scope).Append("::").Append(CppEmitter.Symbol(program.Flow.Entry)).Append("(context");
        for (int i = 0; i < entry.Parameters.Length; i++) text.Append(", p").Append(N(i));
        text.Append(");\n    if (context.pending == 0u) *result = value;\n    *method = context.method; *il = context.il; *image = context.image;\n    return context.pending;\n}\n");
        return new(entry.Source, symbol + "_try", text.ToString()) { FaultEntryPoint = symbol + "_try", MayThrow = true };

        void Header(IrMethod method)
        {
            text.Append("[[maybe_unused]] static ").Append(CType(method.ReturnType)).Append(' ').Append(CppEmitter.Symbol(IrMethodId.From(method.Source)))
                .Append("([[maybe_unused]] Context& context");
            for (int i = 0; i < method.Parameters.Length; i++)
                text.Append(", [[maybe_unused]] ").Append(CType(method.Parameters[i])).Append(" p").Append(N(i));
            text.Append(") noexcept");
        }

        void AppendMethod(IrMethod method)
        {
            var plan = plans[IrMethodId.From(method.Source)];
            var roots = plan.Slots.ToDictionary(s => (s.Kind, s.Block, s.Index), s => s.Slot);
            string Exit() => method.ReturnType.Kind switch { IrTypeKind.Void => "return;", IrTypeKind.Reference => "return nullptr;", _ => "return 0;" };
            void Fault(string code, int offset)
            {
                text.Append("        context.pending = ").Append(code).Append("; context.image = \"").Append(method.Source.ContentHash.ToLowerInvariant())
                    .Append("\"; context.method = 0x").Append(method.Source.Token.ToString("x8", CultureInfo.InvariantCulture)).Append("u; context.il = ")
                    .Append(N(offset)).Append("u; ").Append(Exit()).Append('\n');
            }
            void Guard(string condition, string code, int offset)
            {
                text.Append("    if (").Append(condition).Append(") {\n"); Fault(code, offset); text.Append("    }\n");
            }
            void Check(string value, ObjectTypeId type, bool nullCheck, int offset)
            {
                if (nullCheck) Guard(value + " == nullptr", "4u", offset);
                Guard("!context.instance.owns(" + value + ")", "6u", offset);
                if (type != program.Flow.Types.ObjectBase)
                    Guard(value + " != nullptr && " + value + "->type != &" + TypeSymbol(type) + "_descriptor", "6u", offset);
            }
            string Root(ObjectRootKind kind, int index, int block = -1) => "roots[" + N(roots[(kind, block, index)]) + "]";
            string Call(IrMethodId target, ImmutableArray<int> args, string? receiver = null)
            {
                var parameters = methods[target].Parameters;
                var arguments = args.Select((a, i) => parameters[i + (receiver is null ? 0 : 1)].Kind == IrTypeKind.Int32
                    ? "std::bit_cast<std::int32_t>(v" + N(a) + ")" : "v" + N(a));
                if (receiver is not null) arguments = arguments.Prepend(receiver);
                return CppEmitter.Symbol(target) + "(context" + string.Concat(arguments.Select(a => ", " + a)) + ")";
            }
            void Pending() => text.Append("    if (context.pending != 0u) ").Append(Exit()).Append('\n');
            string Field(ObjectFieldId field, int receiver) => "reinterpret_cast<" + TypeSymbol(field.Owner) + "*>(v" + N(receiver) + ")->" + FieldSymbol(field);
            Header(method); text.Append(" {\n");
            Guard("context.depth >= 64u", "8u", method.Blocks[0].ILOffset);
            text.Append("    std::array<Object*, ").Append(N(plan.Slots.Length)).Append("> roots{};\n");
            foreach (var slot in plan.Slots.Where(s => s.Kind == ObjectRootKind.Parameter))
                text.Append("    roots[").Append(N(slot.Slot)).Append("] = p").Append(N(slot.Index)).Append(";\n");
            text.Append("    Frame frame(context, roots.data(), roots.size());\n");
            for (int i = 0; i < method.Parameters.Length; i++)
                if (method.Parameters[i].Kind == IrTypeKind.Reference) Check(Root(ObjectRootKind.Parameter, i), method.Parameters[i].Class!, false, method.Blocks[0].ILOffset);
            for (int i = 0; i < method.Locals.Length; i++)
            {
                if (method.Locals[i].Kind == IrTypeKind.Int32)
                    text.Append("    [[maybe_unused]] std::uint32_t l").Append(N(i)).Append(" = 0u;\n");
                else text.Append("    [[maybe_unused]] Object*& l").Append(N(i)).Append(" = ").Append(Root(ObjectRootKind.Local, i)).Append(";\n");
            }
            foreach (var block in method.Blocks)
                for (int i = 0; i < block.Inputs.Length; i++)
                    text.Append("    [[maybe_unused]] ").Append(block.Inputs[i].Kind == IrTypeKind.Int32 ? "std::uint32_t " : "Object*& ")
                        .Append(Input(block.Id, i)).Append(" = ").Append(block.Inputs[i].Kind == IrTypeKind.Int32 ? "0u" : Root(ObjectRootKind.BlockInput, i, block.Id)).Append(";\n");
            text.Append("    goto b0;\n");
            foreach (var block in method.Blocks)
            {
                var values = block.Values.ToDictionary(v => v.Id);
                text.Append('b').Append(N(block.Id)).Append(": {\n");
                foreach (var statement in block.Statements)
                {
                    switch (statement)
                    {
                        case IrStatement.Define define:
                        {
                            var value = define.Value; string expression;
                            text.Append("    // IL_").Append(value.ILOffset.ToString("x4", CultureInfo.InvariantCulture)).Append('\n');
                            switch (value.Expression)
                            {
                                case IrExpression.NewObject allocation:
                                    string type = TypeSymbol(bindings[allocation.Constructor].Owner);
                                    expression = "allocate<" + type + ">(context, " + type + "_descriptor)"; break;
                                case IrExpression.LoadField field:
                                    Check("v" + N(field.Receiver), field.Field.Owner, true, value.ILOffset);
                                    expression = Field(field.Field, field.Receiver); break;
                                case IrExpression.Null: expression = "nullptr"; break;
                                case IrExpression.Parameter p when value.Type.Kind == IrTypeKind.Reference:
                                    expression = Root(ObjectRootKind.Parameter, p.Index); break;
                                case IrExpression.Call call:
                                    if (call.NullCheckReceiver) Guard("v" + N(call.Arguments[0]) + " == nullptr", "4u", value.ILOffset);
                                    expression = Call(call.Target, call.Arguments);
                                    if (value.Type.Kind == IrTypeKind.Int32) expression = "std::bit_cast<std::uint32_t>(" + expression + ")";
                                    break;
                                default:
                                    expression = CppEmitter.AppendValuePreparation(text, value, block.Id, (fault, offset) => Fault(N((int)fault) + "u", offset)); break;
                            }
                            if (value.Type.Kind is IrTypeKind.Reference or IrTypeKind.NullReference)
                            {
                                text.Append("    [[maybe_unused]] Object*& v").Append(N(value.Id)).Append(" = ").Append(Root(ObjectRootKind.Value, value.Id, block.Id)).Append(";\n")
                                    .Append("    v").Append(N(value.Id)).Append(" = ").Append(expression).Append(";\n");
                            }
                            else text.Append("    [[maybe_unused]] const std::uint32_t v").Append(N(value.Id)).Append(" = ").Append(expression).Append(";\n");
                            if (value.Expression is IrExpression.NewObject created)
                            {
                                Guard("v" + N(value.Id) + " == nullptr", "context.pending == 0u ? 5u : context.pending", value.ILOffset);
                                // Publish the new reference in the caller frame before entering its constructor.
                                text.Append("    ").Append(Call(created.Constructor, created.Arguments, "v" + N(value.Id))).Append(";\n"); Pending();
                            }
                            else if (value.Expression is IrExpression.Call) Pending();
                            break;
                        }
                        case IrStatement.StoreLocal store:
                            text.Append("    l").Append(N(store.Local)).Append(" = v").Append(N(store.Value)).Append(";\n"); break;
                        case IrStatement.Invoke invoke:
                            if (invoke.NullCheckReceiver) Guard("v" + N(invoke.Arguments[0]) + " == nullptr", "4u", invoke.ILOffset);
                            text.Append("    ").Append(Call(invoke.Target, invoke.Arguments)).Append(";\n"); Pending(); break;
                        case IrStatement.StoreField store:
                            Check("v" + N(store.Receiver), store.Field.Owner, true, store.ILOffset);
                            if (values[store.Value].Type.Kind == IrTypeKind.Reference)
                                Check("v" + N(store.Value), values[store.Value].Type.Class!, false, store.ILOffset);
                            text.Append("    ").Append(Field(store.Field, store.Receiver)).Append(" = v").Append(N(store.Value)).Append(";\n"); break;
                    }
                }
                switch (block.Terminator)
                {
                    case IrReturn returned:
                        text.Append("    return ").Append(method.ReturnType.Kind == IrTypeKind.Int32
                            ? "std::bit_cast<std::int32_t>(v" + N(returned.Value) + ")" : "v" + N(returned.Value)).Append(";\n"); break;
                    case IrVoidReturn: text.Append("    return;\n"); break;
                    case IrJump jump: Edge(jump.Edge); break;
                    case IrBranch branch:
                        text.Append("    if (").Append(CppEmitter.Comparison(branch.Comparison, branch.Left, branch.Right,
                            values[branch.Left].Type.Kind != IrTypeKind.Int32)).Append(") {\n");
                        Edge(branch.Taken); text.Append("    } else {\n"); Edge(branch.NotTaken); text.Append("    }\n"); break;
                }
                text.Append("}\n");
            }
            text.Append("}\n");
            void Edge(IrEdge edge)
            {
                // Every source v has distinct storage from every target input,
                // so these copies preserve parallel transfer even on self edges.
                for (int i = 0; i < edge.Arguments.Length; i++)
                    text.Append("    ").Append(Input(edge.Target, i)).Append(" = v").Append(N(edge.Arguments[i])).Append(";\n");
                text.Append("    goto b").Append(N(edge.Target)).Append(";\n");
            }
        }
    }

    private static string CType(IrType type) => type.Kind switch { IrTypeKind.Int32 => "std::int32_t", IrTypeKind.Void => "void", _ => "Object*" };
    private static string Input(int block, int index) => "s" + N(block) + "_" + N(index);
    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string TypeSymbol(ObjectTypeId type) => "t_" + type.ContentHash + "_" + type.Token.ToString("x8", CultureInfo.InvariantCulture);
    private static string FieldSymbol(ObjectFieldId field) => "f_" + field.Token.ToString("x8", CultureInfo.InvariantCulture);
}
