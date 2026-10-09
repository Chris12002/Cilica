using System;
using System.Collections.Immutable;
using System.Linq;
using System.IO;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class WideRootFrameTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Constructor_arguments_survive_a_wide_branching_heap_frame(bool optimized)
    {
        var result = IntegerObjectTranslationTests.Translate("Run", fixture: typeof(WideRootProbe));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var logical = ValueRootAssignment.Assign(result.Program!);
        var lowered = ValueScalarLowering.LowerExceptions(result.Program!);
        var scalar = ObjectRootAssignment.AssignExceptions(lowered);
        Assert.InRange(logical.Max(p => p.Slots.Length), 1025, ObjectRootAssignment.MaximumIntegerRootsPerMethod);
        Assert.InRange(scalar.Max(p => p.Slots.Length), 1025, ObjectRootAssignment.MaximumIntegerRootsPerMethod);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("Run", result.Program!, WideRootProbe.Run)]);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void Logical_and_scalar_planners_enforce_the_selected_root_limit(bool integers)
    {
        int limit = integers ? ObjectRootAssignment.MaximumIntegerRootsPerMethod : ObjectRootAssignment.MaximumRootsPerMethod;
        var original = BoxedValueIrTests.Model(empty: true);
        BoxedValueIrProgram Model(int count)
        {
            var flow = original.Flow.Flow;
            var catalog = flow.Types with { References = flow.Types.References with { Integer64 = integers } };
            var leaf = catalog.Types[0]; var type = ObjectTypeId.From(leaf.Source);
            var statements = ImmutableArray.CreateBuilder<IrStatement>();
            statements.Add(new IrStatement.Define(new(0, IrType.Value(type), 0, new IrExpression.DefaultValue(type))));
            for (int i = 1; i <= count; ++i)
                statements.Add(new IrStatement.Define(new(i, IrType.Reference(catalog.References.ObjectBase), 0, new IrExpression.BoxValue(type, 0)), IrExceptionTarget.MethodExit));
            statements.Add(new IrStatement.Define(new(count + 1, IrType.Int32, 0, new IrExpression.Constant(0))));
            var method = flow.Methods[0] with { Blocks = [new(0, 0, [], statements.ToImmutable(), new IrReturn(1, count + 1))] };
            return original with { Flow = new(flow with { Types = catalog, Methods = [method] }) };
        }
        var exact = Model(limit);
        Assert.Equal(limit, Assert.Single(ValueRootAssignment.Assign(exact)).Slots.Length);
        Assert.Contains($"{limit} phase A root-slot limit", Assert.Throws<ArgumentException>(() => ValueRootAssignment.Assign(Model(limit + 1))).Message, StringComparison.Ordinal);
        var scalar = ValueScalarLowering.LowerBoxes(exact);
        Assert.Equal(limit, Assert.Single(ObjectRootAssignment.AssignBoxes(scalar)).Slots.Length);
        var objects = scalar.Flow.Flow; var references = objects.Flow; var method = references.Methods[0]; var block = method.Blocks[0];
        int next = block.Values.Max(v => v.Id) + 1;
        var excess = method with { Blocks = [block with { Statements = block.Statements.Add(new IrStatement.Define(new(next, IrType.NullReference, 0, new IrExpression.Null()))) }] };
        var forged = scalar with { Flow = new(objects with { Flow = references with { Methods = [excess] } }) };
        Assert.Contains($"{limit} phase A root-slot limit", Assert.Throws<ArgumentException>(() => ObjectRootAssignment.AssignBoxes(forged)).Message, StringComparison.Ordinal);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Native_tracing_and_borrow_registration_share_the_profile_bound(bool optimized)
    {
        var text = new StringBuilder();
        foreach (bool integers in new[] { false, true })
        {
            var original = NativeScalarLocationTests.Model(); var objects = original.Flow;
            var model = original with { Flow = objects with { Flow = objects.Flow with { Types = objects.Flow.Types with { Integer64 = integers } } } };
            var artifact = ObjectCppEmitter.EmitLocations(model);
            // Each profile has the same input identity; isolate C++ names and the C export in this combined test consumer.
            string label = integers ? "wide" : "legacy";
            // Standard headers must stay outside the outer namespace.
            string body = artifact.Text.Replace(artifact.EntryPoint, label + "_" + artifact.EntryPoint, StringComparison.Ordinal);
            int begin = body.IndexOf("namespace ", StringComparison.Ordinal);
            text.Append(body.AsSpan(0, begin)).Append("namespace ").Append(label).Append(" {\n").Append(body.AsSpan(begin));
            text.Append(CultureInfo.InvariantCulture, $$"""
                using namespace {{artifact.EntryPoint[..^4]}}_closure;
                struct RootBuffers { std::array<Object*, frame_root_limit> roots{}; std::array<ReferencePredicate, frame_root_limit> types{}; };
                [[gnu::noinline]] static void check() {
                    static_assert(frame_root_limit == {{(integers ? 2048 : 1024)}});
                    auto* data = new RootBuffers;
                    for (auto& type : data->types) type = [](const Descriptor*) noexcept { return true; };
                    Instance heap; Context c(heap);
                    {
                        Frame frame; RootScope roots(c, frame, data->roots.data(), data->roots.size());
                        std::size_t count = 0;
                        auto visitor = [](void* state, const ReferenceEdge& edge) noexcept {
                            if (edge.kind == ReferenceKind::frame) ++*static_cast<std::size_t*>(state);
                            return true;
                        };
                        assert(visit_roots(c, visitor, &count) == TraceResult::complete && count == frame_root_limit);
                        { BorrowScope borrow(c, frame, nullptr, 0, data->types.data()); assert(borrow.status == LocationStatus::complete); }
                        frame.count = frame_root_limit + 1;
                        assert(visit_roots(c, visitor, &count) == TraceResult::invalid);
                        { BorrowScope borrow(c, frame, nullptr, 0, data->types.data()); assert(borrow.status == LocationStatus::invalid); }
                        frame.count = frame_root_limit;
                    }
                    assert(c.top == nullptr && c.borrows == nullptr && c.depth == 0);
                    delete data;
                }
                }
                """);
        }
        text.Insert(0, "#include <cassert>\n"); text.Append("int main() { legacy::check(); wide::check(); }\n");
        using var files = new ResolverFiles(); string dir = files.DirectoryPath("wide-root-bounds"), cpp = Path.Combine(dir, "root.cpp"), exe = Path.Combine(dir, "root");
        await File.WriteAllTextAsync(cpp, text.ToString(), TestContext.Current.CancellationToken); NativeEvidence.Record(cpp, optimized);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], dir);
        Assert.True(build.ExitCode == 0, build.Stderr); NativeEvidence.Record(cpp, optimized, exe);
        var run = await NativeProcess.Run(exe, [], dir); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
