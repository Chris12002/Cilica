using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class EnumDefinedTests
{
    private static readonly string[] Names = ["Values", "Integers", "Empty", "Flags", "Names", "Handles", "NullOrder", "WrongEnum", "WrongNumbers", "UnsupportedObjects", "NonEnum"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(EnumDefinedProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    public async Task Definitions_names_and_exact_fault_order_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(EnumDefinedProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray(),
            pairs: (from a in new[] { int.MinValue, -7, -4, -3, -2, -1, 0, 1, 2, 3, 4, 7, 8, 65536, int.MaxValue } from b in new[] { -7, -1, 0, 1, 7 } select (a,b)).ToArray());
    }

    [Theory]
    [InlineData("missing")] [InlineData("token")] [InlineData("name")] [InlineData("flag")] [InlineData("carrier")]
    [InlineData("forward")] [InlineData("implementation")] [InlineData("result")] [InlineData("fault")]
    public async Task Both_stages_reject_invalid_metadata_and_query_operations(string mutation)
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        ValueTypeCatalog Values(ValueTypeCatalog values)
        {
            var target = values.Types.Single(t => t.Source.Name == "Cilica.Fixtures.Execution.DefinedSparse");
            var constants = target.EnumConstants;
            var replacement = mutation switch {
                "missing" => target with { EnumConstants = default },
                "token" => target with { EnumConstants = constants.SetItem(0, constants[0] with { Token = 0x06000002 }) },
                "name" => target with { EnumConstants = constants.SetItem(1, constants[1] with { Name = constants[0].Name }) },
                "flag" => target with { ScalarEnum = false },
                "carrier" => target with { ScalarBox = System.Reflection.Metadata.PrimitiveTypeCode.UInt32 },
                _ => target };
            return values with { Types = values.Types.Replace(target, replacement) };
        }
        System.Collections.Immutable.ImmutableArray<IrMethod> Methods(System.Collections.Immutable.ImmutableArray<IrMethod> methods) =>
            [.. methods.Select(m => m with { Blocks = [.. m.Blocks.Select(b => b with { Statements = [.. b.Statements.Select(statement =>
                statement is IrStatement.Define d && d.Value.Expression is IrExpression.EnumQuery query ? mutation switch {
                    "forward" => d with { Value = d.Value with { Expression = query with { Value = d.Value.Id } } },
                    "implementation" => d with { Value = d.Value with { Expression = query with { Implementation = flow.Types.References.ObjectBase } } },
                    "result" => d with { Value = d.Value with { Expression = query with { Message = !query.Message } } },
                    "fault" => d with { Exceptional = null }, _ => statement } : statement)] })] })];
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Types = Values(flow.Types), Methods = Methods(flow.Methods) } } } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program); var boxes = scalar.Flow.Flow; var objects = boxes.Flow.Flow;
        var invalidScalar = scalar with { Flow = scalar.Flow with { Flow = boxes with { Values = Values(boxes.Values), Flow = boxes.Flow with { Flow = objects with { Flow = objects.Flow with { Methods = Methods(objects.Flow.Methods) } } } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar));
    }

    [Fact]
    public async Task Grants_and_bounded_enum_carriers_are_required()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions {
            Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var input = resolver.OpenInput(typeof(EnumDefinedProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name) => (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(EnumDefinedProbe).GetMethod(name)!.MetadataToken);
        foreach (var capability in new[] { LibraryIntrinsicCapability.EnumIsDefinedQuery, LibraryIntrinsicCapability.EnumIsDefinedErrorMessage })
        {
            var narrowed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != capability)] }] };
            var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
            using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
            var result = ValueTranslator.TranslateIntegerObjects(denied, input, Method("Values"), implementation);
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        }
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        var wide = ValueTranslator.TranslateIntegerObjects(scope, input, Method("UnsupportedWideEnum"), implementation);
        Assert.False(wide.Success); Assert.Null(wide.Program); Assert.NotEmpty(wide.Diagnostics);
        var hostile = new ResolutionAssemblyBuilder(new("EnumIntrinsicCaller", new Version(1, 0, 0, 0)));
        var helper = hostile.TypeReference("Cilica.Runtime", "EnumMetadata", hostile.Reference(implementation.Identity));
        var typeRef = hostile.TypeReference("System", "Type", hostile.Reference(implementation.Identity));
        var signature = new BlobBuilder(); signature.WriteBytes(new byte[] { 0, 2, 8, 0x12 });
        signature.WriteCompressedInteger(CodedIndex.TypeDefOrRef(typeRef)); signature.WriteByte(0x1c);
        var call = hostile.Member(helper, "Query", signature.ToArray());
        hostile.Type("Test", "Caller");
        var root = hostile.Method("Run", [0, 0, 8], emit: il => { il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Ldnull); il.Call(call); il.OpCode(ILOpCode.Ret); });
        var caller = scope.OpenInput(files.Write("enum-caller.dll", hostile.Build())).Value!;
        var resultCaller = ValueTranslator.TranslateIntegerObjects(scope, caller, root, implementation);
        Assert.False(resultCaller.Success);
        Assert.Contains(resultCaller.Diagnostics, d => d.Message.Contains("Explicit type use requires lexical declaring scope", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Native_queries_reject_foreign_and_invalid_references(bool optimized)
    {
        var program = (await Programs.Value)[0];
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        var values = program.Flow.Flow.Flow.Types;
        var implementation = values.References.Types.Single(t => t.Source.Name == "Cilica.Runtime.RuntimeTypeObject").Source;
        var scalar = values.Types.Single(t => t.Source.Name == "Cilica.Fixtures.Execution.DefinedSparse").Source;
        string type = $"t_{implementation.ContentHash}_{implementation.Token:x8}", box = $"box_{scalar.ContentHash}_{scalar.Token:x8}";
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                auto* type = runtime_type<{{type}}>(c, {{type}}_descriptor, enum_definitions[0].identity);
                auto* value = allocate_box_storage(c, &{{box}}_descriptor);
                assert(type != nullptr && value != nullptr && c.pending == 0);
                auto* bits = static_cast<std::uint32_t*>(value->type->accessors[0](value));
                *bits = 0xfffffff9u;
                assert(enum_query(c, type, value) == 1u && c.pending == 0);
                *bits = 7u;
                assert(enum_query(c, type, value) == 0u && c.pending == 0);
                assert(enum_query(c, type, nullptr) == 0u && c.pending == 4u); c.pending = 0;
                assert(enum_query(c, type, reinterpret_cast<Object*>(1)) == 0u && c.pending == 6u); c.pending = 0;
                Instance other; Context d(other);
                auto* foreign_type = runtime_type<{{type}}>(d, {{type}}_descriptor, enum_definitions[0].identity);
                auto* foreign_value = allocate_box_storage(d, &{{box}}_descriptor);
                assert(enum_query(c, type, foreign_value) == 0u && c.pending == 6u); c.pending = 0;
                assert(enum_query(c, foreign_type, value) == 0u && c.pending == 6u); c.pending = 0;
                assert(enum_error_message(c, {{type}}_descriptor, type, value) == nullptr && c.pending == 6u);
            }
            """;
        using var files = new ResolverFiles();
        string directory = files.DirectoryPath("enum-ownership"), cpp = Path.Combine(directory, "query.cpp"), exe = Path.Combine(directory, "query");
        File.WriteAllText(cpp, source);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
             "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
