using System;
using System.Linq;
using System.IO;
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
public sealed class EnumFormattingTests
{
    private static readonly string[] Names = ["Names", "Empty", "Pair", "Mixed", "MixedFirst", "FaultOrder"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(EnumFormattingProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    [InlineData(false, 4)] [InlineData(true, 4)] [InlineData(false, 5)] [InlineData(true, 5)]
    public async Task Names_numeric_formats_composites_and_faults_match_dotnet(bool optimized, int probe)
    {
        var programs = await Programs.Value;
        var pairs = (from a in new[] { int.MinValue, int.MinValue + 1, -7, -2, -1, 0, 1, 2, 3, 4, 5, 7, 8, 9, 15, 16, int.MaxValue }
            from b in Enumerable.Range(0, 13) select (a, b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [(Names[probe], programs[probe], typeof(EnumFormattingProbe).GetMethod(Names[probe])!.CreateDelegate<Func<int,int,int>>())], pairs: pairs);
    }
    [Fact]
    public async Task Attribute_alias_carrier_flow_and_grants_remain_explicit()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var input = resolver.OpenInput(typeof(EnumFormattingProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name) => (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(EnumFormattingProbe).GetMethod(name)!.MetadataToken);
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        foreach (var name in new[] { "UnsupportedFlags", "UnsupportedAliases", "UnsupportedAttributes", "UnsupportedWide", "UnsupportedAlias" })
        {
            var result = ValueTranslator.TranslateIntegerObjects(scope, input, Method(name), implementation);
            Assert.False(result.Success); Assert.Null(result.Program);
            Assert.Contains(result.Diagnostics, d => d.Message.Contains(name == "UnsupportedAliases" ? "duplicate-value" : name is "UnsupportedFlags" or "UnsupportedAttributes" ? "custom attributes" : name == "UnsupportedAlias" ? "directly boxed" : "Int32", StringComparison.Ordinal));
        }
        var hostile = new ResolutionAssemblyBuilder(new("HostileEnumFormatting", new Version(1,0,0,0)));
        var helper = hostile.TypeReference("Cilica.Runtime", "EnumText`1", hostile.Reference(implementation.Identity));
        var payload = hostile.TypeReference("Cilica.Fixtures.Execution", "TextEnum", hostile.Reference(input.Identity));
        var owner = hostile.Specification(0x15, 0x12, checked((byte)((MetadataTokens.GetRowNumber(helper) << 2) | 1)), 1,
            0x11, checked((byte)((MetadataTokens.GetRowNumber(payload) << 2) | 1)));
        var call = hostile.Member(owner, "Count", 0, 0, 8);
        hostile.Type("Test", "Caller");
        var root = hostile.Method("Run", [0,0,8], emit: il => { il.Call(call); il.OpCode(ILOpCode.Ret); });
        var hostileImage = scope.OpenInput(files.Write("hostile.dll", hostile.Build())).Value!;
        var rejected = ValueTranslator.TranslateIntegerObjects(scope, hostileImage, root, implementation);
        Assert.False(rejected.Success); Assert.Contains(rejected.Diagnostics, d => d.Message.Contains("lexical declaring scope", StringComparison.Ordinal));
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        foreach (var grant in artifact.Grants.Where(g => g.Capability == LibraryIntrinsicCapability.EnumFormatting))
        {
            var changed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g != grant)] }] };
            var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(changed);
            using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
            var result = ValueTranslator.TranslateIntegerObjects(denied, input, Method("Names"), implementation);
            Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Native_table_bounds_descriptors_name_copies_and_quota_recovery_are_checked(bool optimized)
    {
        var program = (await Programs.Value)[0];
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        var catalog = program.Flow.Flow.Flow.Types;
        var value = catalog.Types.Single(t => t.Source.Name == "Cilica.Fixtures.Execution.TextEnum").Source;
        var text = catalog.References.String!;
        string table = $"enum_entries_{value.ContentHash}_{value.Token:x8}", descriptor = $"t_{text.ContentHash}_{text.Token:x8}_descriptor";
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                assert(enum_entry_value(c, {{table}}.data(), {{table}}.size(), 0u) == 0u && c.pending == 0u);
                assert(enum_entry_value(c, {{table}}.data(), {{table}}.size(), 0xffffffffu) == 0u && c.pending == 6u); c.pending = 0u;
                assert(enum_entry_name(c, {{descriptor}}, {{table}}.data(), {{table}}.size(), {{table}}.size()) == nullptr && c.pending == 6u); c.pending = 0u;
                assert(enum_entry_name(c, box_{{value.ContentHash}}_{{value.Token:x8}}_descriptor, {{table}}.data(), {{table}}.size(), 0u) == nullptr && c.pending == 6u); c.pending = 0u;
                auto* name = enum_entry_name(c, {{descriptor}}, {{table}}.data(), {{table}}.size(), 0u);
                assert(name != nullptr && c.pending == 0u && string_length(name) == 4u && string_code_unit(name, 0) == 'Z');
                heap.limit = 0;
                assert(enum_entry_name(c, {{descriptor}}, {{table}}.data(), {{table}}.size(), 0u) == nullptr && c.pending == 0u);
                heap.limit = 1048576;
                assert(enum_entry_name(c, {{descriptor}}, {{table}}.data(), {{table}}.size(), 0u) != nullptr && c.pending == 0u);
            }
            """;
        using var files = new ResolverFiles();
        string directory = files.DirectoryPath("enum-tables"), cpp = Path.Combine(directory, "table.cpp"), exe = Path.Combine(directory, "table");
        File.WriteAllText(cpp, source);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
             "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }

    [Theory]
    [InlineData("missing")] [InlineData("type")] [InlineData("forward")] [InlineData("result")] [InlineData("fault")]
    public async Task Both_stages_reject_invalid_enum_table_reads(string mutation)
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        ValueTypeCatalog Values(ValueTypeCatalog values)
        {
            var target = values.Types.Single(t => t.Source.Name == "Cilica.Fixtures.Execution.TextEnum");
            return mutation == "missing" ? values with { Types = values.Types.Replace(target, target with { EnumConstants = default }) } : values;
        }
        System.Collections.Immutable.ImmutableArray<IrMethod> Methods(System.Collections.Immutable.ImmutableArray<IrMethod> methods) =>
            [.. methods.Select(m => m with { Blocks = [.. m.Blocks.Select(b => b with { Statements = [.. b.Statements.Select(s =>
                s is IrStatement.Define d && d.Value.Expression is IrExpression.EnumEntry entry ? mutation switch {
                    "type" => d with { Value = d.Value with { Expression = entry with { EnumType = flow.Types.References.ObjectBase } } },
                    "forward" => d with { Value = d.Value with { Expression = entry with { Index = d.Value.Id } } },
                    "result" => d with { Value = d.Value with { Expression = entry with { Name = !entry.Name } } },
                    "fault" => d with { Exceptional = null }, _ => s } : s)] })] })];
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Types = Values(flow.Types), Methods = Methods(flow.Methods) } } } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program); var boxes = scalar.Flow.Flow; var objects = boxes.Flow.Flow;
        var invalidScalar = scalar with { Flow = scalar.Flow with { Flow = boxes with { Values = Values(boxes.Values), Flow = boxes.Flow with { Flow = objects with { Flow = objects.Flow with { Methods = Methods(objects.Flow.Methods) } } } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar));
    }
}
