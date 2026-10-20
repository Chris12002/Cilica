using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;

public sealed class SubstringConstructionTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(SubstringProbe), ["Range", "Caught"]));

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Slice_source_survives_collection_and_one_allocation_preserves_fault_origins(bool optimized)
    {
        var program = (await Programs.Value)[0];
        var flow = program.Flow.Flow.Flow;
        var copy = flow.Methods.Single(m => m.Blocks.Any(b => b.Values.Any(v => v.Expression is IrExpression.StringFromSlice)));
        int offset = copy.Blocks.SelectMany(b => b.Values).Single(v => v.Expression is IrExpression.StringFromSlice).ILOffset;
        Assert.DoesNotContain(copy.Blocks.SelectMany(b => b.Values), v => v.Expression is IrExpression.NewArray);
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string symbol = "cilica_m_" + copy.Source.ContentHash + "_" + copy.Source.Token.ToString("x8", CultureInfo.InvariantCulture);
        string source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            static unsigned allocations;
            static void observe(Context&) noexcept { ++allocations; }
            int main() {
                Instance heap; Context c(heap); heap.collect_before_allocation = true;
                assert(prepare_exceptions(c) == 0); const auto permanent = heap.count;
                LocationArgument args[3]{};
                const std::uint16_t units[] = {65, 0, 0xd800, 0xdc00, 0xffff, 90};
                for (unsigned start = 0; start < 6; ++start) {
                    for (unsigned length = 1; length <= 6 - start; ++length) {
                        c.observe = nullptr;
                        auto* text = allocate_string(c, *exception_string_type(), units, 6, nullptr, 0);
                        assert(text && c.pending == 0);
                        // No external root: the generated callee must retain its source.
                        args[0].reference = text; args[1].bits = start; args[2].bits = length;
                        allocations = 0; c.observe = observe;
                        auto* result = {{symbol}}(c, args);
                        assert(result && result != text && c.pending == 0 && allocations == 1);
                        assert(heap.count == permanent + 2 && string_length(result) == length);
                        for (unsigned i = 0; i < length; ++i) assert(string_code_unit(result, i) == units[start + i]);
                        assert(c.top == nullptr && c.borrows == nullptr && c.used == 0 && c.depth == 0);
                        assert(collect(c).status == MarkStatus::complete && heap.count == permanent);
                    }
                }
                c.observe = nullptr;
                Object* text = allocate_string(c, *exception_string_type(), units, 6, nullptr, 0);
                Frame frame; RootScope root(c, frame, &text, 1);
                args[0].reference = text; args[1].bits = 1; args[2].bits = 3;
                // A source and one final string fit; there is no spare temporary-array slot.
                heap.object_limit = permanent + 2; allocations = 0; c.observe = observe;
                assert({{symbol}}(c, args) != nullptr && c.pending == 0 && allocations == 1);
                assert(collect(c).status == MarkStatus::complete && heap.count == permanent + 1);
                heap.object_limit = permanent + 1;
                for (bool byte_limit : {false, true}) {
                    heap.object_limit = byte_limit ? 4096 : permanent + 1;
                    heap.limit = byte_limit ? heap.bytes + sizeof(StringObject) + 3 * sizeof(std::uint16_t) - 1 : 1048576;
                    allocations = 0; Context failed(heap); failed.observe = observe;
                    assert({{symbol}}(failed, args) == nullptr && failed.pending == 5 && allocations == 1);
                    assert(failed.method == {{copy.Source.Token}}u && failed.il == {{offset}}u);
                    assert(failed.top == nullptr && failed.borrows == nullptr && failed.used == 0 && failed.depth == 0);
                }
                heap.object_limit = 4096; heap.limit = 1048576;
                for (auto start : {0xffffffffu, 7u, 0x80000000u}) {
                    args[1].bits = start; Context bad(heap); bad.observe = observe; allocations = 0;
                    assert({{symbol}}(bad, args) == nullptr && bad.pending == 10);
                }
                args[1].bits = 1;
                for (auto length : {0xffffffffu, 6u, 0x80000000u}) {
                    args[2].bits = length; Context bad(heap); bad.observe = observe; allocations = 0;
                    assert({{symbol}}(bad, args) == nullptr && bad.pending == 10);
                }
                args[0].reference = text; args[1].bits = 6; args[2].bits = 0;
                Context empty(heap); auto* empty_result = {{symbol}}(empty, args);
                assert(empty_result != nullptr && empty.pending == 0 && string_length(empty_result) == 0);
                // Each Context sees its own frames. Do not reuse text after this
                // null call can collect it while materializing the exception.
                text = nullptr; args[0].reference = nullptr; args[1].bits = 0;
                Context null(heap); assert({{symbol}}(null, args) == nullptr && null.pending == 4);
            }
            """;
        using var files = new ResolverFiles(); string dir = files.DirectoryPath("slice-native");
        string cpp = Path.Combine(dir, "slice.cpp"), exe = Path.Combine(dir, "slice");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", optimized ? "-O2" : "-O0", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror",
             "-DCILICA_VERIFY_INTERNAL_REFERENCES=1", "-DCILICA_VERIFY_ALLOCATION_ROOTS=1",
             "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], dir);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], dir);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Allocation_failure_is_catchable_and_next_call_recovers(bool optimized)
    {
        var program = (await Programs.Value)[1];
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string symbol = "cilica_m_" + artifact.Source.ContentHash + "_" + artifact.Source.Token.ToString("x8", CultureInfo.InvariantCulture);
        string source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; Context c(heap); heap.collect_before_allocation = true;
                LocationArgument args[2]{}; args[0].bits = 1; args[1].bits = 1;
                assert({{symbol}}(c, args) == 1 && c.pending == 0);
                assert(collect(c).status == MarkStatus::complete);
                const auto retained = heap.count; heap.object_limit = retained;
                assert({{symbol}}(c, args) == 77 && c.pending == 0 && c.exception == nullptr);
                assert(c.top == nullptr && c.borrows == nullptr && c.used == 0 && c.depth == 0);
                heap.object_limit = retained + 1;
                assert({{symbol}}(c, args) == 1 && c.pending == 0);
                assert(collect(c).status == MarkStatus::complete && heap.count == retained);
            }
            """;
        using var files = new ResolverFiles(); string dir = files.DirectoryPath("slice-caught");
        string cpp = Path.Combine(dir, "slice.cpp"), exe = Path.Combine(dir, "slice");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", optimized ? "-O2" : "-O0", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror",
             "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], dir);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], dir);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }

    [Fact]
    public async Task Direct_substring_allocation_requires_exact_grant_and_private_caller()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions {
            Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(manifest with { Artifacts = [artifact with {
            Grants = [.. artifact.Grants.Where(g => g.Capability != LibraryIntrinsicCapability.StringFromSlice)] }] });
        var input = resolver.OpenInput(typeof(SubstringProbe).Assembly.Location).Value!;
        var root = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(SubstringProbe).GetMethod("Range")!.MetadataToken);
        using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(denied, input, root, implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        var builder = new ResolutionAssemblyBuilder(new("HostileSliceConstruction", new Version(1, 0, 0, 0)));
        var owner = builder.TypeReference("Cilica.Runtime", "StringConstruction", builder.Reference(implementation.Identity));
        var member = builder.Member(owner, "FromSlice", 0, 3, 0x0e, 0x0e, 8, 8);
        builder.Type("Test", "Caller");
        var method = builder.Method("Run", [0, 0, 8], emit: il => {
            il.OpCode(ILOpCode.Ldnull); il.LoadConstantI4(0); il.LoadConstantI4(1); il.Call(member); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        var hostile = scope.OpenInput(files.Write("hostile.dll", builder.Build())).Value!;
        var rejected = ValueTranslator.TranslateIntegerObjects(scope, hostile, method, implementation);
        Assert.False(rejected.Success); Assert.Contains(rejected.Diagnostics, d => d.Message.Contains("lexical declaring scope", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("type")] [InlineData("source")] [InlineData("start")] [InlineData("length")]
    [InlineData("source-type")] [InlineData("start-type")] [InlineData("length-type")] [InlineData("fault")]
    public async Task Both_detached_stages_reject_invalid_substring_allocation(string mutation)
    {
        var program = (await Programs.Value)[0];
        ImmutableArray<IrMethod> Alter(ImmutableArray<IrMethod> methods) => [.. methods.Select(m => m with {
            Blocks = [.. m.Blocks.Select(b => b with { Statements = [.. b.Statements.Select(s => {
                if (s is not IrStatement.Define { Value.Expression: IrExpression.StringFromSlice slice } d) return s;
                return d with { Value = mutation switch {
                    "type" => d.Value with { Type = IrType.Int32 },
                    "source" => d.Value with { Expression = slice with { Source = d.Value.Id } },
                    "start" => d.Value with { Expression = slice with { Start = d.Value.Id } },
                    "length" => d.Value with { Expression = slice with { Length = d.Value.Id } },
                    "source-type" => d.Value with { Expression = slice with { Source = slice.Start } },
                    "start-type" => d.Value with { Expression = slice with { Start = slice.Source } },
                    "length-type" => d.Value with { Expression = slice with { Length = slice.Source } },
                    _ => d.Value }, Exceptional = mutation == "fault" ? null : d.Exceptional };
            })] })] })];
        var flow = program.Flow.Flow.Flow;
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program with { Flow = program.Flow with {
            Flow = program.Flow.Flow with { Flow = flow with { Methods = Alter(flow.Methods) } } } }));
        var scalar = ValueScalarLowering.LowerExceptions(program); var boxes = scalar.Flow.Flow; var objects = boxes.Flow.Flow;
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(scalar with { Flow = scalar.Flow with {
            Flow = boxes with { Flow = boxes.Flow with { Flow = objects with { Flow = objects.Flow with { Methods = Alter(objects.Flow.Methods) } } } } } }));
    }
}
