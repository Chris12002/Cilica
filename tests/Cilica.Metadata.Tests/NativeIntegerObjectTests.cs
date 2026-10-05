using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeIntegerObjectTests
{
    private static readonly string Hash = new('c', 64);
    private static readonly ObjectTypeId Object = new(Hash, 0x02000002), Node = new(Hash, 0x02000003);
    private static readonly ObjectFieldId Number = new(Node, 0x04000001), Stamp = new(Node, 0x04000002), Link = new(Node, 0x04000003);
    private static readonly IrType NodeRef = IrType.Reference(Node), WideLocation = IrType.Location(IrType.Int64);
    private static IrMethodId Id(int row) => new(Hash, 0x06000000 + row);
    private static string Symbol(int row) => "cilica_m_" + Hash + "_" + (0x06000000 + row).ToString("x8", CultureInfo.InvariantCulture);
    private static string NodeSymbol => "t_" + Hash + "_02000003";
    private static IrStatement.Define D(int id, IrType type, IrExpression expression, bool fault = false) =>
        new(new(id, type, 0, expression), fault ? IrExceptionTarget.MethodExit : null);
    private static IrMethod Method(int row, ImmutableArray<IrType> parameters, IrType result,
        ImmutableArray<IrStatement> statements, int returned = 0, ImmutableArray<IrType> locals = default) =>
        new(new("authored", Hash, Id(row).Token, "Wide" + row), parameters, result,
            [new(0, 0, [], statements, result == IrType.Void ? new IrVoidReturn(1) : new IrReturn(1, returned))], locals.IsDefault ? [] : locals);

    internal static ScalarLocationIrProgram Model(bool checkedSum = false)
    {
        var catalog = new ObjectTypeCatalog([Node], Object,
            [new(new("authored", Hash, Object.Token, "System.Object"), null, TypeAttributes.Public, []),
             new(new("authored", Hash, Node.Token, "Node"), Object, TypeAttributes.Public,
                [new(Number, "Number", FieldAttributes.Public, new ObjectStorage.Int64()),
                 new(Stamp, "Stamp", FieldAttributes.Public | FieldAttributes.InitOnly, new ObjectStorage.Int64()),
                 new(Link, "Link", FieldAttributes.Public, new ObjectStorage.Reference(Node))])]) { Integer64 = true };
        var baseCtor = Method(1, [IrType.Reference(Object)], IrType.Void, []);
        var ctor = Method(2, [NodeRef, IrType.Int64], IrType.Void,
            [D(0, NodeRef, new IrExpression.Parameter(0)), D(1, IrType.Int64, new IrExpression.Parameter(1)),
             new IrStatement.Invoke(0, Id(1), [0]), new IrStatement.StoreField(0, Number, 0, 1),
             new IrStatement.StoreField(0, Stamp, 0, 1), new IrStatement.StoreField(0, Link, 0, 0)]);
        var mutate = Method(3, [WideLocation, IrType.Int64], IrType.Int64,
            [D(0, WideLocation, new IrExpression.Parameter(0)), D(1, IrType.Int64, new IrExpression.Parameter(1)),
             D(2, NodeRef, new IrExpression.NewObject(Id(2), [1]), true),
             D(3, IrType.Int64, new IrExpression.LoadLocation(0), true),
             D(4, IrType.Int64, new IrExpression.Binary64(Int64Operation.AddWrapping, 3, 1)),
             new IrStatement.StoreLocation(0, 0, 4)], 4);
        var worker = Method(4, [IrType.Int64], IrType.Int64,
            [D(0, IrType.Int64, new IrExpression.Parameter(0)),
             D(1, WideLocation, new IrExpression.AddressParameter(0)), D(2, IrType.Int64, new IrExpression.Constant64(1)),
             D(3, IrType.Int64, new IrExpression.Call(Id(3), [1, 2]), true),
             D(4, NodeRef, new IrExpression.NewObject(Id(2), [3]), true),
             D(5, WideLocation, new IrExpression.AddressField(Number, 4), true),
             D(6, IrType.Int64, new IrExpression.Constant64(0x100000001L)),
             D(7, IrType.Int64, new IrExpression.Call(Id(3), [5, 6]), true),
             D(8, WideLocation, new IrExpression.AddressLocal(0)), new IrStatement.InitializeLocation(0, 8),
             new IrStatement.StoreLocation(0, 8, 0), D(9, IrType.Int64, new IrExpression.Constant64(long.MinValue)),
             D(10, IrType.Int64, new IrExpression.Call(Id(3), [8, 9]), true),
             D(11, IrType.Int64, new IrExpression.LoadField(Number, 4), true),
             D(12, IrType.Location(IrType.Int64, true), new IrExpression.AddressField(Stamp, 4), true),
             D(13, IrType.Int64, new IrExpression.LoadLocation(12), true),
             D(14, IrType.Int64, new IrExpression.Local(0)),
             D(15, IrType.Int64, new IrExpression.Binary64(Int64Operation.BitwiseXor, 11, 14)),
             D(16, IrType.Int64, new IrExpression.Binary64(checkedSum ? Int64Operation.AddCheckedSigned : Int64Operation.AddWrapping, 15, 13), checkedSum),
             D(17, IrType.Int32, new IrExpression.Compare(Int32Comparison.LessSigned, 16, 6)),
             D(18, IrType.Int64, new IrExpression.IntegerCast(IntegerCastTarget.UInt64, 17, Checked: true, UnsignedSource: true), true),
             D(19, IrType.Int64, new IrExpression.Binary64(Int64Operation.BitwiseXor, 16, 18))], 19, [IrType.Int64]);
        // Carry full-width values through a branch and both incoming stack slots.
        worker = worker with { Blocks = [worker.Blocks[0] with { Terminator = new IrBranch(1, Int32Comparison.LessSigned, 19, 6, new(1, [19]), new(2, [19])) },
            new(1, 2, [IrType.Int64], [new IrStatement.Define(new(0, IrType.Int64, 2, new IrExpression.StackInput(0))),
                new IrStatement.Define(new(1, IrType.Int64, 2, new IrExpression.Not64(0)))], new IrReturn(3, 1)),
            new(2, 4, [IrType.Int64], [new IrStatement.Define(new(0, IrType.Int64, 4, new IrExpression.StackInput(0)))], new IrReturn(5, 0))] };
        var entry = Method(5, [IrType.Int32], IrType.Int32,
            [D(0, IrType.Int32, new IrExpression.Parameter(0)), D(1, IrType.Int64, new IrExpression.ConvertInteger(IntegerConversion.SignExtend32, 0)),
             D(2, IrType.Int64, new IrExpression.Call(Id(4), [1]), true),
             D(3, IrType.Int32, new IrExpression.ConvertInteger(IntegerConversion.Truncate64, 2))], 3);
        ImmutableArray<IrMethod> methods = [baseCtor, ctor, mutate, worker, entry];
        return new(new(new(Id(5), methods, catalog), [.. methods.Select(m => new ObjectMethodBinding(IrMethodId.From(m.Source),
            m == baseCtor ? Object : Node, m == baseCtor ? ObjectMethodKind.ObjectBaseConstructor : m == ctor ? ObjectMethodKind.Constructor : ObjectMethodKind.Static,
            MethodAttributes.Public | MethodAttributes.HideBySig | (m == baseCtor || m == ctor
                ? MethodAttributes.SpecialName | MethodAttributes.RTSpecialName : MethodAttributes.Static), true))]));
    }

    private sealed class OracleNode(long number) { internal long Number = number; internal readonly long Stamp = number; }
    private static long Oracle(long input, bool checkedSum)
    {
        static long Mutate(ref long slot, long delta) { _ = new OracleNode(delta); return slot = unchecked(slot + delta); }
        long original = input;
        var node = new OracleNode(Mutate(ref input, 1));
        Mutate(ref node.Number, 0x100000001L);
        long local = original; Mutate(ref local, long.MinValue);
        long value = checkedSum ? checked((node.Number ^ local) + node.Stamp) : unchecked((node.Number ^ local) + node.Stamp);
        value ^= value < 0x100000001L ? 1L : 0L;
        return value < 0x100000001L ? ~value : value;
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task Wide_fields_frames_calls_and_borrows_survive_collection(bool optimized, bool checkedSum)
    {
        var program = Model(checkedSum); var artifact = ObjectCppEmitter.EmitLocations(program);
        var source = new StringBuilder(artifact.Text).Append(CultureInfo.InvariantCulture, $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            [[gnu::noinline]] static void execute(std::uint64_t input, std::uint64_t expected, unsigned status) {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                LocationArgument arg; arg.wide_bits = input;
                for (unsigned i = 0; i < 8; ++i) {
                    const auto result = {{Symbol(4)}}(c, &arg);
                    assert(c.pending == status);
                    if (status == 0) assert(std::bit_cast<std::uint64_t>(result) == expected);
                    else assert(c.method == {{Id(4).Token}}u && c.il == 0u);
                    assert(c.top == nullptr && c.borrows == nullptr && c.depth == 0 && c.used == 0);
                    c.pending = 0; assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                }
            }
            [[gnu::noinline]] static void invalid_borrows() {
                Instance heap; Context c(heap);
                Object* owner = allocate<{{NodeSymbol}}>(c, {{NodeSymbol}}_descriptor);
                auto wide = heap_location(c, owner, &{{NodeSymbol}}_descriptor, 0).value;
                std::uint32_t narrow_result = 123;
                assert(load_location_integer(c, wide, narrow_result) == LocationStatus::invalid && narrow_result == 123);
                assert(store_location_integer(c, wide, 99) == LocationStatus::invalid);
                assert(store_location_wide_integer(c, wide, 0xffffffffffffffffull) == LocationStatus::complete);
                std::uint64_t value = 0;
                assert(load_location_wide_integer(c, wide, value) == LocationStatus::complete && value == 0xffffffffffffffffull);
                auto readonly = heap_location(c, owner, &{{NodeSymbol}}_descriptor, 1).value;
                assert(store_location_wide_integer(c, readonly, 1) == LocationStatus::read_only);
                LocationArgument args[2]; args[0].location = readonly; args[1].wide_bits = 7;
                (void){{Symbol(3)}}(c, args); assert(c.pending == 6 && c.used == 0 && c.borrows == nullptr); c.pending = 0;
                Location stale;
                {
                    Frame frame; RootScope roots(c, frame, nullptr, 0);
                    std::uint32_t narrow = 0; std::uint64_t wide_slot = 0;
                    BorrowScope excessive(c, frame, &narrow, 65536, nullptr, &wide_slot, 1);
                    assert(excessive.status == LocationStatus::invalid && c.borrows == nullptr);
                }
                {
                    Frame frame; RootScope roots(c, frame, nullptr, 0);
                    std::uint64_t slot = 0x8000000000000001ull;
                    BorrowScope borrow(c, frame, nullptr, 0, nullptr, &slot, 1);
                    assert(borrow.status == LocationStatus::complete);
                    stale = frame_location(c, borrow.frame.token, LocationOrigin::frame_wide_integer, 0).value;
                    assert(load_location_wide_integer(c, stale, value) == LocationStatus::complete && value == slot);
                    assert(frame_location(c, borrow.frame.token, LocationOrigin::frame_integer, 0).status == LocationStatus::invalid);
                    assert(frame_location(c, borrow.frame.token, LocationOrigin::frame_wide_integer, 1).status == LocationStatus::invalid);
                }
                value = 123; assert(load_location_wide_integer(c, stale, value) == LocationStatus::invalid && value == 123);
                Instance foreign; Context other(foreign);
                assert(load_location_wide_integer(other, wide, value) == LocationStatus::invalid && value == 123);
                assert(collect(c).status == MarkStatus::complete && heap.count == 0);
                heap.object_limit = 0; LocationArgument argument; argument.wide_bits = 1;
                (void){{Symbol(4)}}(c, &argument);
                assert(c.pending == 5 && c.method == {{Id(3).Token}}u && c.used == 0 && c.borrows == nullptr && c.top == nullptr);
            }
            int main() {
            """);
        foreach (long value in new long[] { 0, 1, -1, int.MinValue, int.MaxValue, 0x100000001L, -0x100000001L, long.MinValue, long.MaxValue })
        {
            ulong expected = 0; uint status = 0;
            try { expected = unchecked((ulong)Oracle(value, checkedSum)); } catch (OverflowException) { status = 2; }
            source.Append(CultureInfo.InvariantCulture, $"execute(0x{unchecked((ulong)value):x16}ull, 0x{expected:x16}ull, {status}u);\n");
        }
        source.Append("invalid_borrows(); }\n");
        using var files = new ResolverFiles(); string dir = files.DirectoryPath("integer-objects"), cpp = Path.Combine(dir, "probe.cpp"), exe = Path.Combine(dir, "probe");
        File.WriteAllText(cpp, source.ToString());
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], dir);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], dir);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }

    [Theory]
    [InlineData("legacy")] [InlineData("field-width")] [InlineData("borrow-width")] [InlineData("readonly")]
    public void Wide_object_contract_rejects_profile_and_storage_mismatches(string mutation)
    {
        var wrapper = Model(); var flow = wrapper.Flow.Flow;
        if (mutation == "legacy") flow = flow with { Types = flow.Types with { Integer64 = false } };
        else if (mutation is "field-width" or "readonly")
        {
            var node = flow.Types.Types[1]; var field = node.Fields[0];
            field = mutation == "field-width" ? field with { Storage = new ObjectStorage.Int32() } : field with { Attributes = field.Attributes | FieldAttributes.InitOnly };
            flow = flow with { Types = flow.Types with { Types = flow.Types.Types.SetItem(1, node with { Fields = node.Fields.SetItem(0, field) }) } };
        }
        else
        {
            var method = flow.Methods.Single(m => IrMethodId.From(m.Source) == Id(3));
            flow = flow with { Methods = flow.Methods.Replace(method, method with { Parameters = method.Parameters.SetItem(0, IrType.Location(IrType.Int32)) }) };
        }
        Assert.Throws<ArgumentException>(() => ObjectCppEmitter.EmitLocations(new(wrapper.Flow with { Flow = flow })));
    }
}
