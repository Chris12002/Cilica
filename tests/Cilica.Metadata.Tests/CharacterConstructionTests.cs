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

public sealed class CharacterConstructionTests
{
    private static readonly string[] Names = ["Single", "Identity", "Caught"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(CharacterConstructionProbe), Names));

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Character_strings_preserve_identity_utf16_and_roots(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            Names.Select((n, i) => (n, programs[i], typeof(CharacterConstructionProbe).GetMethod(n)!.CreateDelegate<Func<int, int, int>>())).ToArray(),
            pairs: (from a in new[] { int.MinValue, -1, 0, 1, 255, 256, 0xd7ff, 0xd800, 0xdc00, 0xdfff, 0xffff, 65536, int.MaxValue }
                    from b in new[] { 0, 1, -1 } select (a, b)).ToArray());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Every_code_unit_uses_one_allocation_and_failures_keep_managed_origins(bool optimized)
    {
        var programs = await Programs.Value;
        var source = new System.Text.StringBuilder();
        var calls = new System.Text.StringBuilder();
        foreach (int index in new[] { 0, 2 })
        {
            var program = programs[index]; var flow = program.Flow.Flow.Flow;
            var allocation = flow.Methods.Single(m => m.Blocks.Any(b => b.Values.Any(v => v.Expression is IrExpression.StringFromCharacter)));
            int offset = allocation.Blocks.SelectMany(b => b.Values).Single(v => v.Expression is IrExpression.StringFromCharacter).ILOffset;
            Assert.DoesNotContain(flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values), v => v.Expression is IrExpression.NewArray);
            var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
            string symbol = "cilica_m_" + flow.Entry.ContentHash + "_" + flow.Entry.Token.ToString("x8", CultureInfo.InvariantCulture);
            source.Append(artifact.Text).Append(CultureInfo.InvariantCulture, $$"""
                #include <cassert>
                namespace test_{{index}} {
                using namespace {{artifact.EntryPoint[..^4]}}_closure;
                static unsigned allocations;
                static void observe(Context&) noexcept { ++allocations; }
                static void run() {
                    Instance heap; Context c(heap); heap.collect_before_allocation = true;
                    assert(prepare_exceptions(c) == 0); c.observe = observe;
                    LocationArgument args[2]{}; args[1].bits = 77;
                    if ({{index}} == 0) {
                        for (unsigned unit = 0; unit <= 65535; ++unit) {
                            args[0].bits = unit; allocations = 0;
                            assert({{symbol}}(c, args) == static_cast<std::int32_t>(unit));
                            assert(c.pending == 0 && allocations == 1 && heap.count == 2);
                            assert(c.used == 0 && c.depth == 0 && c.top == nullptr && c.borrows == nullptr);
                            assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                        }
                    }
                    heap.object_limit = 1; allocations = 0;
                    const auto failed = {{symbol}}(c, args);
                    if ({{index}} == 0) {
                        assert(failed == 0 && c.pending == 5 && c.method == {{allocation.Source.Token}}u && c.il == {{offset}}u);
                    } else assert(failed == 77 && c.pending == 0 && c.exception == nullptr);
                    assert(c.used == 0 && c.depth == 0 && c.top == nullptr && c.borrows == nullptr);
                    assert(heap.count == 1 && allocations == 1);
                }
                }
                """).Append('\n');
            calls.Append("test_").Append(index).Append("::run();\n");
        }
        source.Append("int main() {\n").Append(calls).Append("}\n");
        using var files = new ResolverFiles(); string dir = files.DirectoryPath("char-native");
        string cpp = Path.Combine(dir, "chars.cpp"), exe = Path.Combine(dir, "chars");
        await File.WriteAllTextAsync(cpp, source.ToString(), TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", optimized ? "-O2" : "-O0", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], dir);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], dir);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }

    [Fact]
    public async Task Direct_character_allocation_requires_exact_grant_and_private_caller()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions {
            Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(manifest with { Artifacts = [artifact with {
            Grants = [.. artifact.Grants.Where(g => g.Capability != LibraryIntrinsicCapability.StringFromCharacter)] }] });
        var input = resolver.OpenInput(typeof(CharacterConstructionProbe).Assembly.Location).Value!;
        var root = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CharacterConstructionProbe).GetMethod("Single")!.MetadataToken);
        using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(denied, input, root, implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        var builder = new ResolutionAssemblyBuilder(new("HostileCharConstruction", new Version(1, 0, 0, 0)));
        var owner = builder.TypeReference("Cilica.Runtime", "StringConstruction", builder.Reference(implementation.Identity));
        var member = builder.Member(owner, "FromCharacter", 0, 1, 0x0e, 3);
        builder.Type("Test", "Caller");
        var method = builder.Method("Run", [0, 0, 8], emit: il => {
            il.LoadConstantI4(65); il.Call(member); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        var hostile = scope.OpenInput(files.Write("hostile.dll", builder.Build())).Value!;
        var rejected = ValueTranslator.TranslateIntegerObjects(scope, hostile, method, implementation);
        Assert.False(rejected.Success); Assert.Contains(rejected.Diagnostics, d => d.Message.Contains("lexical declaring scope", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("type")] [InlineData("operand")] [InlineData("fault")]
    public async Task Both_detached_stages_reject_invalid_character_allocation(string mutation)
    {
        var program = (await Programs.Value)[0];
        ImmutableArray<IrMethod> Alter(ImmutableArray<IrMethod> methods) => [.. methods.Select(m => m with {
            Blocks = [.. m.Blocks.Select(b => b with { Statements = [.. b.Statements.Select(s => {
                if (s is not IrStatement.Define { Value.Expression: IrExpression.StringFromCharacter } d) return s;
                return d with { Value = mutation switch {
                    "type" => d.Value with { Type = IrType.Int32 },
                    "operand" => d.Value with { Expression = new IrExpression.StringFromCharacter(d.Value.Id) },
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
