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

public sealed class NativeBoxedPayloadTests
{
    private static readonly string Hash = new('a', 64);
    private static ObjectTypeId Id(int row) => new(Hash, 0x02000000 | row);
    private static string Symbol(ObjectTypeId id, string prefix) => prefix + id.ContentHash + "_" + id.Token.ToString("x8", CultureInfo.InvariantCulture);
    internal static (ScalarLocationIrProgram Program, ValueTypeCatalog Values) Model()
    {
        var obj = Id(2); var leaf = Id(4); var pair = Id(5); var child = Id(6); var empty = Id(7);
        var other = empty with { ContentHash = new string('b', 64) };
        var refs = new ObjectTypeCatalog([child], obj,
            [new(new("fixture", Hash, obj.Token, "Object"), null, TypeAttributes.Public, []),
                new(new("fixture", Hash, child.Token, "Child"), obj, TypeAttributes.Public,
                    [new(new(child, 0x04000005), "Next", FieldAttributes.Public, new ObjectStorage.Reference(obj))])]);
        var values = new ValueTypeCatalog([pair, empty, other], Id(3),
            [new(new("fixture", Hash, leaf.Token, "Leaf"), TypeAttributes.Public | TypeAttributes.Sealed,
                [new(new(leaf, 0x04000001), "Number", FieldAttributes.Public, new ObjectStorage.Int32()),
                    new(new(leaf, 0x04000002), "Reference", FieldAttributes.Public, new ObjectStorage.Reference(child))]),
                new(new("fixture", Hash, pair.Token, "Pair"), TypeAttributes.Public | TypeAttributes.Sealed,
                    [new(new(pair, 0x04000003), "Left", FieldAttributes.Public, new ObjectStorage.Value(leaf)),
                        new(new(pair, 0x04000004), "Right", FieldAttributes.Public, new ObjectStorage.Value(leaf))]),
                new(new("fixture", Hash, empty.Token, "Empty"), TypeAttributes.Public | TypeAttributes.Sealed, []),
                new(new("fixture", other.ContentHash, other.Token, "Empty"), TypeAttributes.Public | TypeAttributes.Sealed, [])], refs);
        var method = new IrMethod(new("fixture", Hash, 0x06000001, "Entry"), [], IrType.Int32,
            [new(0, 0, [], [new IrStatement.Define(new(0, IrType.Int32, 0, new IrExpression.Constant(0)))], new IrReturn(1, 0))], [IrType.Reference(child)]);
        var program = new ScalarLocationIrProgram(new(new(IrMethodId.From(method.Source), [method], refs),
            [new(IrMethodId.From(method.Source), child, ObjectMethodKind.Static, MethodAttributes.Public | MethodAttributes.Static, true)]));
        return (program, values);
    }

    private struct Leaf { public int Number; public object Reference; }
    private struct Pair { public Leaf Left, Right; }
    private struct Empty { }
    private struct OtherEmpty { }
    private static (int Snapshot, int Mutated, int Replaced) Oracle()
    {
        var reference = new object();
        var source = new Pair { Left = new() { Number = 17, Reference = reference }, Right = new() { Number = 23, Reference = reference } };
        object first = source, second = source;
        Assert.NotSame(first, second);
        var copy = (Pair)first;
        ref var borrowed = ref Unsafe.Unbox<Pair>(first); borrowed.Left.Number = 41;
        Assert.Equal(17, source.Left.Number); Assert.Equal(17, ((Pair)second).Left.Number);
        Assert.Same(reference, copy.Left.Reference); int mutated = ((Pair)first).Left.Number;
        var replacement = source; replacement.Left.Number = 77; borrowed = replacement;
        object empty = new Empty(), secondEmpty = new Empty(); Assert.NotSame(empty, secondEmpty);
        Assert.Throws<InvalidCastException>(() => (OtherEmpty)empty);
        object? absent = null; Assert.Throws<NullReferenceException>(() => (Pair)absent!);
        return (copy.Left.Number, mutated, ((Pair)first).Left.Number);
    }

    [Fact]
    public void Runtime_payloads_require_the_same_validated_reference_catalog()
    {
        var (program, values) = Model();
        var changed = values.References with { Types = [values.References.Types[0],
            values.References.Types[1] with { Attributes = TypeAttributes.Public | TypeAttributes.Sealed }] };
        Assert.Contains("share the scalar program's reference catalog", Assert.Throws<ArgumentException>(() =>
            ObjectCppEmitter.EmitBoxedRuntime(program, values with { References = changed })).Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => ObjectCppEmitter.EmitBoxedRuntime(program, values with { Types = default }));
    }

    [Fact]
    public void Combined_class_and_boxed_slots_are_bounded_before_native_source_expansion()
    {
        var (program, original) = Model();
        var classes = ImmutableArray.CreateBuilder<ObjectTypeLayout>(); classes.Add(original.References.Types[0]);
        for (int row = 16; row < 32; row++)
            classes.Add(new(new("fixture", Hash, Id(row).Token, "Class" + row), row == 16 ? Id(2) : Id(row - 1), TypeAttributes.Public,
                [.. Enumerable.Range(0, 64).Select(i => new ObjectField(new(Id(row), 0x04000000 | (1000 + (row - 16) * 64 + i)),
                    "Field" + i, FieldAttributes.Public, new ObjectStorage.Int32()))]));
        var refs = new ObjectTypeCatalog([Id(31)], Id(2), classes.ToImmutable());
        var structs = ImmutableArray.CreateBuilder<ValueTypeLayout>();
        structs.Add(new(new("fixture", Hash, Id(4).Token, "Leaf"), TypeAttributes.Public | TypeAttributes.Sealed,
            [.. Enumerable.Range(1, 30).Select(i => new ObjectField(new(Id(4), 0x04000000 | i), "Field" + i,
                FieldAttributes.Public, new ObjectStorage.Int32()))]));
        for (int row = 5; row < 7; row++)
            structs.Add(new(new("fixture", Hash, Id(row).Token, "Nested" + row), TypeAttributes.Public | TypeAttributes.Sealed,
                [.. Enumerable.Range(row == 5 ? 31 : 131, row == 5 ? 100 : 20).Select(i => new ObjectField(new(Id(row), 0x04000000 | i),
                    "Field" + i, FieldAttributes.Public, new ObjectStorage.Value(Id(row - 1))))]));
        var values = new ValueTypeCatalog([Id(6)], Id(3), structs.ToImmutable(), refs);
        Assert.Equal(63030, ValueLayoutPlanner.Build(values).Sum(p => p.Slots.Length));
        program = new(new(program.Flow.Flow with { Types = refs, Methods = [program.Flow.Flow.Methods[0] with { Locals = [IrType.Reference(Id(31))] }] },
            [program.Flow.Bindings[0] with { Owner = Id(31) }]));
        Assert.Contains("Combined class/boxed native field-slot limit", Assert.Throws<ArgumentException>(() =>
            ObjectCppEmitter.EmitBoxedRuntime(program, values)).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_payloads_preserve_identity_copies_borrows_fault_order_and_collection(bool optimized)
    {
        var (program, values) = Model(); var artifact = ObjectCppEmitter.EmitBoxedRuntime(program, values); var oracle = Oracle();
        string pair = Symbol(Id(5), "box_"), empty = Symbol(Id(7), "box_"), other = Symbol(Id(7) with { ContentHash = new string('b', 64) }, "box_");
        string child = Symbol(Id(6), "t_"), obj = Symbol(Id(2), "t_");
        var source = new StringBuilder(artifact.Text).Append(CultureInfo.InvariantCulture, $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            static void observe(Context& c) noexcept { assert(c.top != nullptr && valid_roots(c)); }
            [[gnu::noinline]] static void identity_copy_and_mutation() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap); c.observe = observe;
                Object* roots[3]{}; Frame frame; RootScope scope(c, frame, roots, 3);
                roots[0] = allocate<{{child}}>(c, {{child}}_descriptor); assert(roots[0]);
                Argument packet[4]; packet[0].bits = 17; packet[1].reference = roots[0]; packet[2].bits = 23; packet[3].reference = roots[0];
                auto first = box_value(c, &{{pair}}_descriptor, packet, 4); roots[1] = first.value;
                auto second = box_value(c, &{{pair}}_descriptor, packet, 4); roots[2] = second.value;
                assert(first.status == BoxStatus::complete && second.status == BoxStatus::complete && first.value != second.value);
                assert({{obj}}_accepts(first.value->type) && !{{child}}_accepts(first.value->type));
                Argument copy[4]; assert(copy_box(c, first.value, &{{pair}}_descriptor, copy, 4) == BoxStatus::complete);
                assert(copy[0].bits == {{oracle.Snapshot}}u && copy[1].reference == roots[0] && copy[2].bits == 23 && copy[3].reference == roots[0]);
                auto checked = unbox(c, first.value, &{{pair}}_descriptor); assert(checked.status == BoxStatus::complete && checked.value == first.value);
                auto field = heap_location(c, checked.value, &{{pair}}_descriptor, 0); assert(field.status == LocationStatus::complete);
                assert(store_location_integer(c, field.value, 41) == LocationStatus::complete);
                std::uint32_t number = 0; assert(load_location_integer(c, field.value, number) == LocationStatus::complete && number == {{oracle.Mutated}}u);
                assert(copy[0].bits == {{oracle.Snapshot}}u && packet[0].bits == 17);
                Argument unchanged[4]; assert(copy_box(c, second.value, &{{pair}}_descriptor, unchanged, 4) == BoxStatus::complete && unchanged[0].bits == 17);
                copy[0].bits = 55; assert(load_location_integer(c, field.value, number) == LocationStatus::complete && number == 41);
                packet[0].bits = 77;
                assert(replace_box(c, first.value, &{{pair}}_descriptor, packet, 4, true) == BoxStatus::read_only);
                assert(load_location_integer(c, field.value, number) == LocationStatus::complete && number == 41);
                assert(replace_box(c, first.value, &{{pair}}_descriptor, packet, 4) == BoxStatus::complete);
                assert(load_location_integer(c, field.value, number) == LocationStatus::complete && number == {{oracle.Replaced}}u);
                auto readonly = heap_location(c, first.value, &{{pair}}_descriptor, 0, true);
                assert(store_location_integer(c, readonly.value, 1) == LocationStatus::read_only);
                assert(c.pending == 0 && c.used == 0 && c.borrows == nullptr);
            }
            [[gnu::noinline]] static void invalid_identity(Context& c, Object* boxed, Object* child, Object* outsider) {
                auto* forged = reinterpret_cast<Object*>(std::uintptr_t{1});
                const auto* unknown = reinterpret_cast<const Descriptor*>(std::uintptr_t{1});
                const Descriptor copied = {{pair}}_descriptor;
                assert(unbox(c, nullptr, unknown).status == BoxStatus::null_reference);
                for (Object* bad : {outsider, forged}) assert(unbox(c, bad, &{{pair}}_descriptor).status == BoxStatus::invalid);
                assert(unbox(c, child, &{{pair}}_descriptor).status == BoxStatus::wrong_type);
                for (const Descriptor* bad : {unknown, &copied, &{{child}}_descriptor})
                    assert(unbox(c, boxed, bad).status == BoxStatus::invalid);
            }
            [[gnu::noinline]] static void invalid_inputs_are_checked_before_access_or_partial_copy() {
                Instance heap, foreign; Context c(heap), elsewhere(foreign);
                Object* roots[2]{}; Frame frame; RootScope scope(c, frame, roots, 2);
                roots[0] = allocate<{{child}}>(c, {{child}}_descriptor);
                Object* outsider = allocate<{{child}}>(elsewhere, {{child}}_descriptor);
                Argument packet[4]; packet[0].bits = 17; packet[1].reference = roots[0]; packet[2].bits = 23; packet[3].reference = roots[0];
                roots[1] = box_value(c, &{{pair}}_descriptor, packet, 4).value; assert(roots[1]);
                auto* forged = reinterpret_cast<Object*>(std::uintptr_t{1});
                const auto* unknown = reinterpret_cast<const Descriptor*>(std::uintptr_t{1});
                invalid_identity(c, roots[1], roots[0], outsider);
                Argument output[4]; output[0].bits = 99; output[1].reference = roots[0]; output[2].bits = 98; output[3].reference = roots[0];
                assert(copy_box(c, nullptr, &{{pair}}_descriptor, output, 4) == BoxStatus::null_reference && output[0].bits == 99);
                assert(copy_box(c, roots[0], &{{pair}}_descriptor, output, 4) == BoxStatus::wrong_type && output[0].bits == 99);
                assert(copy_box(c, roots[1], &{{pair}}_descriptor, nullptr, 4) == BoxStatus::invalid);
                assert(copy_box(c, roots[1], &{{pair}}_descriptor, output, 3) == BoxStatus::invalid && output[0].bits == 99);
                const auto count = heap.count;
                for (Object* bad : {outsider, forged, roots[1]}) {
                    packet[0].bits = 45; packet[3].reference = bad;
                    assert(box_value(c, &{{pair}}_descriptor, packet, 4).status == BoxStatus::invalid && heap.count == count);
                    assert(replace_box(c, roots[1], &{{pair}}_descriptor, packet, 4) == BoxStatus::invalid);
                    assert(*static_cast<std::uint32_t*>(field_address(roots[1], 0)) == 17);
                }
                // A corrupt final contained reference must not expose earlier fields.
                *static_cast<Object**>(field_address(roots[1], 3)) = forged;
                assert(copy_box(c, roots[1], &{{pair}}_descriptor, output, 4) == BoxStatus::invalid);
                assert(output[0].bits == 99 && output[1].reference == roots[0] && output[2].bits == 98 && output[3].reference == roots[0]);
                *static_cast<Object**>(field_address(roots[1], 3)) = roots[0];
                assert(box_value(c, unknown, nullptr, 0).status == BoxStatus::invalid);
                assert(box_value(c, &{{pair}}_descriptor, nullptr, 4).status == BoxStatus::invalid);
                assert(box_value(c, &{{pair}}_descriptor, packet, 3).status == BoxStatus::invalid);
            }
            [[gnu::noinline]] static void borrowed_owner_keeps_the_graph_alive() {
                Instance heap; heap.object_limit = 3; heap.collect_before_allocation = true; Context c(heap);
                Object* source_root = nullptr; Frame frame; RootScope source(c, frame, &source_root, 1);
                source_root = allocate<{{child}}>(c, {{child}}_descriptor); Object* child = source_root;
                Argument packet[4]; packet[0].bits = 17; packet[1].reference = child; packet[2].bits = 23; packet[3].reference = child;
                auto boxed = box_value(c, &{{pair}}_descriptor, packet, 4); assert(boxed.status == BoxStatus::complete);
                Location location = heap_location(c, unbox(c, boxed.value, &{{pair}}_descriptor).value, &{{pair}}_descriptor, 1).value;
                // A class Object field must accept a box, allowing a traced cycle.
                auto back = heap_location(c, child, &{{child}}_descriptor, 0);
                assert(store_location_reference(c, back.value, boxed.value) == LocationStatus::complete);
                source_root = nullptr;
                {
                    Frame borrowed; RootScope keep(c, borrowed, &location.owner, 1);
                    for (int i = 0; i < 20; ++i) {
                        assert(box_value(c, &{{empty}}_descriptor, nullptr, 0).status == BoxStatus::complete);
                        Object* observed = nullptr; assert(load_location_reference(c, location, observed) == LocationStatus::complete && observed == child);
                    }
                    assert(collect(c).status == MarkStatus::complete && heap.count == 2);
                    assert(store_location_reference(c, location, nullptr) == LocationStatus::complete);
                    // The second embedded reference still retains the shared child.
                    assert(collect(c).status == MarkStatus::complete && heap.count == 2);
                    auto right = heap_location(c, location.owner, &{{pair}}_descriptor, 3);
                    assert(store_location_reference(c, right.value, nullptr) == LocationStatus::complete);
                    assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                }
                assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                Object* output = nullptr; assert(load_location_reference(c, location, output) == LocationStatus::invalid);
            }
            [[gnu::noinline]] static void empty_identity_and_budgets() {
                Instance heap; Context c(heap); Object* roots[2]{}; Frame frame; RootScope scope(c, frame, roots, 2);
                auto a = box_value(c, &{{empty}}_descriptor, nullptr, 0); roots[0] = a.value;
                auto b = box_value(c, &{{empty}}_descriptor, nullptr, 0); roots[1] = b.value;
                assert(a.status == BoxStatus::complete && b.status == BoxStatus::complete && a.value != b.value);
                assert(unbox(c, a.value, &{{other}}_descriptor).status == BoxStatus::wrong_type);
                assert(copy_box(c, a.value, &{{empty}}_descriptor, nullptr, 0) == BoxStatus::complete);
                assert(replace_box(c, a.value, &{{empty}}_descriptor, nullptr, 0) == BoxStatus::complete);
                assert(heap_location(c, a.value, &{{empty}}_descriptor, 0).status == LocationStatus::invalid);
                roots[0] = nullptr; roots[1] = nullptr; assert(collect(c).status == MarkStatus::complete && heap.count == 0);
                heap.limit = sizeof({{pair}}) - 1; Argument packet[4];
                assert(box_value(c, &{{pair}}_descriptor, packet, 4).status == BoxStatus::allocation && heap.count == 0 && heap.bytes == 0);
                heap.limit = sizeof({{pair}});
                auto exact = box_value(c, &{{pair}}_descriptor, packet, 4); roots[0] = exact.value;
                assert(exact.status == BoxStatus::complete && heap.bytes == sizeof({{pair}}));
                heap.limit = 1048576; heap.object_limit = 1;
                assert(box_value(c, &{{empty}}_descriptor, nullptr, 0).status == BoxStatus::allocation && heap.count == 1);
                roots[0] = nullptr; assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                assert(box_value(c, &{{empty}}_descriptor, nullptr, 0).status == BoxStatus::complete);
                assert(collect(c).status == MarkStatus::complete && heap.count == 0 && c.used == 0 && c.borrows == nullptr && c.pending == 0);
            }
            [[gnu::noinline]] static void empty_borrow_root() {
                Instance heap; heap.collect_before_allocation = true; heap.object_limit = 2; Context c(heap);
                Object* borrowed = unbox(c, box_value(c, &{{empty}}_descriptor, nullptr, 0).value, &{{empty}}_descriptor).value;
                assert(borrowed != nullptr);
                {
                    Frame frame; RootScope owner(c, frame, &borrowed, 1);
                    for (int i = 0; i < 10; ++i) {
                        assert(box_value(c, &{{empty}}_descriptor, nullptr, 0).status == BoxStatus::complete);
                        assert(unbox(c, borrowed, &{{empty}}_descriptor).status == BoxStatus::complete);
                    }
                    assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                }
                assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                assert(c.top == nullptr && c.depth == 0 && c.used == 0 && c.borrows == nullptr);
            }
            struct Busy { Context* context; Object* value; };
            static bool busy(void* opaque, Object*, bool) noexcept {
                auto& s = *static_cast<Busy*>(opaque); Argument packet[4]; packet[0].bits = 99;
                assert(unbox(*s.context, s.value, &{{pair}}_descriptor).status == BoxStatus::busy);
                assert(copy_box(*s.context, s.value, &{{pair}}_descriptor, packet, 4) == BoxStatus::busy && packet[0].bits == 99);
                assert(replace_box(*s.context, s.value, &{{pair}}_descriptor, packet, 4) == BoxStatus::busy);
                assert(box_value(*s.context, &{{empty}}_descriptor, nullptr, 0).status == BoxStatus::busy);
                return true;
            }
            [[gnu::noinline]] static void marking_preserves_pending_faults() {
                Instance heap; Context c(heap); Argument packet[4];
                Object* value = box_value(c, &{{pair}}_descriptor, packet, 4).value; assert(value);
                Frame frame; RootScope roots(c, frame, &value, 1); Busy state{&c, value};
                c.pending = 2; c.method = 19; c.il = 23; c.image = "saved";
                const char* image = c.image;
                assert(mark_reachable(c, {}, busy, &state).status == MarkStatus::complete);
                assert(c.pending == 2 && c.method == 19 && c.il == 23 && c.image == image && heap.count == 1);
            }
            int main() {
                identity_copy_and_mutation(); invalid_inputs_are_checked_before_access_or_partial_copy();
                borrowed_owner_keeps_the_graph_alive(); empty_identity_and_budgets(); empty_borrow_root(); marking_preserves_pending_faults();
            }
            """);
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("native-boxed-payloads");
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
