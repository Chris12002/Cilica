using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class LocationJoinTests
{
    private static readonly string[] Names = ["Local", "Parameter", "BorrowedParameter", "ReferenceValue", "Faults", "UpdatedContents"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(LocationJoinProbe), Names));
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Stable_slots_keep_aliases_references_and_exception_order(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(LocationJoinProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).ToArray());
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Same_slot_joins_execute_and_distinct_slots_remain_diagnostic(bool optimized)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        foreach (string kind in new[] { "same", "different-local", "different-kind" })
        {
            var builder = new ResolutionAssemblyBuilder(new("LocationJoin" + kind, new Version(1, 0, 0, 0)));
            builder.Type("Test", "Root");
            var root = builder.Method("Run", [0, 2, 8, 8, 8], locals: [7, 2, 8, 8], emit: il => {
                var other = il.DefineLabel(); var join = il.DefineLabel();
                il.LoadArgument(0); il.StoreLocal(0); il.LoadArgument(0); il.Branch(ILOpCode.Brtrue, other);
                il.LoadLocalAddress(0); il.Branch(ILOpCode.Br, join); il.MarkLabel(other);
                if (kind == "different-kind") il.LoadArgumentAddress(0);
                else il.LoadLocalAddress(kind == "different-local" ? 1 : 0);
                il.MarkLabel(join); il.LoadArgument(1); il.OpCode(ILOpCode.Stind_i4); il.LoadLocal(0); il.OpCode(ILOpCode.Ret);
            });
            var input = scope.OpenInput(files.Write(kind + ".dll", builder.Build())).Value!;
            var result = ValueTranslator.TranslateIntegerObjects(scope, input, root, implementation);
            if (kind == "same")
            {
                Assert.True(result.Success, string.Join("; ", result.Diagnostics));
                await IntegerObjectTranslationTests.VerifyTranslated(optimized, [(kind, result.Program!, (int a, int b) => b)]);
                var legacy = ValueTranslator.TranslateExceptions(scope, input, root, implementation);
                Assert.False(legacy.Success); Assert.Contains(legacy.Diagnostics, d => d.Message.Contains("cannot cross block joins", StringComparison.Ordinal));
            }
            else
            {
                Assert.False(result.Success); Assert.Null(result.Program);
                Assert.Contains(result.Diagnostics, d => d.Message.Contains("identity differs across incoming edges", StringComparison.Ordinal));
            }
        }
    }
    [Fact]
    public async Task Heap_and_array_element_addresses_keep_their_lifetime_boundary()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(LocationJoinProbe).Assembly.Location).Value!;
        foreach (string name in new[] { "HeapAddress", "ArrayAddress" })
        {
            var result = ValueTranslator.TranslateIntegerObjects(scope, input,
                (System.Reflection.Metadata.MethodDefinitionHandle)System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(typeof(LocationJoinProbe).GetMethod(name)!.MetadataToken), implementation);
            Assert.False(result.Success); Assert.Null(result.Program);
            Assert.Contains(result.Diagnostics, d => d.Message.Contains("stable local/parameter slots", StringComparison.Ordinal));
        }
    }
    [Fact]
    public async Task Lowered_blocks_never_transport_locations_as_phi_inputs()
    {
        foreach (var program in await Programs.Value)
        {
            var scalar = ValueScalarLowering.LowerExceptions(program);
            foreach (var block in scalar.Flow.Flow.Flow.Flow.Flow.Methods.SelectMany(m => m.Blocks))
                Assert.DoesNotContain(block.Inputs, type => type.Kind == IrTypeKind.ManagedLocation);
            ScalarManagedExceptionIrValidator.Validate(scalar);
        }
    }
}
