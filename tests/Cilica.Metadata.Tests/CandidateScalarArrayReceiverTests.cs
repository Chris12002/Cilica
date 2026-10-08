using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class CandidateScalarArrayReceiverTests
{
    private static readonly string[] Names = ["EqualsElement", "EqualsAfterArgumentMutation", "ReplaceArrayAndCopy", "Reborrow", "UnsignedAlias", "EnumAlias", "FaultOrder", "CarrierKinds"];
    internal static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateScalarArrayReceiverProbe), Names));
    public static IEnumerable<object[]> Cases() => from name in Names from optimized in new[] { false, true } select new object[] { name, optimized };
    [Theory] [MemberData(nameof(Cases))]
    public async Task Element_locations_preserve_mutation_copy_roots_reborrows_aliases_and_fault_order(string name, bool optimized)
    {
        // In particular, the receiver value is observed after argument mutation.
        Assert.Equal(1, CandidateScalarArrayReceiverProbe.EqualsAfterArgumentMutation(1, 2));
        Assert.Equal(19, CandidateScalarArrayReceiverProbe.UnsignedAlias(1, 2));
        Assert.Equal(4, CandidateScalarArrayReceiverProbe.EnumAlias(1, 2));
        var program = (await Programs.Value)[Array.IndexOf(Names, name)];
        Assert.Contains(program.Flow.Flow.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values),
            v => v.Expression is IrExpression.AddressElement { ArrayType.ArrayElement.Kind: ObjectArrayElementKind.Int32 });
        var scalar = ValueScalarLowering.LowerExceptions(program);
        Assert.Contains(ObjectRootAssignment.AssignExceptions(scalar).SelectMany(p => p.Slots), s => s.LocationOwner);
        var oracle = typeof(CandidateScalarArrayReceiverProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [(name, program, oracle)]);
    }

    [Theory]
    [InlineData("Byte")] [InlineData("Char")] [InlineData("Boolean")] [InlineData("UInt32")] [InlineData("Int64")]
    [InlineData("native-escape")] [InlineData("legacy")]
    public async Task Other_scalar_views_native_escapes_and_legacy_profile_remain_diagnostic(string kind)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var builder = new ResolutionAssemblyBuilder(new("ScalarArray" + kind, new Version(1, 0, 0, 0)));
        var integer = builder.TypeReference("System", kind is "native-escape" or "legacy" ? "Int32" : kind, builder.Reference(implementation.Identity));
        builder.Type("Test", "Root");
        var root = builder.Method("Run", [0, 2, 8, 8, 8], emit: il => {
            il.LoadConstantI4(1); il.OpCode(ILOpCode.Newarr); il.Token(integer); il.LoadConstantI4(0);
            il.OpCode(ILOpCode.Ldelema); il.Token(integer);
            if (kind == "native-escape") il.OpCode(ILOpCode.Conv_u);
            il.OpCode(ILOpCode.Pop); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        var input = scope.OpenInput(files.Write(kind + ".dll", builder.Build())).Value!;
        var result = kind == "legacy" ? ValueTranslator.TranslateExceptions(scope, input, root, implementation)
            : ValueTranslator.TranslateIntegerObjects(scope, input, root, implementation);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Id == "CIL2001");
        if (kind is not ("native-escape" or "legacy"))
            Assert.Contains(result.Diagnostics, d => d.Message.Contains("Element addresses currently require", StringComparison.Ordinal));
    }
}
