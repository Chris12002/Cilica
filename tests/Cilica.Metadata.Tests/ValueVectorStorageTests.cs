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

public sealed class ValueVectorStorageTests
{
    private static readonly string Hash = new('a', 64);
    private static readonly ObjectTypeId Object = new(Hash, 0x02000002), Value = new(Hash, 0x02000003);
    private static readonly ObjectTypeId Vector = ObjectTypeId.Vector(Object, ObjectArrayElement.Value(Value));
    internal static ObjectTypeCatalog Catalog() => new([Vector], Object,
        [new(new("fixture", Hash, Object.Token, "System.Object"), null, TypeAttributes.Public, [])])
    {
        Integer64 = true, Arrays = [Vector],
        ValueArrays = [new(Value, [new ObjectStorage.Int32(), new ObjectStorage.Reference(Object),
            new ObjectStorage.Int64(), new ObjectStorage.Reference(Object), new ObjectStorage.Int32()])]
    };
    private static ObjectIrProgram Program()
    {
        var method = new IrMethod(new("fixture", Hash, 0x06000001, "Driver::Run"), [IrType.Int32], IrType.Int32,
            [new(0, 0, [], [
                new IrStatement.Define(new(0, IrType.Int32, 0, new IrExpression.Parameter(0))),
                new IrStatement.Define(new(1, IrType.Reference(Vector), 1, new IrExpression.NewArray(Vector, 0)), IrExceptionTarget.MethodExit),
                new IrStatement.Define(new(2, IrType.Int32, 2, new IrExpression.ArrayLength(1)), IrExceptionTarget.MethodExit)
            ], new IrReturn(3, 2))], []);
        return new(new(IrMethodId.From(method.Source), [method], Catalog()),
            [new(IrMethodId.From(method.Source), new ObjectTypeId(Hash, 0x02000004), ObjectMethodKind.Static, MethodAttributes.Public | MethodAttributes.Static, true)]);
    }

    [Fact]
    public void Scalar_shape_requires_exact_bounded_known_leaves_and_distinct_value_identity()
    {
        var catalog = Catalog(); ObjectModelValidator.Validate(catalog); ObjectIrValidator.Validate(Program());
        foreach (var bad in new[] {
            catalog with { Integer64 = false }, catalog with { ValueArrays = [] },
            catalog with { ValueArrays = [catalog.ValueArrays[0], catalog.ValueArrays[0]] },
            catalog with { ValueArrays = [new(Value, [])] },
            catalog with { ValueArrays = [new(Value, [new ObjectStorage.Value(Value)])] },
            catalog with { ValueArrays = [new(Value, [new ObjectStorage.Reference(Value)])] },
            catalog with { ValueArrays = [new(Object, [new ObjectStorage.Int32()])] },
            catalog with { ValueArrays = [new(Value, [.. Enumerable.Repeat<ObjectStorage>(new ObjectStorage.Int32(), ObjectModelLimits.MaximumFields + 1)])] }
        }) Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(bad));
        var referenceVector = ObjectTypeId.Vector(Object, ObjectArrayElement.Reference(Object));
        Assert.NotEqual(Vector, referenceVector);
        var both = catalog with { Arrays = [Vector, referenceVector], Roots = [Vector, referenceVector] };
        var conversion = new IrMethod(new("fixture", Hash, 0x06000002, "Driver::Convert"),
            [IrType.Reference(Vector)], IrType.Reference(referenceVector),
            [new(0, 0, [], [new IrStatement.Define(new(0, IrType.Reference(Vector), 0, new IrExpression.Parameter(0)))], new IrReturn(1, 0))], []);
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(new(IrMethodId.From(conversion.Source), [conversion], both)));
        var p = Program(); var method = p.Flow.Methods[0];
        // A value vector must not accidentally enter the old reference-element operations.
        var load = new IrStatement.Define(new(2, IrType.Reference(Object), 2, new IrExpression.LoadElement(Vector, 1, 0)), IrExceptionTarget.MethodExit);
        var badMethod = method with { Blocks = [method.Blocks[0] with { Statements = method.Blocks[0].Statements.SetItem(2, load) }] };
        Assert.Throws<ArgumentException>(() => ObjectIrValidator.Validate(p with { Flow = p.Flow with { Methods = [badMethod] } }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Value_vector_storage_zeroes_charges_traces_and_releases_all_scalar_buffers(bool optimized)
    {
        var artifact = ObjectCppEmitter.EmitProgram(Program());
        string type = "t_" + Hash + "_02000002", vector = type + "_vector_value_t_" + Hash + "_02000003";
        string source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                for (std::uint32_t length : {0u, 1u, 7u}) {
                    const auto charge = sizeof(ArrayObject) + length * (2 * sizeof(std::uint32_t) + sizeof(std::uint64_t) + 2 * sizeof(Object*));
                    Instance heap; Context c(heap); heap.limit = charge;
                    Object* value = allocate_array(c, {{vector}}_descriptor, length);
                    assert(value != nullptr && c.pending == 0 && valid_array_storage(value));
                    assert(heap.bytes == charge && heap.count == 1);
                    auto* array = reinterpret_cast<ArrayObject*>(value);
                    for (std::size_t i = 0; i < length; ++i) {
                        assert(array->integers[2*i] == 0 && array->integers[2*i+1] == 0 && array->wide_integers[i] == 0);
                        assert(array->references[2*i] == nullptr && array->references[2*i+1] == nullptr);
                        array->integers[2*i+1] = 0xffffffffu; array->wide_integers[i] = 0xffffffffffffffffull;
                    }
                    Instance short_heap; Context short_c(short_heap); short_heap.limit = charge - 1;
                    assert(allocate_array(short_c, {{vector}}_descriptor, length) == nullptr);
                    assert(short_heap.count == 0 && short_heap.bytes == 0);
                    Frame frame; RootScope roots(c, frame, &value, 1);
                    heap.limit = 1048576; heap.collect_before_allocation = true;
                    for (std::size_t i = 0; i < length * 2; ++i) {
                        array->references[i] = allocate<{{type}}>(c, {{type}}_descriptor);
                        assert(array->references[i] != nullptr);
                    }
                    assert(collect(c).status == MarkStatus::complete && heap.count == 1 + length * 2);
                    assert(array_reference_count(value) == length * 2);
                    const auto saved = array->length; array->length = 0x7fffffffu;
                    assert(collect(c).status == MarkStatus::invalid && heap.count == 1 + length * 2);
                    array->length = saved;
                    assert(collect(c).status == MarkStatus::complete);
                    value = nullptr;
                    assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                }
                Instance heap; Context c(heap);
                assert(allocate_array(c, {{vector}}_descriptor, 0xffffffffu) == nullptr && c.pending == 2);
                c.pending = 0;
                Object* first = empty_array(c, {{vector}}_descriptor);
                assert(first != nullptr && first == empty_array(c, {{vector}}_descriptor));
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                return 0;
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("value-vector-storage");
        string cpp = Path.Combine(directory, "probe.cpp"), executable = Path.Combine(directory, "probe");
        File.WriteAllText(cpp, source, new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stderr);
    }
}
