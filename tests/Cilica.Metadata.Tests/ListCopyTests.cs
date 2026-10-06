using System;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.IO;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Metadata;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cilica.Metadata.Resolution;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ListCopyTests
{
    private static readonly string[] Names = ["Integers", "References", "Values", "Faults", "Covariant", "EmptyValue"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ListCopyProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    public async Task Copies_fault_order_and_partial_covariant_stores_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ListCopyProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }
    [Fact]
    public async Task Sections_and_nongeneric_copy_stay_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(ListCopyProbe).Assembly.Location).Value!;
        foreach (string name in new[] { "UnsupportedSection", "UnsupportedLegacy" })
        {
            var result = ValueTranslator.TranslateIntegerObjects(scope, input, (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ListCopyProbe).GetMethod(name)!.MetadataToken), implementation);
            Assert.False(result.Success); Assert.NotEmpty(result.Diagnostics);
        }
    }    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Direct_call_on_null_list_faults_before_null_destination(bool optimized)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var b = new ResolutionAssemblyBuilder(new AssemblyIdentity("DirectNullListCopy", new Version(1, 0, 0, 0)));
        var core = b.Reference(implementation.Identity);
        var obj = b.TypeReference("System", "Object", core);
        var list = b.TypeReference("System.Collections.Generic", "List`1", core);
        var nullFault = b.TypeReference("System", "NullReferenceException", core);
        var argumentFault = b.TypeReference("System", "ArgumentException", core);
        byte code = checked((byte)((MetadataTokens.GetRowNumber(list) << 2) | 1));
        var closed = b.Specification(0x15, 0x12, code, 1, 8);
        var copy = b.Member(closed, "CopyTo", 0x20, 2, 1, 0x1d, 0x13, 0, 8);
        b.Type("Example", "DirectNullListCopy", baseType: obj);
        var root = b.Method("Run", [0, 2, 8, 8, 8], locals: [7, 1, 8], emit: il => {
            var start = il.DefineLabel(); var nullHandler = il.DefineLabel(); var otherHandler = il.DefineLabel(); var done = il.DefineLabel();
            il.MarkLabel(start); il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Ldnull); il.LoadConstantI4(-1); il.Call(copy);
            il.LoadConstantI4(-100); il.StoreLocal(0); il.Branch(ILOpCode.Leave, done);
            il.MarkLabel(nullHandler); il.OpCode(ILOpCode.Pop); il.LoadArgument(0); il.StoreLocal(0); il.Branch(ILOpCode.Leave, done);
            il.MarkLabel(otherHandler); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(-101); il.StoreLocal(0); il.Branch(ILOpCode.Leave, done);
            il.MarkLabel(done); il.LoadLocal(0); il.OpCode(ILOpCode.Ret);
            il.ControlFlowBuilder!.AddCatchRegion(start, nullHandler, nullHandler, otherHandler, nullFault);
            il.ControlFlowBuilder.AddCatchRegion(start, nullHandler, otherHandler, done, argumentFault);
        });
        var input = resolver.OpenInput(files.Write("direct-null-copy.dll", b.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, root, implementation);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("DirectNullCopy", result.Program!, (a, _) => a)]);
    }
    [Fact]
    public async Task Copy_store_requires_its_exact_grant_and_stays_private()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var grant = Assert.Single(artifact.Grants.Where(g => g.Capability == LibraryIntrinsicCapability.ArrayCopyStore));
        var narrowed = AssemblyResolver.SerializeLibraryBuildManifest(manifest with { Artifacts = [artifact with { Grants = artifact.Grants.Remove(grant) }] });
        using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, narrowed, Convert.ToHexStringLower(SHA256.HashData(narrowed))).Value!;
        var input = resolver.OpenInput(typeof(ListCopyProbe).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(denied, input, (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ListCopyProbe).GetMethod("Integers")!.MetadataToken), implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        var b = new ResolutionAssemblyBuilder(new AssemblyIdentity("ExternalCopyStore", new Version(1, 0, 0, 0)));
        var core = b.Reference(implementation.Identity);
        var owner = b.TypeReference("Cilica.Runtime", "ArrayInterface`1", core);
        byte code = checked((byte)((MetadataTokens.GetRowNumber(owner) << 2) | 1));
        var spec = b.Specification(0x15, 0x12, code, 1, 8);
        var call = b.Member(spec, "CopyStore", 0, 3, 1, 0x1d, 0x13, 0, 8, 0x13, 0);
        b.Type("Example", "ExternalCopyStore", baseType: b.TypeReference("System", "Object", core));
        var root = b.Method("Run", [0, 0, 8], emit: il => { il.OpCode(ILOpCode.Ldnull); il.LoadConstantI4(0); il.LoadConstantI4(1); il.Call(call); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret); });
        var hostile = resolver.OpenInput(files.Write("external-copy.dll", b.Build())).Value!;
        var failure = ValueTranslator.TranslateIntegerObjects(scope, hostile, root, implementation);
        Assert.False(failure.Success); Assert.Contains(failure.Diagnostics, d => d.Message.Contains("lexical declaring scope", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData("array")] [InlineData("index")] [InlineData("value")] [InlineData("fault")]
    public async Task Copy_stores_validate_packets_and_fault_edges_at_both_stages(string mutation)
    {
        var program = (await Programs.Value)[4]; var flow = program.Flow.Flow.Flow;
        IrMethod Change(IrMethod method) => method with { Blocks = [.. method.Blocks.Select(block => block with { Statements = [.. block.Statements.Select(statement => statement is IrStatement.StoreElement { CopyCast: true } store
            ? mutation switch { "array" => store with { Array = store.Index }, "index" => store with { Index = store.Value },
                "value" => store with { Value = store.Index }, _ => store with { Exceptional = (IrExceptionTarget)9 } } : statement)] })] };
        Assert.Contains(flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Statements), s => s is IrStatement.StoreElement { CopyCast: true });
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Methods = [.. flow.Methods.Select(Change)] } } } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program); var scalarFlow = scalar.Flow.Flow.Flow.Flow.Flow;
        var invalidScalar = scalar with { Flow = scalar.Flow with { Flow = scalar.Flow.Flow with { Flow = scalar.Flow.Flow.Flow with { Flow = scalar.Flow.Flow.Flow.Flow with { Flow = scalarFlow with { Methods = [.. scalarFlow.Methods.Select(Change)] } } } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar));
    }

}
