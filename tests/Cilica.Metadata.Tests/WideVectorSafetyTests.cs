using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class WideVectorSafetyTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram>> Program = new(async () =>
        (await CoreLibraryBuildTests.TranslateFixtures(typeof(WideVectorProbe), ["Storage"]))[0]);
    [Theory]
    [InlineData("array")] [InlineData("width")] [InlineData("fault")]
    public async Task Both_stages_reject_malformed_wide_element_operations(string mutation)
    {
        var program = await Program.Value; var flow = program.Flow.Flow.Flow;
        ImmutableArray<IrMethod> Alter(ImmutableArray<IrMethod> methods) =>
            [.. methods.Select(m => m with { Blocks = [.. m.Blocks.Select(b => b with { Statements = [.. b.Statements.Select(statement =>
                statement is IrStatement.StoreElement s && s.ArrayType.ArrayElement!.Kind == ObjectArrayElementKind.Int64
                    ? mutation == "array" ? s with { ArrayType = ObjectTypeId.Vector(flow.Types.References.ObjectBase, ObjectArrayElement.Int32) }
                    : mutation == "width" ? s with { Value = s.Index } : s with { Exceptional = (IrExceptionTarget)99 }
                    : statement)] })] })];
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program with { Flow = program.Flow with {
            Flow = program.Flow.Flow with { Flow = flow with { Methods = Alter(flow.Methods) } } } }));
        var scalar = ValueScalarLowering.LowerExceptions(program); var boxes = scalar.Flow.Flow; var objects = boxes.Flow.Flow;
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(scalar with { Flow = scalar.Flow with {
            Flow = boxes with { Flow = boxes.Flow with { Flow = objects with { Flow = objects.Flow with { Methods = Alter(objects.Flow.Methods) } } } } } }));
    }
    [Fact]
    public async Task Wide_vector_model_requires_its_profile_and_no_nominal_element_identity()
    {
        var root = (await Program.Value).Flow.Flow.Flow.Types.References.ObjectBase;
        var vector = ObjectTypeId.Vector(root, ObjectArrayElement.Int64);
        var catalog = new ObjectTypeCatalog([vector], root,
            [new(new("fixture", root.ContentHash, root.Token, "System.Object"), null, TypeAttributes.Public, [])])
            { Integer64 = true, Arrays = [vector] };
        ObjectModelValidator.Validate(catalog);
        Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(catalog with { Integer64 = false }));
        var invalid = vector with { ArrayElement = new ObjectArrayElement(ObjectArrayElementKind.Int64, catalog.ObjectBase) };
        Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(catalog with {
            Arrays = [.. catalog.Arrays.Select(a => a == vector ? invalid : a)],
            Roots = [.. catalog.Roots.Select(a => a == vector ? invalid : a)] }));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Native_wide_buffers_zero_charge_validate_and_recover(bool optimized)
    {
        var program = await Program.Value;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        var root = program.Flow.Flow.Flow.Types.References.ObjectBase;
        string vector = $"t_{root.ContentHash}_{root.Token:x8}_vector_i8";
        string source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                for (std::uint32_t length : {0u, 1u, 7u}) {
                    const auto charge = sizeof(ArrayObject) + length * sizeof(std::uint64_t);
                    Instance heap; Context c(heap); heap.limit = charge;
                    Object* value = allocate_array(c, {{vector}}_descriptor, length);
                    assert(value != nullptr && c.pending == 0 && valid_array_storage(value));
                    assert(heap.bytes == charge && heap.count == 1);
                    auto* array = reinterpret_cast<ArrayObject*>(value);
                    assert(array->integers == nullptr && array->references == nullptr);
                    for (std::uint32_t i = 0; i < length; ++i) {
                        assert(load_wide_element(value, i) == 0);
                        store_wide_element(value, i, 0xfedcba9876543210ull + i);
                        assert(load_wide_element(value, i) == 0xfedcba9876543210ull + i);
                    }
                    Instance short_heap; Context short_c(short_heap); short_heap.limit = charge - 1;
                    assert(allocate_array(short_c, {{vector}}_descriptor, length) == nullptr);
                    assert(short_heap.count == 0 && short_heap.bytes == 0);
                    Frame frame; RootScope roots(c, frame, &value, 1);
                    assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                    array->length = 0x7fffffffu;
                    assert(!valid_array_storage(value) && collect(c).status == MarkStatus::invalid);
                    array->length = length;
                    auto* saved = array->wide_integers;
                    if (length != 0) { array->wide_integers = nullptr; assert(!valid_array_storage(value)); array->wide_integers = saved; }
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
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("wide-vector-storage");
        string cpp = Path.Combine(directory,"probe.cpp"), executable = Path.Combine(directory,"probe");
        File.WriteAllText(cpp, source, new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr);
    }
}
