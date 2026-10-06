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
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ActualRangeTests
{
    private static readonly string[] Names = ["Integer", "Null", "Caught", "InterfaceClosure"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ActualRangeProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    public async Task Actual_integer_and_null_messages_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ActualRangeProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }
    [Fact]
    public async Task Exact_grant_is_required_and_unproven_payloads_stay_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var input = resolver.OpenInput(typeof(ActualRangeProbe).Assembly.Location).Value!;
        var grant = Assert.Single(artifact.Grants.Where(g => g.Capability == LibraryIntrinsicCapability.ArgumentOutOfRangeActualConstructor));
        var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(manifest with { Artifacts = [artifact with { Grants = artifact.Grants.Remove(grant) }] });
        using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
        var failure = ValueTranslator.TranslateIntegerObjects(denied, input, Method("Integer"), implementation);
        Assert.False(failure.Success); Assert.Contains(failure.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        foreach (string name in new[] { "Alias", "WrongInteger", "Text", "Getter" })
        {
            var result = ValueTranslator.TranslateIntegerObjects(scope, input, Method(name), implementation);
            Assert.False(result.Success, name); Assert.NotEmpty(result.Diagnostics);
        }
        MethodDefinitionHandle Method(string name) => (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ActualRangeProbe).GetMethod(name)!.MetadataToken);
    }
    [Theory]
    [InlineData("operand")] [InlineData("identity")] [InlineData("message")] [InlineData("fault")]
    public async Task Detached_actual_payloads_require_direct_boxes_exact_fault_type_and_edges(string mutation)
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        var method = flow.Methods.First(m => m.Blocks.Any(b => b.Values.Any(v => v.Expression is IrExpression.NewException { ActualValue: not null })));
        IrBlock Change(IrBlock block)
        {
            if (block.Statements.FirstOrDefault(s => s is IrStatement.Define { Value.Expression: IrExpression.NewException { ActualValue: not null } }) is not IrStatement.Define statement) return block;
            var creation = (IrExpression.NewException)statement.Value.Expression;
            var changed = mutation switch { "operand" => creation with { ActualValue = creation.Message },
                "identity" => creation with { Type = program.Exceptions.Base }, "message" => creation with { Message = null }, _ => creation };
            return block with { Statements = block.Statements.Replace(statement, statement with { Value = statement.Value with { Expression = changed }, Exceptional = mutation == "fault" ? null : statement.Exceptional }) };
        }
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Methods = flow.Methods.Replace(method, method with { Blocks = [.. method.Blocks.Select(Change)] }) } } } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program); var scalarFlow = scalar.Flow.Flow.Flow.Flow.Flow;
        var scalarMethod = scalarFlow.Methods.Single(m => m.Source == method.Source);
        var invalidScalar = scalar with { Flow = scalar.Flow with { Flow = scalar.Flow.Flow with { Flow = scalar.Flow.Flow.Flow with { Flow = scalar.Flow.Flow.Flow.Flow with { Flow = scalarFlow with { Methods = scalarFlow.Methods.Replace(scalarMethod, scalarMethod with { Blocks = [.. scalarMethod.Blocks.Select(Change)] }) } } } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Actual_integer_and_range_resources_require_selected_names(bool integer)
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        var layout = integer ? flow.Types.Types.Single(t => t.Source.Name == "System.Int32") : null;
        var range = flow.Types.References.Types.Single(t => t.Source.Name == "System.ArgumentOutOfRangeException");
        var changed = integer ? flow.Types with { Types = flow.Types.Types.Replace(layout!, layout! with { Source = layout!.Source with { Name = "Other.Int32" } }) }
            : flow.Types with { References = flow.Types.References with { Types = flow.Types.References.Types.Replace(range, range with { Source = range.Source with { Name = "Other.ArgumentOutOfRangeException" } }) } };
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Types = changed } } } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program); var boxes = scalar.Flow.Flow;
        var invalidScalar = scalar with { Flow = scalar.Flow with { Flow = boxes with { Values = changed } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Retained_box_and_temporary_messages_survive_gc_and_allocation_failure(bool optimized)
    {
        var program = (await Programs.Value)[0];
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        var integer = program.Flow.Flow.Flow.Types.Types.Single(t => t.Source.Name == "System.Int32");
        var range = program.Exceptions.Resources.Single(r => r.Kind == ExceptionResourceKind.ArgumentOutOfRange).Type;
        string box = $"box_{integer.Source.ContentHash}_{integer.Source.Token:x8}";
        string Symbol(ObjectTypeId type) => $"t_{type.ContentHash}_{type.Token:x8}_descriptor";
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            [[gnu::noinline]] static void create(Context& c, Object** roots) {
                roots[0] = allocate_box_storage(c, &{{box}}_descriptor);
                assert(roots[0] != nullptr); reinterpret_cast<{{box}}*>(roots[0])->s0 = 0x80000000u;
                static constexpr std::uint16_t name[] = {105};
                roots[1] = allocate_string(c, {{Symbol(program.Flow.Flow.Flow.Types.References.String!)}}, name, 1, nullptr, 0);
                roots[2] = make_exception(c, &{{Symbol(range)}}, nullptr, 0, roots[1], nullptr, nullptr, roots[0]).value;
                assert(roots[2] != nullptr); roots[0] = roots[1] = nullptr;
                assert(collect(c).status == MarkStatus::complete && c.instance.count == 4);
            }
            [[gnu::noinline]] static void inspect(Context& c, Object** roots) {
                auto* payload = reinterpret_cast<ExceptionObject*>(roots[2]);
                assert(payload->actual_value != nullptr && reinterpret_cast<{{box}}*>(payload->actual_value)->s0 == 0x80000000u);
                auto result = exception_message(c, roots[2]); assert(result.status == 0);
                assert(collect(c).status == MarkStatus::complete && c.instance.count == 5);
            }
            [[gnu::noinline]] static void fail_allocations(Context& c, Object** roots) {
                auto* payload = reinterpret_cast<ExceptionObject*>(roots[2]);
                c.instance.limit = c.instance.bytes; c.pending = 2; c.image = "saved"; c.method = 17; c.il = 31;
                assert(exception_message(c, roots[2]).status == 5);
                assert(c.pending == 2 && c.method == 17 && c.il == 31);
                // Permit precisely the intermediate parameter message, then fail the suffix allocation.
                c.instance.limit += sizeof(StringObject) + (string_length(payload->message) + 16u) * sizeof(std::uint16_t);
                assert(exception_message(c, roots[2]).status == 5);
                assert(c.pending == 2 && c.method == 17 && c.il == 31);
                c.instance.limit = 1024 * 1024; c.pending = 0;
                assert(exception_message(c, roots[2]).status == 0);
            }
            [[gnu::noinline]] static void corrupt(Context& c, Object** roots) {
                auto* payload = reinterpret_cast<ExceptionObject*>(roots[2]);
                auto* saved = payload->actual_value; payload->actual_value = reinterpret_cast<Object*>(std::uintptr_t{1});
                assert(exception_message(c, roots[2]).status == 6 && collect(c).status == MarkStatus::invalid);
                payload->actual_value = saved;
                assert(collect(c).status == MarkStatus::complete && c.instance.count == 5);
                roots[2] = nullptr; assert(collect(c).status == MarkStatus::complete && c.instance.count == 2);
            }
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                assert(prepare_exceptions(c) == 0);
                Object* roots[3]{}; Frame frame; RootScope scope(c, frame, roots, 3);
                create(c, roots); inspect(c, roots); fail_allocations(c, roots); corrupt(c, roots);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("actual-range");
        string cpp = Path.Combine(directory, "actual.cpp"), exe = Path.Combine(directory, "actual");
        File.WriteAllText(cpp, source);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }

}
