using System;
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
public sealed class TypeInitializationExceptionTests
{
    private static readonly (string Name, Func<int, int, int> Oracle)[] Probes = [
        ("Payload", TypeInitializationExceptionProbe.Payload), ("Chain", TypeInitializationExceptionProbe.Chain),
        ("Null", TypeInitializationExceptionProbe.Null), ("CodeUnit", TypeInitializationExceptionProbe.CodeUnit)];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(TypeInitializationExceptionProbe), Probes.Select(p => p.Name).ToArray()));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Type_names_messages_inner_identity_and_faults_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Probes.Take(3).Select((p, i) => (p.Name, programs[i], p.Oracle)).ToArray());
        var pairs = (from input in new[] { -1, 0 } from index in Enumerable.Range(-1, 55) select (input, index)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("CodeUnit", programs[3], TypeInitializationExceptionProbe.CodeUnit)], pairs);
    }
    [Fact]
    public async Task Each_new_exception_operation_requires_its_exact_grant()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var input = resolver.OpenInput(typeof(TypeInitializationExceptionProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(TypeInitializationExceptionProbe).GetMethod("Payload")!.MetadataToken);
        foreach (var capability in new[] { LibraryIntrinsicCapability.TypeInitializationExceptionConstructor, LibraryIntrinsicCapability.ExceptionTypeName, LibraryIntrinsicCapability.ExceptionInnerException })
        {
            var narrowed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != capability)] }] };
            var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
            using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
            var failed = ValueTranslator.TranslateIntegerObjects(denied, input, method, implementation);
            Assert.False(failed.Success); Assert.Contains(failed.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        }
    }
    [Theory]
    [InlineData("name")] [InlineData("inner")] [InlineData("identity")] [InlineData("fault")]
    public async Task Detached_payloads_require_exact_types_and_fault_edges(string mutation)
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        var method = flow.Methods.First(m => m.Blocks.Any(b => b.Statements.Any(s => s is IrStatement.Define { Value.Expression: IrExpression.NewException { TypeName: not null } })));
        var block = method.Blocks.First(b => b.Statements.Any(s => s is IrStatement.Define { Value.Expression: IrExpression.NewException { TypeName: not null } }));
        var statement = (IrStatement.Define)block.Statements.First(s => s is IrStatement.Define { Value.Expression: IrExpression.NewException { TypeName: not null } });
        var creation = (IrExpression.NewException)statement.Value.Expression;
        var changed = mutation switch {
            "name" => creation with { TypeName = creation.Inner },
            "inner" => creation with { Inner = creation.TypeName },
            _ => creation };
        var badBlock = block with { Statements = block.Statements.Replace(statement, statement with {
            Value = statement.Value with { Expression = changed }, Exceptional = mutation == "fault" ? null : statement.Exceptional }) };
        var badMethod = method with { Blocks = method.Blocks.Replace(block, badBlock) };
        var invalid = program with { Exceptions = mutation == "identity" ? program.Exceptions with { TypeInitialization = flow.Types.References.String } : program.Exceptions,
            Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Methods = flow.Methods.Replace(method, badMethod) } } } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(invalid));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Payload_roots_survive_collection_and_format_failure_preserves_origin(bool optimized)
    {
        var program = (await Programs.Value)[0];
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string Symbol(ObjectTypeId type) => $"t_{type.ContentHash}_{type.Token:x8}_descriptor";
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            [[gnu::noinline]] static void exercise(Context& c, Object** roots) {
                static constexpr std::uint16_t name[] = {65, 0, 0xd800};
                roots[0] = allocate_string(c, {{Symbol(program.Flow.Flow.Flow.Types.References.String!)}}, name, 3, nullptr, 0);
                roots[1] = make_exception(c, runtime_exception(1), nullptr, 2).value;
                roots[2] = make_exception(c, &{{Symbol(program.Exceptions.TypeInitialization!)}}, nullptr, 3, nullptr, roots[1], roots[0]).value;
                assert(roots[2] != nullptr); roots[0] = roots[1] = nullptr;
            }
            [[gnu::noinline]] static void format(Context& c, Object** roots) {
                auto& heap = c.instance; auto* payload = reinterpret_cast<ExceptionObject*>(roots[2]);
                assert(collect(c).status == MarkStatus::complete && heap.count == 4);
                assert(exception_inner(c, roots[2]).value == payload->inner);
                assert(exception_type_name(c, roots[2]).value == payload->type_name);
                heap.limit = heap.bytes; c.pending = 2; c.image = "saved"; c.method = 17; c.il = 31;
                auto failed = exception_message(c, roots[2]);
                assert(failed.status == 5 && payload->message == nullptr && c.pending == 2 && c.method == 17 && c.il == 31);
                heap.limit = 1024 * 1024; c.pending = 0;
                auto formatted = exception_message(c, roots[2]);
                assert(formatted.status == 0 && string_length(formatted.value) == 50);
                assert(string_code_unit(formatted.value, 28) == 0xd800);
                assert(exception_message(c, roots[2]).value == formatted.value);
                assert(collect(c).status == MarkStatus::complete && heap.count == 5);
            }
            [[gnu::noinline]] static void corrupt(Context& c, Object** roots) {
                auto& heap = c.instance; auto* payload = reinterpret_cast<ExceptionObject*>(roots[2]);
                auto* saved = payload->inner; payload->inner = reinterpret_cast<Object*>(std::uintptr_t{1});
                assert(exception_inner(c, roots[2]).status == 6 && exception_message(c, roots[2]).status == 6);
                assert(collect(c).status == MarkStatus::invalid); payload->inner = saved;
                saved = payload->type_name; payload->type_name = reinterpret_cast<Object*>(std::uintptr_t{1});
                assert(exception_type_name(c, roots[2]).status == 6);
                assert(collect(c).status == MarkStatus::invalid); payload->type_name = saved;
                assert(exception_inner(c, nullptr).status == 4 && exception_type_name(c, nullptr).status == 4);
                roots[2] = nullptr; assert(collect(c).status == MarkStatus::complete && heap.count == 1);
            }
            [[gnu::noinline]] static void construct(Context& c, Object** roots) {
                auto& heap = c.instance;
                heap.limit = heap.bytes + sizeof(ExceptionObject); c.pending = 2; c.method = 17; c.il = 31;
                auto failedConstruction = make_initialization_exception(c, &{{Symbol(program.Exceptions.TypeInitialization!)}}, nullptr, nullptr);
                assert(failedConstruction.status == 5 && failedConstruction.value == nullptr && c.pending == 2 && c.method == 17 && c.il == 31);
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                heap.limit = 1024 * 1024; c.pending = 0;
                roots[2] = make_initialization_exception(c, &{{Symbol(program.Exceptions.TypeInitialization!)}}, nullptr, nullptr).value;
                assert(roots[2] != nullptr && reinterpret_cast<ExceptionObject*>(roots[2])->message != nullptr);
                heap.limit = heap.bytes;
                assert(exception_message(c, roots[2]).status == 0);
            }
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                assert(prepare_exceptions(c) == 0);
                Object* roots[3]{}; Frame frame; RootScope scope(c, frame, roots, 3);
                exercise(c, roots); format(c, roots); corrupt(c, roots); construct(c, roots);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("initialization-exception");
        string cpp = Path.Combine(directory, "payload.cpp"), exe = Path.Combine(directory, "payload");
        File.WriteAllText(cpp, source);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }

}
