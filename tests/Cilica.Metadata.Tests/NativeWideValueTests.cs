using System;
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

public sealed class NativeWideValueTests
{
    private static readonly string Hash = new('d', 64);
    private static readonly ObjectTypeId Object = new(Hash, 0x02000002), ValueBase = new(Hash, 0x02000003), Leaf = new(Hash, 0x02000004), Outer = new(Hash, 0x02000005), Owner = new(Hash, 0x02000006);
    private static readonly ObjectFieldId Number = new(Leaf, 0x04000001), Reference = new(Leaf, 0x04000002), Marker = new(Outer, 0x04000003), Nested = new(Outer, 0x04000004);
    private static IrMethodId Id(int row) => new(Hash, 0x06000000 + row);
    private static string Symbol(int row) => "cilica_m_" + Hash + "_" + Id(row).Token.ToString("x8", CultureInfo.InvariantCulture);
    private static string BoxSymbol => "box_" + Hash + "_02000005";
    internal static BoxedValueIrProgram Model()
    {
        var vector = ObjectTypeId.Vector(Object, ObjectArrayElement.Int32);
        var objectRef = IrType.Reference(Object); var arrayRef = IrType.Reference(vector);
        var leaf = IrType.Value(Leaf); var outer = IrType.Value(Outer); var borrow = IrType.Location(outer);
        var catalog = new ValueTypeCatalog([Outer], ValueBase,
            [new(new("authored", Hash, Leaf.Token, "Leaf"), TypeAttributes.Public | TypeAttributes.Sealed,
                [new(Number, "Number", FieldAttributes.Public, new ObjectStorage.Int64()), new(Reference, "Reference", FieldAttributes.Public, new ObjectStorage.Reference(Object))]),
             new(new("authored", Hash, Outer.Token, "Outer"), TypeAttributes.Public | TypeAttributes.Sealed,
                [new(Marker, "Marker", FieldAttributes.Public, new ObjectStorage.Int32()), new(Nested, "Nested", FieldAttributes.Public, new ObjectStorage.Value(Leaf))])],
            new([vector], Object, [new(new("authored", Hash, Object.Token, "System.Object"), null, TypeAttributes.Public, [])]) { Arrays = [vector], Integer64 = true });
        IrMethod Method(int row, ImmutableArray<IrType> parameters, IrType result, ImmutableArray<IrStatement> statements, int returned) =>
            new(new("authored", Hash, Id(row).Token, "WideValue" + row), parameters, result,
                [new(0, 0, [], statements, new IrReturn(1, returned))], []);
        var make = Method(1, [IrType.Int64], outer,
            [D(0, IrType.Int64, new IrExpression.Parameter(0)), D(1, leaf, new IrExpression.DefaultValue(Leaf)),
             D(2, leaf, new IrExpression.WithValueField(Number, 1, 0)), D(3, IrType.Int32, new IrExpression.Constant(1)),
             D(4, arrayRef, new IrExpression.NewArray(vector, 3), true), D(5, leaf, new IrExpression.WithValueField(Reference, 2, 4)),
             D(6, outer, new IrExpression.DefaultValue(Outer)), D(7, outer, new IrExpression.WithValueField(Nested, 6, 5)),
             D(8, IrType.Int32, new IrExpression.Constant(7)), D(9, outer, new IrExpression.WithValueField(Marker, 7, 8))], 9);
        var change = Method(2, [borrow, IrType.Int64], outer,
            [D(0, borrow, new IrExpression.Parameter(0)), D(1, IrType.Int64, new IrExpression.Parameter(1)),
             D(2, IrType.Location(leaf), new IrExpression.LocationField(Nested, 0)),
             D(3, IrType.Location(IrType.Int64), new IrExpression.LocationField(Number, 2)),
             D(4, IrType.Int64, new IrExpression.LoadLocation(3), true),
             D(5, IrType.Int64, new IrExpression.Binary64(Int64Operation.AddWrapping, 4, 1)), new IrStatement.StoreLocation(0, 3, 5),
             D(6, IrType.Int32, new IrExpression.Constant(1)), D(7, arrayRef, new IrExpression.NewArray(vector, 6), true),
             D(8, outer, new IrExpression.LoadLocation(0), true)], 8);
        var worker = Method(3, [IrType.Int64], IrType.Int64,
            [D(0, IrType.Int64, new IrExpression.Parameter(0)), D(1, outer, new IrExpression.Call(Id(1), [0]), true),
             D(2, objectRef, new IrExpression.BoxValue(Outer, 1), true), D(3, objectRef, new IrExpression.BoxValue(Outer, 1), true),
             D(4, outer, new IrExpression.UnboxValue(Outer, 2), true), D(5, borrow, new IrExpression.UnboxLocation(Outer, 2), true),
             D(6, IrType.Int64, new IrExpression.Constant64(long.MaxValue)), D(7, outer, new IrExpression.Call(Id(2), [5, 6]), true),
             D(8, borrow, new IrExpression.UnboxLocation(Outer, 3), true), D(9, IrType.Location(leaf), new IrExpression.LocationField(Nested, 8)),
             D(10, IrType.Location(objectRef), new IrExpression.LocationField(Reference, 9)), new IrStatement.StoreLocation(0, 10, 2),
             D(11, leaf, new IrExpression.ValueField(Nested, 7)), D(12, IrType.Int64, new IrExpression.ValueField(Number, 11)),
             D(13, leaf, new IrExpression.ValueField(Nested, 4)), D(14, IrType.Int64, new IrExpression.ValueField(Number, 13)),
             D(15, objectRef, new IrExpression.ValueField(Reference, 11)), D(16, objectRef, new IrExpression.ValueField(Reference, 13)),
             D(17, IrType.Int32, new IrExpression.Compare(Int32Comparison.Equal, 15, 16)),
             D(18, IrType.Int32, new IrExpression.Compare(Int32Comparison.NotEqual, 2, 3)),
             D(19, IrType.Int32, new IrExpression.ValueField(Marker, 7)), D(20, IrType.Int32, new IrExpression.Binary(Int32Operation.AddWrapping, 17, 18)),
             D(21, IrType.Int32, new IrExpression.Binary(Int32Operation.AddWrapping, 20, 19)),
             D(22, IrType.Int64, new IrExpression.ConvertInteger(IntegerConversion.SignExtend32, 21)),
             D(23, IrType.Int64, new IrExpression.Binary64(Int64Operation.BitwiseXor, 12, 14)),
             D(24, IrType.Int64, new IrExpression.Binary64(Int64Operation.AddWrapping, 23, 22))], 24);
        var entry = Method(4, [IrType.Int32], IrType.Int32,
            [D(0, IrType.Int32, new IrExpression.Parameter(0)), D(1, IrType.Int64, new IrExpression.ConvertInteger(IntegerConversion.SignExtend32, 0)),
             D(2, IrType.Int64, new IrExpression.Call(Id(3), [1]), true), D(3, IrType.Int32, new IrExpression.ConvertInteger(IntegerConversion.Truncate64, 2))], 3);
        ImmutableArray<IrMethod> methods = [make, change, worker, entry];
        return new(new(new(Id(4), methods, catalog)), [.. methods.Select(m => new ObjectMethodBinding(IrMethodId.From(m.Source), Owner,
            ObjectMethodKind.Static, MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, true))]);
    }
    private static IrStatement.Define D(int id, IrType type, IrExpression expression, bool fault = false) =>
        new(new(id, type, 0, expression), fault ? IrExceptionTarget.MethodExit : null);

    private struct LeafOracle { public long Number; public object Reference; }
    private struct OuterOracle { public int Marker; public LeafOracle Nested; }
    private static long Oracle(long number)
    {
        static OuterOracle Make(long n) => new() { Marker = 7, Nested = new() { Number = n, Reference = new int[1] } };
        static OuterOracle Change(ref OuterOracle value, long delta) { value.Nested.Number = unchecked(value.Nested.Number + delta); _ = new int[1]; return value; }
        var input = Make(number); object box = input, second = input;
        var copy = (OuterOracle)box; var current = Change(ref Unsafe.Unbox<OuterOracle>(box), long.MaxValue);
        Unsafe.Unbox<OuterOracle>(second).Nested.Reference = box;
        return unchecked((current.Nested.Number ^ copy.Nested.Number) + current.Marker
            + (ReferenceEquals(current.Nested.Reference, copy.Nested.Reference) ? 1 : 0) + (!ReferenceEquals(box, second) ? 1 : 0));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Nested_wide_value_returns_boxes_and_borrows_match_dotnet(bool optimized)
    {
        var program = Model(); var lowered = ValueScalarLowering.LowerBoxes(program); var artifact = ObjectCppEmitter.EmitBoxes(lowered);
        var source = new StringBuilder(artifact.Text).Append(CultureInfo.InvariantCulture, $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            [[gnu::noinline]] static void execute(std::uint64_t number, std::uint64_t expected) {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                LocationArgument arg; arg.wide_bits = number;
                for (unsigned i = 0; i < 10; ++i) {
                    const auto result = {{Symbol(3)}}(c, &arg);
                    assert(c.pending == 0 && std::bit_cast<std::uint64_t>(result) == expected);
                    assert(c.used == 0 && c.top == nullptr && c.borrows == nullptr && c.depth == 0);
                    assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                }
            }
            [[gnu::noinline]] static void packet_checks() {
                Instance heap; Context c(heap); Argument source[3], output[3];
                source[0].bits = 7; source[1].wide_bits = 0x8000000000000001ull;
                auto boxed = box_value(c, &{{BoxSymbol}}_descriptor, source, 3);
                assert(boxed.status == BoxStatus::complete);
                assert(copy_box(c, boxed.value, &{{BoxSymbol}}_descriptor, output, 3) == BoxStatus::complete);
                assert(output[0].bits == 7 && output[1].wide_bits == 0x8000000000000001ull && output[2].reference == nullptr);
                source[0].bits = 99; source[1].wide_bits = 0xffffffffffffffffull;
                source[2].reference = reinterpret_cast<Object*>(std::uintptr_t{1});
                assert(replace_box(c, boxed.value, &{{BoxSymbol}}_descriptor, source, 3) == BoxStatus::invalid);
                assert(copy_box(c, boxed.value, &{{BoxSymbol}}_descriptor, output, 3) == BoxStatus::complete);
                assert(output[0].bits == 7 && output[1].wide_bits == 0x8000000000000001ull);
                source[2].reference = nullptr;
                assert(replace_box(c, boxed.value, &{{BoxSymbol}}_descriptor, source, 3) == BoxStatus::complete);
                assert(copy_box(c, boxed.value, &{{BoxSymbol}}_descriptor, output, 3) == BoxStatus::complete && output[1].wide_bits == 0xffffffffffffffffull);
                assert(collect(c).status == MarkStatus::complete && heap.count == 0);
                heap.object_limit = 1; LocationArgument arg;
                (void){{Symbol(3)}}(c, &arg);
                assert(c.pending == 5 && c.used == 0 && c.top == nullptr && c.borrows == nullptr);
                c.pending = 0; assert(collect(c).status == MarkStatus::complete && heap.count == 0);
            }
            int main() {
            """);
        foreach (long value in new long[] { 0, 1, -1, int.MinValue, int.MaxValue, 0x100000001L, -0x100000001L, long.MinValue, long.MaxValue })
            source.Append(CultureInfo.InvariantCulture, $"execute(0x{unchecked((ulong)value):x16}ull, 0x{unchecked((ulong)Oracle(value)):x16}ull);\n");
        source.Append("packet_checks(); }\n");
        using var files = new ResolverFiles(); string dir = files.DirectoryPath("wide-values"), cpp = Path.Combine(dir, "probe.cpp"), exe = Path.Combine(dir, "probe");
        File.WriteAllText(cpp, source.ToString());
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], dir);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], dir); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }

    [Fact]
    public void Wide_value_catalog_requires_explicit_admission_and_preserves_leaf_width()
    {
        var program = Model(); var catalog = program.Flow.Flow.Types;
        var slots = ValueLayoutPlanner.Build(catalog).Single(p => p.Type == Outer).Slots;
        Assert.IsType<ObjectStorage.Int32>(slots[0].Storage); Assert.IsType<ObjectStorage.Int64>(slots[1].Storage);
        Assert.IsType<ObjectStorage.Reference>(slots[2].Storage);
        Assert.Throws<ArgumentException>(() => ValueLayoutPlanner.Build(catalog with { References = catalog.References with { Integer64 = false } }));
        var scalar = ValueScalarLowering.LowerBoxes(program);
        var worker = scalar.Flow.Flow.Flow.Methods.Single(m => IrMethodId.From(m.Source) == Id(3));
        var block = worker.Blocks[0]; var box = block.Statements.OfType<IrStatement.Define>().First(d => d.Value.Expression is IrExpression.BoxScalars);
        var expression = (IrExpression.BoxScalars)box.Value.Expression;
        var invalid = box with { Value = box.Value with { Expression = expression with { Values = expression.Values.SetItem(1, expression.Values[0]) } } };
        worker = worker with { Blocks = [block with { Statements = block.Statements.Replace(box, invalid) }] };
        var flow = scalar.Flow.Flow.Flow with { Methods = scalar.Flow.Flow.Flow.Methods.Replace(scalar.Flow.Flow.Flow.Methods.Single(m => IrMethodId.From(m.Source) == Id(3)), worker) };
        Assert.Throws<ArgumentException>(() => ObjectCppEmitter.EmitBoxes(scalar with { Flow = new(scalar.Flow.Flow with { Flow = flow }) }));
    }
}
