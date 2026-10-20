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

public sealed class ValueVectorBorrowTests
{
    private static ScalarLocationIrProgram Program()
    {
        var catalog = ValueVectorStorageTests.Catalog(); var vector = catalog.Arrays[0]; string hash = catalog.ObjectBase.ContentHash;
        var method = new IrMethod(new("fixture", hash, 0x06000001, "Driver::Run"), [IrType.Int32, IrType.Int32, IrType.Int32], IrType.Int32,
            [new(0, 0, [], [
                Def(0, IrType.Int32, new IrExpression.Parameter(0)),
                Def(1, IrType.Int32, new IrExpression.Parameter(1)),
                Def(2, IrType.Int32, new IrExpression.Parameter(2)),
                Def(3, IrType.Reference(vector), new IrExpression.NewArray(vector, 0), true),
                Def(4, IrType.Location(IrType.Int32), new IrExpression.AddressArraySlot(vector, 3, 1, 0), true),
                new IrStatement.StoreLocation(5, 4, 2),
                Def(5, IrType.Reference(vector), new IrExpression.NewArray(vector, 0), true),
                Def(6, IrType.Int32, new IrExpression.LoadLocation(4), true)
            ], new IrReturn(8, 6))], []);
        return new(new(new(IrMethodId.From(method.Source), [method], catalog),
            [new(IrMethodId.From(method.Source), new(hash, 0x02000004), ObjectMethodKind.Static, MethodAttributes.Public | MethodAttributes.Static, true)]));
    }
    private static IrStatement.Define Def(int id, IrType type, IrExpression expression, bool fault = false) =>
        new(new(id, type, id, expression), fault ? IrExceptionTarget.MethodExit : null);

    [Fact]
    public void Scalar_borrow_rejects_bad_slot_kind_index_carrier_and_readonly_store()
    {
        var p = Program(); ScalarLocationIrValidator.Validate(p); var method = p.Flow.Flow.Methods[0];
        var address = (IrExpression.AddressArraySlot)method.Blocks[0].Values.Single(v => v.Id == 4).Expression;
        foreach (var bad in new[] {
            Def(4, IrType.Location(IrType.Int32), address with { Slot = -1 }, true),
            Def(4, IrType.Location(IrType.Int32), address with { Slot = 5 }, true),
            Def(4, IrType.Location(IrType.Int32), address with { Slot = 1 }, true),
            Def(4, IrType.Location(IrType.Int32), address with { Index = 3 }, true),
            Def(4, IrType.Location(IrType.Int32), address with { Array = 2 }, true),
            Def(4, IrType.Location(IrType.Int32, true), address, true),
            Def(4, IrType.Location(IrType.Int32), address)
        }) {
            var changed = method with { Blocks = [method.Blocks[0] with { Statements = method.Blocks[0].Statements.SetItem(4, bad) }] };
            Assert.Throws<ArgumentException>(() => ScalarLocationIrValidator.Validate(new(p.Flow with { Flow = p.Flow.Flow with { Methods = [changed] } })));
        }
        var roots = ObjectRootAssignment.AssignLocations(p).Single();
        Assert.Contains(roots.Slots, s => s.Kind == ObjectRootKind.Value && s.Index == 4 && s.LocationOwner);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Integer_array_carrier_construction_preserves_shape_fault_order_and_readonly(bool optimized)
    {
        var original = Program();
        var vector = ObjectTypeId.Vector(original.Flow.Flow.Types.ObjectBase, ObjectArrayElement.Int32);
        var catalog = original.Flow.Flow.Types with
        { Arrays = [.. original.Flow.Flow.Types.Arrays, vector], Roots = [.. original.Flow.Flow.Types.Roots, vector] };
        var entry = original.Flow.Flow.Methods[0];
        entry = entry with { Blocks = [entry.Blocks[0] with
        { Statements = entry.Blocks[0].Statements.Add(Def(7, IrType.Reference(vector), new IrExpression.NewArray(vector, 0), true)) }] };
        var program = original with { Flow = original.Flow with { Flow = original.Flow.Flow with { Types = catalog, Methods = [entry] } } };
        var artifact = ObjectCppEmitter.EmitLocations(program);
        string symbol = "t_" + catalog.ObjectBase.ContentHash + "_" + catalog.ObjectBase.Token.ToString("x8", System.Globalization.CultureInfo.InvariantCulture) + "_vector_i4";
        string source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                for (std::uint32_t length : {0u, 1u, 7u}) {
                    Instance heap; Context c(heap);
                    Object* owner = allocate_array(c, {{symbol}}_descriptor, length);
                    assert(owner);
                    assert(array_location(c, owner, &{{symbol}}_descriptor, length, 1).status == LocationStatus::index_bounds);
                    if (length == 0) continue;
                    assert(array_location(c, owner, &{{symbol}}_descriptor, 0, 1).status == LocationStatus::invalid);
                    auto frozen = array_location(c, owner, &{{symbol}}_descriptor, 0, 0, true);
                    assert(frozen.status == LocationStatus::complete && frozen.value.read_only);
                    assert(store_location_integer(c, frozen.value, 17) == LocationStatus::read_only);
                    std::uint32_t value = 99;
                    assert(load_location_integer(c, frozen.value, value) == LocationStatus::complete && value == 0);
                    auto* array = reinterpret_cast<ArrayObject*>(owner);
                    auto* buffer = array->integers; array->integers = nullptr;
                    assert(array_location(c, owner, &{{symbol}}_descriptor, length, 1).status == LocationStatus::invalid);
                    array->integers = buffer;
                    auto good = array_location(c, owner, &{{symbol}}_descriptor, length - 1, 0);
                    assert(good.status == LocationStatus::complete);
                    assert(store_location_integer(c, good.value, 23) == LocationStatus::complete);
                    assert(load_location_integer(c, good.value, value) == LocationStatus::complete && value == 23);
                }
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("integer-array-carriers");
        string cpp = Path.Combine(directory, "probe.cpp"), executable = Path.Combine(directory, "probe");
        File.WriteAllText(cpp, source, new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-DCILICA_VERIFY_INTERNAL_REFERENCES=1", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stderr);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Scalar_array_borrows_preserve_fields_owner_lifetime_and_ordered_faults(bool optimized)
    {
        var program = Program(); var artifact = ObjectCppEmitter.EmitLocations(program);
        string hash = program.Flow.Flow.Entry.ContentHash, type = "t_" + hash + "_02000002";
        string vector = type + "_vector_value_t_" + hash + "_02000003", method = "cilica_m_" + hash + "_06000001";
        var source = new StringBuilder(artifact.Text).Append(System.Globalization.CultureInfo.InvariantCulture, $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            static void execute(std::int32_t length, std::int32_t index, std::int32_t value, unsigned fault) {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                LocationArgument arguments[3]; arguments[0].bits = std::bit_cast<std::uint32_t>(length);
                arguments[1].bits = std::bit_cast<std::uint32_t>(index); arguments[2].bits = std::bit_cast<std::uint32_t>(value);
                for (int repeat = 0; repeat < 3; ++repeat) {
                    const auto result = {{method}}(c, arguments);
                    assert(c.pending == fault && (fault != 0 || result == value));
                    assert(c.top == nullptr && c.borrows == nullptr && c.used == 0 && c.depth == 0);
                    c.pending = 0; assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                }
            }
            static void carriers() {
                Instance heap; Context c(heap); Object* owner = allocate_array(c, {{vector}}_descriptor, 2);
                Frame frame; RootScope root(c, frame, &owner, 1);
                auto i = array_location(c, owner, &{{vector}}_descriptor, 1, 0);
                auto r = array_location(c, owner, &{{vector}}_descriptor, 1, 1);
                auto w = array_location(c, owner, &{{vector}}_descriptor, 1, 2);
                assert(i.status == LocationStatus::complete && r.status == i.status && w.status == i.status);
                assert(store_location_integer(c, i.value, 0xffffffffu) == LocationStatus::complete);
                assert(store_location_wide_integer(c, w.value, 0x8000000000000001ull) == LocationStatus::complete);
                Object* object = allocate<{{type}}>(c, {{type}}_descriptor);
                assert(store_location_reference(c, r.value, object) == LocationStatus::complete);
                assert(collect(c).status == MarkStatus::complete && heap.count == 2);
                std::uint32_t integer = 0; std::uint64_t wide = 0; Object* reference = nullptr;
                assert(load_location_integer(c, i.value, integer) == LocationStatus::complete && integer == 0xffffffffu);
                assert(load_location_wide_integer(c, w.value, wide) == LocationStatus::complete && wide == 0x8000000000000001ull);
                assert(load_location_reference(c, r.value, reference) == LocationStatus::complete && reference == object);
                auto bad = r.value; bad.read_only = true;
                assert(store_location_reference(c, bad, nullptr) == LocationStatus::read_only);
                bad = i.value; bad.element = 2; assert(load_location_integer(c, bad, integer) == LocationStatus::invalid);
                bad = i.value; bad.slot = 5; assert(load_location_integer(c, bad, integer) == LocationStatus::invalid);
                bad = i.value; bad.frame = 1; assert(load_location_integer(c, bad, integer) == LocationStatus::invalid);
                assert(load_location_integer(c, w.value, integer) == LocationStatus::invalid);
                assert(array_location(c, nullptr, &{{vector}}_descriptor, 0xffffffffu, 0).status == LocationStatus::null_reference);
                assert(array_location(c, owner, &{{vector}}_descriptor, 0xffffffffu, 0).status == LocationStatus::index_bounds);
                assert(array_location(c, owner, &{{vector}}_descriptor, 1, 5).status == LocationStatus::invalid);
                assert(array_location(c, owner, &{{vector}}_descriptor, 2, 5).status == LocationStatus::index_bounds);
                const auto frozen = array_location(c, owner, &{{vector}}_descriptor, 1, 2, true);
                assert(frozen.status == LocationStatus::complete && frozen.value.read_only);
                assert(store_location_wide_integer(c, frozen.value, 17) == LocationStatus::read_only);
                assert(load_location_wide_integer(c, w.value, wide) == LocationStatus::complete && wide == 0x8000000000000001ull);
                Instance foreign; Context other(foreign);
                assert(load_location_integer(other, i.value, integer) == LocationStatus::invalid);
                Object* foreign_object = allocate<{{type}}>(other, {{type}}_descriptor);
                assert(store_location_reference(c, r.value, foreign_object) == LocationStatus::invalid);
                owner = nullptr; assert(collect(c).status == MarkStatus::complete && heap.count == 0);
                assert(load_location_integer(c, i.value, integer) == LocationStatus::invalid);
            }
            int main() { carriers();
            """);
        foreach (int length in new[] { -1, 0, 1, 3 }) foreach (int index in new[] { -1, 0, 1, 3 }) foreach (int value in new[] { int.MinValue, 0, int.MaxValue })
        {
            int fault = 0;
            try { var array = new long[length]; array[index] = value; Assert.Equal(value, array[index]); }
            catch (OverflowException) { fault = 2; }
            catch (IndexOutOfRangeException) { fault = 10; }
            source.Append(System.Globalization.CultureInfo.InvariantCulture, $"execute({length}, {index}, {value}, {fault}u);\n");
        }
        source.Append("}\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("value-vector-borrows");
        string cpp = Path.Combine(directory, "probe.cpp"), executable = Path.Combine(directory, "probe");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-DCILICA_VERIFY_INTERNAL_REFERENCES=1", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stderr);
    }
}
