using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class CandidateArrayLengthBranchTests
{
    private static readonly string[] Names = ["EmptyPair", "ShortCircuit", "Cleanup", "ElementKinds"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateArrayLengthBranchProbe), Names));
    public static IEnumerable<object[]> Cases() => from name in Names from optimized in new[] { false, true } select new object[] { name, optimized };
    [Theory] [MemberData(nameof(Cases))]
    public async Task Length_truth_preserves_short_circuit_fault_order_cleanup_and_element_kinds(string name, bool optimized)
    {
        var programs = await Programs.Value;
        var oracle = typeof(CandidateArrayLengthBranchProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [(name, programs[Array.IndexOf(Names, name)], oracle)]);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Both_truth_polarities_and_branch_encodings_match_native_size_dotnet_IL(bool optimized)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var probes = new List<(string, ManagedExceptionIrProgram, Func<int, int, int>)>();
        foreach (var branch in new[] { ILOpCode.Brtrue, ILOpCode.Brtrue_s, ILOpCode.Brfalse, ILOpCode.Brfalse_s })
        {
            var builder = new ResolutionAssemblyBuilder(new("Length" + branch, new Version(1, 0, 0, 0)));
            var integer = builder.TypeReference("System", "Int32", builder.Reference(implementation.Identity));
            builder.Type("Test", "Root");
            var root = builder.Method("Run", [0, 2, 8, 8, 8], emit: il => {
                var target = il.DefineLabel();
                il.LoadArgument(0); il.LoadConstantI4(3); il.OpCode(ILOpCode.And); il.OpCode(ILOpCode.Newarr); il.Token(integer);
                il.OpCode(ILOpCode.Ldlen); il.Branch(branch, target); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
                il.MarkLabel(target); il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret);
            });
            var input = scope.OpenInput(files.Write(branch + ".dll", builder.Build())).Value!;
            var translated = ValueTranslator.TranslateIntegerObjects(scope, input, root, implementation);
            Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
            var method = new DynamicMethod("LengthOracle", typeof(int), [typeof(int), typeof(int)]);
            var oracleIl = method.GetILGenerator(); var yes = oracleIl.DefineLabel();
            oracleIl.Emit(OpCodes.Ldarg_0); oracleIl.Emit(OpCodes.Ldc_I4_3); oracleIl.Emit(OpCodes.And); oracleIl.Emit(OpCodes.Newarr, typeof(int));
            oracleIl.Emit(OpCodes.Ldlen); oracleIl.Emit(branch switch { ILOpCode.Brtrue => OpCodes.Brtrue, ILOpCode.Brtrue_s => OpCodes.Brtrue_S, ILOpCode.Brfalse => OpCodes.Brfalse, _ => OpCodes.Brfalse_S }, yes);
            oracleIl.Emit(OpCodes.Ldc_I4_0); oracleIl.Emit(OpCodes.Ret); oracleIl.MarkLabel(yes); oracleIl.Emit(OpCodes.Ldc_I4_1); oracleIl.Emit(OpCodes.Ret);
            probes.Add((branch.ToString(), translated.Program!, method.CreateDelegate<Func<int, int, int>>()));
            var legacy = ValueTranslator.TranslateExceptions(scope, input, root, implementation);
            Assert.False(legacy.Success);
            Assert.Contains(legacy.Diagnostics, d => d.Id == "CIL2001" && d.Message.Contains("Array length requires", StringComparison.Ordinal));
        }
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, probes.ToArray());
    }

    [Theory]
    [InlineData("return")] [InlineData("pop")] [InlineData("dup")]
    [InlineData("arithmetic")] [InlineData("comparison")] [InlineData("wide-conversion")]
    [InlineData("local")] [InlineData("nop")] [InlineData("join")] [InlineData("branch-entry")]
    public async Task Numeric_native_length_escapes_and_split_consumers_remain_rejected(string kind)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var builder = new ResolutionAssemblyBuilder(new("LengthEscape", new Version(1, 0, 0, 0)));
        var integer = builder.TypeReference("System", "Int32", builder.Reference(implementation.Identity));
        builder.Type("Test", "Root");
        var root = builder.Method("Run", [0, 2, 8, 8, 8], locals: [7, 1, 8], emit: il => {
            var consume = il.DefineLabel(); var yes = il.DefineLabel();
            void Array() { il.LoadConstantI4(1); il.OpCode(ILOpCode.Newarr); il.Token(integer); }
            if (kind == "branch-entry")
            {
                Array(); il.OpCode(ILOpCode.Ldlen); il.OpCode(ILOpCode.Conv_i4);
                il.LoadArgument(0); il.Branch(ILOpCode.Brtrue, consume); il.OpCode(ILOpCode.Pop);
            }
            Array(); il.OpCode(ILOpCode.Ldlen);
            switch (kind)
            {
                case "return": il.OpCode(ILOpCode.Ret); return;
                case "pop": il.OpCode(ILOpCode.Pop); il.LoadConstantI4(0); break;
                case "dup": il.OpCode(ILOpCode.Dup); il.OpCode(ILOpCode.Pop); break;
                case "arithmetic": il.LoadConstantI4(1); il.OpCode(ILOpCode.Add); break;
                case "comparison": il.LoadConstantI4(0); il.OpCode(ILOpCode.Ceq); break;
                case "wide-conversion": il.OpCode(ILOpCode.Conv_i8); il.OpCode(ILOpCode.Conv_i4); break;
                case "local": il.StoreLocal(0); il.LoadLocal(0); break;
                case "nop": il.OpCode(ILOpCode.Nop); break;
                case "join": il.Branch(ILOpCode.Br, consume); break;
            }
            il.MarkLabel(consume); il.Branch(ILOpCode.Brtrue, yes); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
            il.MarkLabel(yes); il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret);
        });
        var input = scope.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, root, implementation);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Id == "CIL2001" && d.Message.Contains("Array length requires", StringComparison.Ordinal));
    }
}
