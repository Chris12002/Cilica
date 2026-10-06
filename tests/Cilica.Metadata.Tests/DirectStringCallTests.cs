using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class DirectStringCallTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(DirectStringProbe), "Length", "Chars"));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Roslyn_literal_instance_calls_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("LiteralLength", programs[0], DirectStringProbe.Length), ("LiteralChars", programs[1], DirectStringProbe.Chars)]);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Direct_calls_preserve_null_and_bounds_fault_precedence(bool optimized)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var b = new ResolutionAssemblyBuilder(new("DirectStringCalls", new Version(1, 0, 0, 0)));
        var core = b.Reference(implementation.Identity);
        var text = b.TypeReference("System", "String", core);
        var nullFault = b.TypeReference("System", "NullReferenceException", core);
        var rangeFault = b.TypeReference("System", "IndexOutOfRangeException", core);
        var length = b.Member(text, "get_Length", 0x20, 0, 8);
        var chars = b.Member(text, "get_Chars", 0x20, 1, 3, 8);
        var literal = b.String("A\0\ud800\udfffZ");
        b.Type("Test", "Calls");
        MethodDefinitionHandle Root(bool character) => b.Method(character ? "Chars" : "Length", [0, 2, 8, 8, 8], locals: [7, 1, 8], emit: il => {
            var start = il.DefineLabel(); var nonnull = il.DefineLabel(); var join = il.DefineLabel();
            var nullHandler = il.DefineLabel(); var rangeHandler = il.DefineLabel(); var done = il.DefineLabel();
            il.MarkLabel(start); il.LoadArgument(0); il.Branch(ILOpCode.Brtrue, nonnull);
            il.OpCode(ILOpCode.Ldnull); il.Branch(ILOpCode.Br, join);
            il.MarkLabel(nonnull); il.LoadString(literal); il.MarkLabel(join);
            if (character) il.LoadArgument(1);
            il.Call(character ? chars : length); il.StoreLocal(0); il.Branch(ILOpCode.Leave, done);
            il.MarkLabel(nullHandler); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(-10001); il.StoreLocal(0); il.Branch(ILOpCode.Leave, done);
            il.MarkLabel(rangeHandler); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(-10002); il.StoreLocal(0); il.Branch(ILOpCode.Leave, done);
            il.MarkLabel(done); il.LoadLocal(0); il.OpCode(ILOpCode.Ret);
            il.ControlFlowBuilder!.AddCatchRegion(start, nullHandler, nullHandler, rangeHandler, nullFault);
            il.ControlFlowBuilder.AddCatchRegion(start, nullHandler, rangeHandler, done, rangeFault);
        });
        var lengthRoot = Root(false); var charRoot = Root(true);
        var input = scope.OpenInput(files.Write("calls.dll", b.Build())).Value!;
        ManagedExceptionIrProgram Translate(MethodDefinitionHandle method) {
            var result = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
            Assert.True(result.Success, string.Join("; ", result.Diagnostics)); return result.Program!;
        }
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("DirectLength", Translate(lengthRoot), DirectStringProbe.RawLengthOracle), ("DirectChars", Translate(charRoot), DirectStringProbe.RawCharsOracle)]);
    }
}
