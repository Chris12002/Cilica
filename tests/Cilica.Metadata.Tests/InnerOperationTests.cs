using System;
using System.IO;
using System.Linq;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Metadata.Resolution;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class InnerOperationTests
{
    private static readonly string[] Names = ["Payload", "Chain", "Catch", "Filter", "Rethrow", "NullRead", "CodeUnit"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(InnerOperationProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)] [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Wrapping_preserves_identity_messages_and_fault_order(bool optimized, int group)
    {
        var programs = await Programs.Value;
        var pairs = group == 2 ? (from a in new[] { -1, 0, 1 } from b in Enumerable.Range(-1, 70) select (a, b)).ToArray() : null;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(InnerOperationProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray(), pairs);
    }
    [Theory]
    [InlineData("UnsupportedArgument")] [InlineData("UnsupportedInnerFilter")]
    public async Task Other_inner_constructor_families_remain_diagnostic(string name)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(InnerOperationProbe).Assembly.Location).Value!;
        var handle = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(InnerOperationProbe).GetMethod(name)!.MetadataToken);
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, handle, implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains(name == "UnsupportedInnerFilter" ? "General exception filters" : "No implementation method matches", StringComparison.Ordinal));
    }
    [Fact]
    public async Task Exact_constructor_grant_is_required()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var narrowed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != LibraryIntrinsicCapability.InvalidOperationInnerConstructor)] }] };
        var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
        var input = resolver.OpenInput(typeof(InnerOperationProbe).Assembly.Location).Value!;
        var handle = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(InnerOperationProbe).GetMethod("Payload")!.MetadataToken);
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, handle, implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
    }
    [Theory]
    [InlineData("inner")] [InlineData("message")] [InlineData("parameter")] [InlineData("type-name")] [InlineData("fault")]
    public async Task Detached_wrapper_payloads_fail_at_both_stages(string mutation)
    {
        ImmutableArray<IrMethod> Change(ImmutableArray<IrMethod> methods)
        {
            var method = methods.First(m => m.Blocks.Any(b => b.Statements.Any(s => s is IrStatement.Define { Value.Expression: IrExpression.NewException { Inner: not null, TypeName: null } })));
            var block = method.Blocks.First(b => b.Statements.Any(s => s is IrStatement.Define { Value.Expression: IrExpression.NewException { Inner: not null, TypeName: null } }));
            var statement = (IrStatement.Define)block.Statements.First(s => s is IrStatement.Define { Value.Expression: IrExpression.NewException { Inner: not null, TypeName: null } });
            var creation = (IrExpression.NewException)statement.Value.Expression;
            var changed = mutation switch {
                "inner" => creation with { Inner = creation.Message },
                "message" => creation with { Message = null },
                "parameter" => creation with { ParameterName = creation.Message },
                "type-name" => creation with { TypeName = creation.Message }, _ => creation };
            var bad = block with { Statements = block.Statements.Replace(statement, statement with {
                Value = statement.Value with { Expression = changed }, Exceptional = mutation == "fault" ? null : statement.Exceptional }) };
            return methods.Replace(method, method with { Blocks = method.Blocks.Replace(block, bad) });
        }
        var program = (await Programs.Value)[0]; var logical = program.Flow.Flow.Flow;
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = logical with { Methods = Change(logical.Methods) } } } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program); var objects = scalar.Flow.Flow.Flow.Flow;
        var invalidObjects = objects with { Flow = objects.Flow with { Methods = Change(objects.Flow.Methods) } };
        var invalidScalar = scalar with { Flow = scalar.Flow with { Flow = scalar.Flow.Flow with { Flow = scalar.Flow.Flow.Flow with { Flow = invalidObjects } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar));
    }
    [Fact]
    public async Task Wrapper_resource_requires_the_selected_framework_identity()
    {
        var program = (await Programs.Value)[0];
        var flow = program.Flow.Flow.Flow;
        var references = flow.Types.References;
        var operation = program.Exceptions.Resources.Single(r => r.Kind == ExceptionResourceKind.InvalidOperation).Type;
        var declaration = references.Types.Single(t => ObjectTypeId.From(t.Source) == operation);
        var renamed = declaration with { Source = declaration.Source with { Name = "Other.WrapperException" } };
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with {
            Types = flow.Types with { References = references with { Types = references.Types.Replace(declaration, renamed) } } } } } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program);
        var objects = scalar.Flow.Flow.Flow.Flow;
        var scalarDeclaration = objects.Flow.Types.Types.Single(t => ObjectTypeId.From(t.Source) == operation);
        var scalarRenamed = scalarDeclaration with { Source = scalarDeclaration.Source with { Name = "Other.WrapperException" } };
        var invalidObjects = objects with { Flow = objects.Flow with { Types = objects.Flow.Types with {
            Types = objects.Flow.Types.Types.Replace(scalarDeclaration, scalarRenamed) } } };
        var invalidValues = scalar.Flow.Flow.Values with { References = references with { Types = references.Types.Replace(declaration, renamed) } };
        var invalidScalar = scalar with { Flow = scalar.Flow with { Flow = scalar.Flow.Flow with {
            Values = invalidValues, Flow = scalar.Flow.Flow.Flow with { Flow = invalidObjects } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Inner_graph_is_rooted_and_failed_allocation_preserves_pending_fault(bool optimized)
    {
        var program = (await Programs.Value)[0]; var catalog = program.Flow.Flow.Flow.Types.References;
        var operation = program.Exceptions.Resources.Single(r => r.Kind == ExceptionResourceKind.InvalidOperation).Type;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string Symbol(ObjectTypeId type) => $"t_{type.ContentHash}_{type.Token:x8}_descriptor";
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            [[gnu::noinline]] static void construct(Context& c, Object** roots) {
                static constexpr std::uint16_t units[] = {65, 0, 0xd800};
                roots[0] = allocate_string(c, {{Symbol(catalog.String!)}}, units, 3, nullptr, 0);
                roots[1] = make_exception(c, runtime_exception(1), nullptr, 2).value;
                roots[2] = make_exception(c, &{{Symbol(operation)}}, roots[0], 0, nullptr, roots[1]).value;
                assert(roots[2] != nullptr); roots[0] = roots[1] = nullptr;
            }
            [[gnu::noinline]] static void check_payload(Context& c, Object** roots) {
                auto& heap = c.instance; auto* outer = reinterpret_cast<ExceptionObject*>(roots[2]);
                assert(collect(c).status == MarkStatus::complete && heap.count == 4);
                assert(exception_inner(c, roots[2]).value == outer->inner && heap.owns(outer->inner));
                assert(exception_message(c, roots[2]).value == outer->message);
                assert(string_length(outer->message) == 3 && string_code_unit(outer->message, 2) == 0xd800);
            }
            [[gnu::noinline]] static void allocation_failure(Context& c, Object** roots) {
                auto& heap = c.instance; auto* outer = reinterpret_cast<ExceptionObject*>(roots[2]);
                heap.limit = heap.bytes; c.pending = 2; c.image = "saved"; c.method = 17; c.il = 31;
                auto failed = make_exception(c, &{{Symbol(operation)}}, outer->message, 0, nullptr, outer->inner);
                assert(failed.status == 5 && failed.value == nullptr && c.pending == 2 && c.method == 17 && c.il == 31);
                assert(collect(c).status == MarkStatus::complete && heap.count == 4);
                heap.limit = 1048576; c.pending = 0;
            }
            [[gnu::noinline]] static void corrupt_and_clear(Context& c, Object** roots) {
                auto& heap = c.instance; auto* outer = reinterpret_cast<ExceptionObject*>(roots[2]);
                assert(make_exception(c, runtime_exception(1), nullptr, 0, nullptr, outer->inner).status == 6);
                assert(make_exception(c, &{{Symbol(operation)}}, nullptr, 1, nullptr, outer->inner).status == 6);
                auto* saved = outer->inner; outer->inner = reinterpret_cast<Object*>(std::uintptr_t{1});
                assert(exception_inner(c, roots[2]).status == 6 && exception_message(c, roots[2]).status == 6);
                assert(collect(c).status == MarkStatus::invalid); outer->inner = saved;
                assert(exception_inner(c, nullptr).status == 4);
                roots[2] = nullptr; assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                assert(c.used == 0 && c.borrows == nullptr);
            }
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                assert(prepare_exceptions(c) == 0);
                Object* roots[3] = {}; Frame frame; RootScope scope(c, frame, roots, 3);
                construct(c, roots); check_payload(c, roots); allocation_failure(c, roots); corrupt_and_clear(c, roots);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("inner-operation");
        string cpp = Path.Combine(directory, "payload.cpp"), exe = Path.Combine(directory, "payload");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }

}
