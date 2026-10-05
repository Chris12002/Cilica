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
public sealed class StringBuilderLibraryTests
{
    private static readonly (string Name, Func<int, int, int> Oracle)[] Probes = [
        ("Text", StringBuilderProbe.Text), ("Characters", StringBuilderProbe.Characters),
        ("Snapshots", StringBuilderProbe.Snapshots), ("NullReceiver", StringBuilderProbe.NullReceiver),
        ("LargeChunks", StringBuilderProbe.LargeChunks), ("CodeUnitAt", StringBuilderProbe.CodeUnitAt)];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(StringBuilderProbe), Probes.Select(p => p.Name).ToArray()));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Chunked_append_utf16_snapshots_and_identity_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            Probes.Take(4).Select((p, i) => (p.Name, programs[i], p.Oracle)).ToArray());
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("LargeChunks", programs[4], StringBuilderProbe.LargeChunks)], [(0, 0), (15, -1)]);
        var pairs = (from input in new[] { 0, 1, -1, 0xd7ff, 0xd800, 0xdc00, 0xdfff, 0xffff, 65536, int.MinValue }
                     from index in Enumerable.Range(-1, 48) select (input, index)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("CodeUnitAt", programs[5], StringBuilderProbe.CodeUnitAt)], pairs);
    }
    [Fact]
    public async Task String_construction_requires_exact_grant_and_private_caller()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions {
            Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var narrowed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != LibraryIntrinsicCapability.StringFromCodeUnits)] }] };
        var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
        var input = resolver.OpenInput(typeof(StringBuilderProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name) => (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(StringBuilderProbe).GetMethod(name)!.MetadataToken);
        using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(denied, input, Method("Text"), implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        var unsupported = ValueTranslator.TranslateIntegerObjects(scope, input, Method("UnsupportedLengthSetter"), implementation);
        Assert.False(unsupported.Success); Assert.Contains(unsupported.Diagnostics, d => d.Message.Contains("no implemented managed body", StringComparison.Ordinal));
        var builder = new ResolutionAssemblyBuilder(new("HostileStringConstruction", new Version(1, 0, 0, 0)));
        var owner = builder.TypeReference("Cilica.Runtime", "StringConstruction", builder.Reference(implementation.Identity));
        var member = builder.Member(owner, "FromCodeUnits", 0, 1, 0x0e, 0x1d, 8);
        builder.Type("Test", "Caller");
        var method = builder.Method("Run", [0, 0, 8], emit: il => {
            il.OpCode(ILOpCode.Ldnull); il.Call(member); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        var hostile = scope.OpenInput(files.Write("hostile.dll", builder.Build())).Value!;
        var rejected = ValueTranslator.TranslateIntegerObjects(scope, hostile, method, implementation);
        Assert.False(rejected.Success); Assert.Contains(rejected.Diagnostics, d => d.Message.Contains("private to the selected core", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Private_code_unit_snapshot_copies_bits_checks_null_and_reuses_only_empty(bool optimized)
    {
        using var files = new ResolverFiles();
        const string source = """
            namespace Cilica.Runtime {
                public static class CodeUnitProbe {
                    public static int Run(int input, int mode) {
                        var values = new[] { input, mode, 0x1dfff };
                        string first = StringConstruction.FromCodeUnits(values);
                        string second = StringConstruction.FromCodeUnits(values);
                        values[0] = 0;
                        int bits = (object)first == (object)second ? 1 : 2;
                        string empty = StringConstruction.FromCodeUnits(new int[0]);
                        bits += (object)empty == (object)"" ? 4 : 8;
                        try { StringConstruction.FromCodeUnits(null); }
                        catch (System.NullReferenceException) { bits += 16; }
                        return unchecked(first[0] + first[1] * 31 + first[2] * 1009 + bits);
                    }
                }
            }
            """;
        string path = files.Write("code-units.cs", System.Text.Encoding.UTF8.GetBytes(source));
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files, path);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var type = scope.ResolveType(implementation, "Cilica.Runtime", "CodeUnitProbe").Value!;
        var method = scope.ResolveClosedDeclaration(new(type, SignatureList<TypeSignature>.Empty), "Run", 2, new()).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, implementation, method.Handle, implementation);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        static int Oracle(int input, int mode)
        {
            var values = new[] { (char)input, (char)mode, (char)0xdfff };
            string first = new(values); string second = new(values); values[0] = (char)0;
            int bits = (object)first == (object)second ? 1 : 2;
            string empty = new(Array.Empty<char>()); bits += (object)empty == (object)"" ? 4 : 8;
            char[] missing = null!;
            try { _ = missing.Length; } catch (NullReferenceException) { bits += 16; }
            return unchecked(first[0] + first[1] * 31 + first[2] * 1009 + bits);
        }
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("Units", result.Program!, Oracle)]);
    }

    [Theory]
    [InlineData("type")] [InlineData("operand")] [InlineData("fault")]
    public async Task Detached_string_construction_requires_typed_earlier_array_and_throwing_flag(string mutation)
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        var method = flow.Methods.First(m => m.Blocks.Any(b => b.Statements.Any(s => s is IrStatement.Define { Value.Expression: IrExpression.StringFromCodeUnits })));
        var block = method.Blocks.First(b => b.Statements.Any(s => s is IrStatement.Define { Value.Expression: IrExpression.StringFromCodeUnits }));
        var statement = (IrStatement.Define)block.Statements.First(s => s is IrStatement.Define { Value.Expression: IrExpression.StringFromCodeUnits });
        var value = statement.Value;
        var changed = mutation switch {
            "type" => value with { Type = IrType.Int32 },
            "operand" => value with { Expression = new IrExpression.StringFromCodeUnits(value.Id) },
            _ => value };
        var badBlock = block with { Statements = block.Statements.Replace(statement, statement with { Value = changed, Exceptional = mutation == "fault" ? null : statement.Exceptional }) };
        var badMethod = method with { Blocks = method.Blocks.Replace(block, badBlock) };
        var badFlow = flow with { Methods = flow.Methods.Replace(method, badMethod) };
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = badFlow } } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(invalid));
    }
}
