using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class NarrowVectorTests
{
    private static readonly string[] Names = ["Bytes", "Chars", "Loops", "Generic", "Heap", "Empty", "Types", "RuntimeTypes", "CastMessages", "LoadFaults", "StoreFaults"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(NarrowVectorProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Narrow_vectors_preserve_values_identity_and_faults(bool optimized, int group)
    {
        var programs = await Programs.Value;
        int[] inputs = [int.MinValue, -65537, -1, 0, 1, 2, 127, 128, 255, 256, 32767, 32768, 55296, 56320, 65535, 65536, int.MaxValue];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(NarrowVectorProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group * 4).Take(4).ToArray(),
            pairs: inputs.SelectMany(a => new[] {-1,0,1,2,int.MaxValue}.Select(b => (a,b))).ToArray());
    }
    [Fact]
    public async Task Narrow_borrows_interfaces_and_other_widths_remain_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(NarrowVectorProbe).Assembly.Location).Value!;
        foreach (var name in new[] { "UnsupportedBorrow", "UnsupportedInterface", "UnsupportedSignedArray" })
        {
            var result = ValueTranslator.TranslateIntegerObjects(scope, input,
                (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(NarrowVectorProbe).GetMethod(name)!.MetadataToken), implementation);
            Assert.False(result.Success, name); Assert.Null(result.Program); Assert.NotEmpty(result.Diagnostics);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Raw_stores_truncate_without_a_source_conversion(bool optimized)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var builder = new ResolutionAssemblyBuilder(new("RawNarrowStores", new Version(1,0,0,0)));
        var core = builder.Reference(implementation.Identity);
        var bytes = builder.TypeReference("System", "Byte", core);
        var chars = builder.TypeReference("System", "Char", core);
        builder.Type("Test", "Stores");
        MethodDefinitionHandle Root(bool character) => builder.Method(character ? "Chars" : "Bytes", [0,2,8,8,8], emit: il => {
            il.LoadConstantI4(1); il.OpCode(ILOpCode.Newarr); il.Token(character ? chars : bytes);
            il.OpCode(ILOpCode.Dup); il.LoadConstantI4(0); il.LoadArgument(0);
            il.OpCode(character ? ILOpCode.Stelem_i2 : ILOpCode.Stelem_i1);
            il.LoadConstantI4(0); il.OpCode(character ? ILOpCode.Ldelem_u2 : ILOpCode.Ldelem_u1);
            il.LoadArgument(1); il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Ret);
        });
        var byteRoot = Root(false); var charRoot = Root(true);
        var input = scope.OpenInput(files.Write("stores.dll", builder.Build())).Value!;
        ManagedExceptionIrProgram Translate(MethodDefinitionHandle method)
        {
            var result = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
            Assert.True(result.Success, string.Join("; ", result.Diagnostics)); return result.Program!;
        }
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("RawByte", Translate(byteRoot), (a,b) => unchecked((byte)a + b)),
             ("RawChar", Translate(charRoot), (a,b) => unchecked((char)a + b))]);
    }

    [Theory]
    [InlineData("element")] [InlineData("fault")]
    public async Task Both_stages_reject_mismatched_array_identity_and_missing_fault_edges(string mutation)
    {
        var program = (await Programs.Value)[0];
        var flow = program.Flow.Flow.Flow;
        System.Collections.Immutable.ImmutableArray<IrMethod> Alter(System.Collections.Immutable.ImmutableArray<IrMethod> methods) =>
            [.. methods.Select(m => m with { Blocks = [.. m.Blocks.Select(b => b with { Statements = [.. b.Statements.Select(statement =>
                statement is IrStatement.StoreElement s ? mutation == "element" ? s with {
                    ArrayType = ObjectTypeId.Vector(flow.Types.References.ObjectBase, ObjectArrayElement.Char) } : s with { Exceptional = (IrExceptionTarget)99 } : statement)] })] })];
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program with { Flow = program.Flow with {
            Flow = program.Flow.Flow with { Flow = flow with { Methods = Alter(flow.Methods) } } } }));
        var scalar = ValueScalarLowering.LowerExceptions(program); var boxes = scalar.Flow.Flow; var objects = boxes.Flow.Flow;
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(scalar with { Flow = scalar.Flow with {
            Flow = boxes with { Flow = boxes.Flow with { Flow = objects with { Flow = objects.Flow with { Methods = Alter(objects.Flow.Methods) } } } } } }));
    }
}
