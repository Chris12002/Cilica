using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Metadata;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ValueVectorIrTests
{
    internal static ValueArrayIrProgram Model(bool empty = false)
    {
        var references = ValueVectorStorageTests.Catalog() with { ValueArrays = [] };
        var array = references.Arrays[0]; var value = array.ArrayElement!.Type!; string hash = value.ContentHash;
        var number = new ObjectFieldId(value, 0x04000001); var reference = new ObjectFieldId(value, 0x04000002); var wide = new ObjectFieldId(value, 0x04000003);
        var values = new ValueTypeCatalog([value], new(hash, 0x02000005),
            [new(new("fixture", hash, value.Token, "Example.Value"), TypeAttributes.Public | TypeAttributes.Sealed,
                empty ? [] : [new(number, "Number", FieldAttributes.Public, new ObjectStorage.Int32()),
                    new(reference, "Reference", FieldAttributes.Public, new ObjectStorage.Reference(references.ObjectBase)),
                    new(wide, "Wide", FieldAttributes.Public, new ObjectStorage.Int64())])], references);
        var statements = ImmutableArray.CreateBuilder<IrStatement>();
        void D(int id, IrType type, IrExpression expression, bool fault = false) => statements.Add(new IrStatement.Define(new(id, type, id, expression), fault ? IrExceptionTarget.MethodExit : null));
        D(0, IrType.Int32, new IrExpression.Parameter(0)); D(1, IrType.Int32, new IrExpression.Parameter(1)); D(2, IrType.Int32, new IrExpression.Parameter(2));
        D(3, IrType.Reference(array), new IrExpression.NewArray(array, 0), true);
        D(4, IrType.Value(value), new IrExpression.DefaultValue(value));
        int returned;
        if (empty)
        {
            statements.Add(new IrStatement.StoreElement(5, array, 3, 1, 4));
            D(5, IrType.Value(value), new IrExpression.LoadElement(array, 3, 1), true);
            D(6, IrType.Location(IrType.Value(value)), new IrExpression.AddressElement(array, 3, 1), true);
            statements.Add(new IrStatement.InitializeLocation(7, 6));
            D(7, IrType.Int32, new IrExpression.ArrayLength(3), true); returned = 7;
        }
        else
        {
            D(5, IrType.Value(value), new IrExpression.WithValueField(number, 4, 2));
            D(6, IrType.Value(value), new IrExpression.WithValueField(reference, 5, 3));
            D(7, IrType.Int64, new IrExpression.Constant64(unchecked((long)0x8000000000000001)));
            D(8, IrType.Value(value), new IrExpression.WithValueField(wide, 6, 7));
            statements.Add(new IrStatement.StoreElement(9, array, 3, 1, 8));
            D(9, IrType.Value(value), new IrExpression.LoadElement(array, 3, 1), true);
            D(10, IrType.Location(IrType.Value(value)), new IrExpression.AddressElement(array, 3, 1), true);
            D(11, IrType.Location(IrType.Int32), new IrExpression.LocationField(number, 10));
            D(12, IrType.Int32, new IrExpression.Constant(1));
            D(13, IrType.Int32, new IrExpression.Binary(Int32Operation.AddWrapping, 2, 12));
            statements.Add(new IrStatement.StoreLocation(14, 11, 13));
            D(14, IrType.Reference(array), new IrExpression.NewArray(array, 0), true);
            D(15, IrType.Int32, new IrExpression.ValueField(number, 9));
            D(16, IrType.Value(value), new IrExpression.LoadLocation(10), true);
            D(17, IrType.Int32, new IrExpression.ValueField(number, 16));
            D(18, IrType.Reference(references.ObjectBase), new IrExpression.ValueField(reference, 9));
            D(19, IrType.Int32, new IrExpression.Compare(Int32Comparison.Equal, 18, 3));
            D(20, IrType.Int64, new IrExpression.ValueField(wide, 16));
            D(21, IrType.Int32, new IrExpression.Compare(Int32Comparison.Equal, 20, 7));
            D(22, IrType.Int32, new IrExpression.Binary(Int32Operation.AddWrapping, 15, 17));
            D(23, IrType.Int32, new IrExpression.Binary(Int32Operation.AddWrapping, 22, 19));
            D(24, IrType.Int32, new IrExpression.Binary(Int32Operation.AddWrapping, 23, 21)); returned = 24;
        }
        var method = new IrMethod(new("fixture", hash, empty ? 0x06000002 : 0x06000001, "Driver::Run"), [IrType.Int32, IrType.Int32, IrType.Int32], IrType.Int32,
            [new(0, 0, [], statements.ToImmutable(), new IrReturn(returned + 1, returned))], []);
        return new(new(new(IrMethodId.From(method.Source), [method], values)),
            [new(IrMethodId.From(method.Source), new(hash, 0x02000004), ObjectMethodKind.Static, MethodAttributes.Public | MethodAttributes.Static, true)]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Logical_arrays_lower_to_checked_snapshots_and_exact_storage(bool empty)
    {
        var program = Model(empty); ValueArrayIrValidator.Validate(program);
        var lowered = ValueScalarLowering.LowerArrays(program); ScalarLocationIrValidator.Validate(lowered);
        var shape = Assert.Single(lowered.Flow.Flow.Types.ValueArrays);
        Assert.Equal(empty ? 1 : 3, shape.Slots.Length);
        Assert.DoesNotContain(lowered.Flow.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values), v => v.Type.Kind == IrTypeKind.Value);
        Assert.Contains(lowered.Flow.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values), v => v.Expression is IrExpression.AddressArraySlot);
        var badShape = lowered.Flow.Flow.Types with { ValueArrays = [shape with { Slots = [new ObjectStorage.Int64()] }] };
        var logical = program.Flow.Flow.Types with { References = badShape };
        Assert.Throws<ArgumentException>(() => ValueLayoutPlanner.Build(logical));
    }

    private struct OracleValue { public int Number; public object? Reference; public long Wide; }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Logical_element_copies_borrows_empty_values_and_self_references_match_dotnet(bool optimized)
    {
        var source = new StringBuilder();
        for (int empty = 0; empty < 2; ++empty)
        {
            var program = Model(empty != 0); var artifact = ObjectCppEmitter.EmitLocations(ValueScalarLowering.LowerArrays(program)); source.Append(artifact.Text);
            string method = "cilica_m_" + program.Flow.Flow.Entry.ContentHash + "_" + program.Flow.Flow.Entry.Token.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
            source.Append(System.Globalization.CultureInfo.InvariantCulture, $$"""
                #include <cassert>
                static void execute_{{empty}}(std::int32_t length, std::int32_t index, std::int32_t number, std::uint32_t fault, std::int32_t expected) {
                    using namespace {{artifact.EntryPoint[..^4]}}_closure;
                    Instance heap; heap.collect_before_allocation = true; Context c(heap);
                    LocationArgument args[3]; args[0].bits = std::bit_cast<std::uint32_t>(length); args[1].bits = std::bit_cast<std::uint32_t>(index); args[2].bits = std::bit_cast<std::uint32_t>(number);
                    for (int i = 0; i < 3; ++i) {
                        const auto result = {{method}}(c, args); assert(c.pending == fault && (fault != 0 || result == expected));
                        assert(c.top == nullptr && c.borrows == nullptr && c.used == 0 && c.depth == 0);
                        c.pending = 0; assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                    }
                }
                """);
        }
        source.Append("int main() {\n");
        for (int empty = 0; empty < 2; ++empty) foreach (int length in new[] { -1, 0, 1, 3 }) foreach (int index in new[] { -1, 0, 1, 3 }) foreach (int number in new[] { int.MinValue, 0, int.MaxValue })
        {
            int fault = 0, expected = 0;
            try {
                var array = new OracleValue[length]; array[index] = new() { Number = number, Reference = array, Wide = unchecked((long)0x8000000000000001) };
                var copy = array[index]; ref var borrow = ref array[index]; borrow.Number = unchecked(number + 1);
                expected = empty != 0 ? length : unchecked(copy.Number + borrow.Number + (ReferenceEquals(copy.Reference, array) ? 1 : 0) + (borrow.Wide == unchecked((long)0x8000000000000001) ? 1 : 0));
            }
            catch (OverflowException) { fault = 2; } catch (IndexOutOfRangeException) { fault = 10; }
            source.Append(System.Globalization.CultureInfo.InvariantCulture, $"execute_{empty}({length}, {index}, {number}, {fault}u, {expected});\n");
        }
        source.Append("}\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("value-vector-ir");
        string cpp = Path.Combine(directory, "probe.cpp"), executable = Path.Combine(directory, "probe"); File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stderr);
    }
}
