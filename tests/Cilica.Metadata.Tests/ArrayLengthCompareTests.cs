using System;
using System.Linq;
using System.IO;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ArrayLengthCompareTests
{
    private static readonly string[] Names = ["Nonempty", "Empty", "SelectedArray", "FaultOrder"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ArrayLengthCompareProbe), Names));
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Zero_comparisons_preserve_selected_array_faults_and_boolean_results(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ArrayLengthCompareProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).ToArray());
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Exact_native_size_zero_comparisons_match_dotnet_and_keep_legacy_boundary(bool optimized)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        foreach (var comparison in new[] { ILOpCode.Ceq, ILOpCode.Cgt_un })
        {
            var builder = new ResolutionAssemblyBuilder(new("ZeroLength" + comparison, new Version(1, 0, 0, 0)));
            var integer = builder.TypeReference("System", "Int32", builder.Reference(implementation.Identity));
            builder.Type("Test", "Root");
            var root = builder.Method("Run", [0, 2, 8, 8, 8], emit: il => {
                il.LoadArgument(0); il.LoadConstantI4(3); il.OpCode(ILOpCode.And); il.OpCode(ILOpCode.Newarr); il.Token(integer);
                il.OpCode(ILOpCode.Ldlen); il.LoadConstantI4(0); il.OpCode(comparison); il.OpCode(ILOpCode.Ret);
            });
            var input = scope.OpenInput(files.Write(comparison + ".dll", builder.Build())).Value!;
            var translated = ValueTranslator.TranslateIntegerObjects(scope, input, root, implementation);
            Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
            var method = new DynamicMethod("LengthOracle", typeof(int), [typeof(int), typeof(int)]);
            var oracle = method.GetILGenerator();
            oracle.Emit(OpCodes.Ldarg_0); oracle.Emit(OpCodes.Ldc_I4_3); oracle.Emit(OpCodes.And); oracle.Emit(OpCodes.Newarr, typeof(int));
            oracle.Emit(OpCodes.Ldlen); oracle.Emit(OpCodes.Ldc_I4_0); oracle.Emit(comparison == ILOpCode.Ceq ? OpCodes.Ceq : OpCodes.Cgt_Un); oracle.Emit(OpCodes.Ret);
            await IntegerObjectTranslationTests.VerifyTranslated(optimized, [(comparison.ToString(), translated.Program!, method.CreateDelegate<Func<int, int, int>>() )]);
            var legacy = ValueTranslator.TranslateExceptions(scope, input, root, implementation);
            Assert.False(legacy.Success); Assert.Contains(legacy.Diagnostics, d => d.Message.Contains("Array length requires", StringComparison.Ordinal));
        }
    }
    [Theory] [InlineData("constant-entry")] [InlineData("operator-entry")] [InlineData("nop")]
    public async Task Split_zero_comparisons_remain_diagnostic(string kind)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var builder = new ResolutionAssemblyBuilder(new("SplitLength", new Version(1, 0, 0, 0)));
        var integer = builder.TypeReference("System", "Int32", builder.Reference(implementation.Identity));
        builder.Type("Test", "Root");
        var root = builder.Method("Run", [0, 2, 8, 8, 8], emit: il => {
            var constant = il.DefineLabel(); var operation = il.DefineLabel(); var array = il.DefineLabel();
            il.LoadArgument(0); il.Branch(ILOpCode.Brtrue, array); il.LoadConstantI4(1);
            if (kind == "operator-entry") { il.LoadConstantI4(0); il.Branch(ILOpCode.Br, operation); }
            else il.Branch(ILOpCode.Br, constant);
            il.MarkLabel(array); il.LoadConstantI4(1); il.OpCode(ILOpCode.Newarr); il.Token(integer); il.OpCode(ILOpCode.Ldlen);
            if (kind == "nop") il.OpCode(ILOpCode.Nop);
            il.MarkLabel(constant); il.LoadConstantI4(0); il.MarkLabel(operation); il.OpCode(ILOpCode.Cgt_un); il.OpCode(ILOpCode.Ret);
        });
        var input = scope.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, root, implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("Array length requires", StringComparison.Ordinal));
    }
}
