using System;
using System.IO;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ThrowStackTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Throw_discards_unrelated_scalar_and_reference_stack_values(bool optimized)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var builder = new ResolutionAssemblyBuilder(new("ResidualThrow", new Version(1, 0, 0, 0)));
        var reference = builder.Reference(library.Identity);
        var obj = builder.TypeReference("System", "Object", reference);
        var exception = builder.TypeReference("System", "Exception", reference);
        var constructor = builder.Member(exception, ".ctor", 0x20, 0, 1);
        var discarded = builder.String("discarded stack root");
        builder.Type("Test", "Probe", baseType: obj);
        var root = builder.Method("Run", [0, 2, 8, 8, 8], locals: [7, 1, 8], emit: il => {
            var start = il.DefineLabel(); var handler = il.DefineLabel(); var done = il.DefineLabel();
            il.MarkLabel(start);
            il.LoadArgument(0); il.LoadString(discarded);
            il.OpCode(ILOpCode.Newobj); il.Token(constructor); il.OpCode(ILOpCode.Throw);
            il.MarkLabel(handler); il.OpCode(ILOpCode.Pop);
            il.LoadArgument(0); il.LoadArgument(1); il.OpCode(ILOpCode.Add); il.StoreLocal(0);
            il.Branch(ILOpCode.Leave, done);
            il.MarkLabel(done); il.LoadLocal(0); il.OpCode(ILOpCode.Ret);
            il.ControlFlowBuilder!.AddCatchRegion(start, handler, handler, done, exception);
        });
        var input = resolver.OpenInput(files.Write("throw.dll", builder.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.True(ValueTranslator.TranslateExceptions(resolver, input, root, library).Success);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [
            ("ResidualThrow", result.Program!, (int left, int right) => unchecked(left + right))]);
    }
}
