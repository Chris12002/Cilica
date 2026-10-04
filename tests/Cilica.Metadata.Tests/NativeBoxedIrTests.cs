using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeBoxedIrTests
{
    internal static (BoxedValueIrProgram Program, int FaultOffset) Model(int kind = 0)
    {
        var original = BoxedValueIrTests.Model(); var flow = original.Flow.Flow; var leaf = flow.Types.Roots[0];
        var empty = leaf with { Token = 0x02000008 }; var value = IrType.Value(leaf); var emptyValue = IrType.Value(empty);
        var obj = flow.Types.References.ObjectBase; var objectRef = IrType.Reference(obj);
        var vector = ObjectTypeId.Vector(obj, ObjectArrayElement.Int32); var arrayRef = IrType.Reference(vector);
        var catalog = flow.Types with { Roots = [leaf, empty], Types = flow.Types.Types.Add(new(
            new("fixture", empty.ContentHash, empty.Token, "Empty"), TypeAttributes.Public | TypeAttributes.Sealed, [])),
            References = flow.Types.References with { Roots = [vector], Arrays = [vector] } };
        var number = catalog.Types[0].Fields[0].Id; var reference = catalog.Types[0].Fields[1].Id;
        var statements = ImmutableArray.CreateBuilder<IrStatement>(); int next = 0;
        int D(IrType type, IrExpression expression, bool fault = false)
        { int id = next++; statements.Add(new IrStatement.Define(new(id, type, statements.Count * 2, expression), fault ? IrExceptionTarget.MethodExit : null)); return id; }
        int input = D(IrType.Int32, new IrExpression.Parameter(0));
        int initial = D(value, new IrExpression.DefaultValue(leaf));
        int numbered = D(value, new IrExpression.WithValueField(number, initial, input));
        int one = D(IrType.Int32, new IrExpression.Constant(1));
        int array = D(arrayRef, new IrExpression.NewArray(vector, one), true);
        int source = D(value, new IrExpression.WithValueField(reference, numbered, array));
        int emptySource = D(emptyValue, new IrExpression.DefaultValue(empty));
        int boxed = kind is 1 or 4 ? D(IrType.NullReference, new IrExpression.Null())
            : D(objectRef, new IrExpression.BoxValue(kind is 2 or 3 ? empty : leaf, kind is 2 or 3 ? emptySource : source), true);
        int second = D(objectRef, new IrExpression.BoxValue(leaf, source), true);
        var unboxType = kind >= 3 ? empty : leaf; var unboxed = IrType.Value(unboxType);
        int faultOffset = statements.Count * 2;
        int copy, borrowed;
        if (kind % 2 == 0)
        {
            copy = D(unboxed, new IrExpression.UnboxValue(unboxType, boxed), true);
            borrowed = D(IrType.Location(unboxed), new IrExpression.UnboxLocation(unboxType, boxed), true);
        }
        else
        {
            borrowed = D(IrType.Location(unboxed), new IrExpression.UnboxLocation(unboxType, boxed), true);
            copy = D(unboxed, new IrExpression.UnboxValue(unboxType, boxed), true);
        }
        var sourceMethod = flow.Methods[0].Source with { Token = 0x06000001 + kind, Name = "Boxes" + kind };
        var helpers = ImmutableArray<IrMethod>.Empty; var helperBindings = ImmutableArray<ObjectMethodBinding>.Empty;
        int result;
        if (kind >= 3)
        {
            var helper = new IrMethod(sourceMethod with { Token = 0x06000030 + kind, Name = "EmptyBorrowPressure" }, [IrType.Location(emptyValue)], IrType.Void,
                [new(0, 0, [], [new IrStatement.Define(new(0, IrType.Location(emptyValue), 0, new IrExpression.Parameter(0))),
                    new IrStatement.Define(new(1, IrType.Int32, 1, new IrExpression.Constant(1))),
                    new IrStatement.Define(new(2, arrayRef, 2, new IrExpression.NewArray(vector, 1)), IrExceptionTarget.MethodExit)], new IrVoidReturn(3))], []);
            helpers = [helper]; helperBindings = [original.Bindings[0] with { Method = IrMethodId.From(helper.Source) }];
            statements.Add(new IrStatement.Invoke(statements.Count * 2, IrMethodId.From(helper.Source), [borrowed]));
            result = D(IrType.Int32, new IrExpression.Constant(0));
        }
        else
        {
            int field = D(IrType.Location(IrType.Int32), new IrExpression.LocationField(number, borrowed));
            int changed = D(IrType.Int32, new IrExpression.Constant(41));
            statements.Add(new IrStatement.StoreLocation(statements.Count * 2, field, changed));
            int other = D(IrType.Location(value), new IrExpression.UnboxLocation(leaf, second), true);
            int nextReference = D(IrType.Location(objectRef), new IrExpression.LocationField(reference, other));
            statements.Add(new IrStatement.StoreLocation(statements.Count * 2, nextReference, boxed));
            _ = D(arrayRef, new IrExpression.NewArray(vector, one), true);
            int current = D(value, new IrExpression.LoadLocation(borrowed), true);
            int observed = D(IrType.Int32, new IrExpression.ValueField(number, current));
            int snapshot = D(IrType.Int32, new IrExpression.ValueField(number, copy));
            int retained = D(objectRef, new IrExpression.ValueField(reference, current));
            int same = D(IrType.Int32, new IrExpression.Compare(Int32Comparison.Equal, retained, array));
            int distinct = D(IrType.Int32, new IrExpression.Compare(Int32Comparison.NotEqual, boxed, second));
            int sum = D(IrType.Int32, new IrExpression.Binary(Int32Operation.AddWrapping, observed, snapshot));
            int withReference = D(IrType.Int32, new IrExpression.Binary(Int32Operation.AddWrapping, sum, same));
            result = D(IrType.Int32, new IrExpression.Binary(Int32Operation.AddWrapping, withReference, distinct));
        }
        var method = new IrMethod(sourceMethod, [IrType.Int32], IrType.Int32,
            [new(0, 0, [], statements.ToImmutable(), new IrReturn(statements.Count * 2, result))], []);
        return (new(new(new(IrMethodId.From(method.Source), [method, .. helpers], catalog)),
            [original.Bindings[0] with { Method = IrMethodId.From(method.Source) }, .. helperBindings]), faultOffset);
    }

    private struct Payload { public int Number; public object Reference; }
    private static int Oracle(int number)
    {
        var input = new Payload { Number = number, Reference = new int[1] }; object box = input, second = input;
        var copy = (Payload)box; ref var borrowed = ref Unsafe.Unbox<Payload>(box); borrowed.Number = 41;
        Unsafe.Unbox<Payload>(second).Reference = box; _ = new int[1];
        return unchecked(borrowed.Number + copy.Number + (ReferenceEquals(borrowed.Reference, input.Reference) ? 1 : 0)
            + (!ReferenceEquals(box, second) ? 1 : 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Boxed_IR_executes_copies_mutation_faults_and_empty_borrows(bool optimized)
    {
        var source = new StringBuilder(); var cases = new List<string>();
        for (int kind = 0; kind < 6; ++kind)
        {
            var (program, faultOffset) = Model(kind); var flow = program.Flow.Flow;
            var artifact = ObjectCppEmitter.EmitBoxes(ValueScalarLowering.LowerBoxes(program));
            string scope = artifact.EntryPoint[..^4] + "_closure";
            string method = "cilica_m_" + flow.Entry.ContentHash + "_" + flow.Entry.Token.ToString("x8", CultureInfo.InvariantCulture);
            uint fault = kind is 1 or 4 ? 4u : kind is 2 or 5 ? 7u : 0u;
            source.Append(artifact.Text).Append(CultureInfo.InvariantCulture, $$"""
                #include <cassert>
                [[gnu::noinline]] static void probe_{{kind}}(std::int32_t number, std::int32_t expected) {
                    using namespace {{scope}};
                    Instance heap; heap.collect_before_allocation = true; heap.object_limit = 5; Context c(heap);
                    LocationArgument argument; argument.bits = std::bit_cast<std::uint32_t>(number);
                    for (int i = 0; i < 3; ++i) {
                        const auto output = {{method}}(c, &argument);
                        assert(c.pending == {{fault}}u && ({{fault}}u != 0 || output == expected));
                        if ({{fault}}u != 0) assert(c.method == {{flow.Entry.Token}}u && c.il == {{faultOffset}}u);
                        assert(c.top == nullptr && c.depth == 0 && c.borrows == nullptr && c.used == 0);
                        c.pending = 0; assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                    }
                    std::int32_t output = 123; std::uint32_t token = 0, il = 0; const char* image = nullptr;
                    assert({{artifact.EntryPoint}}(&output, &token, &il, &image, &number, 1) == {{fault}}u);
                    assert(output == ({{fault}}u == 0 ? expected : 123));
                    if ({{fault}}u != 0) assert(token == {{flow.Entry.Token}}u && il == {{faultOffset}}u);
                }
                """);
            foreach (int number in new[] { 0, -19, int.MaxValue, int.MinValue })
                cases.Add(FormattableString.Invariant($"probe_{kind}({number}, {(kind == 0 ? Oracle(number) : 0)});"));
            if (kind != 0) continue;
            source.Append(CultureInfo.InvariantCulture, $$"""
                [[gnu::noinline]] static std::size_t arena(std::size_t limit, bool success) {
                    using namespace {{scope}};
                    Instance heap; heap.collect_before_allocation = true; Context c(heap, limit); LocationArgument argument;
                    const auto output = {{method}}(c, &argument);
                    assert(c.pending == (success ? 0u : 9u) && (!success || output == {{Oracle(0)}}));
                    assert(c.top == nullptr && c.depth == 0 && c.borrows == nullptr && c.used == 0);
                    c.pending = 0; assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                    return c.peak;
                }
                [[gnu::noinline]] static void allocation_failure() {
                    using namespace {{scope}};
                    Instance heap; heap.object_limit = 1; heap.collect_before_allocation = true; Context c(heap); LocationArgument argument;
                    (void){{method}}(c, &argument);
                    assert(c.pending == 5 && c.method == {{flow.Entry.Token}}u && c.il == 14u);
                    assert(c.top == nullptr && c.depth == 0 && c.borrows == nullptr && c.used == 0);
                    c.pending = 0; assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                    heap.object_limit = 5; assert({{method}}(c, &argument) == {{Oracle(0)}} && c.pending == 0);
                    assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                }
                """);
        }
        source.Append("int main() {\n").AppendJoin('\n', cases).Append("\nconst auto peak = arena(1048576, true); assert(peak > 0); arena(peak, true); arena(peak - 1, false); allocation_failure();\n}\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("native-boxed-ir");
        string cpp = Path.Combine(directory, "boxes.cpp"), executable = Path.Combine(directory, "boxes");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
