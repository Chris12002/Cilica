using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ProgramMethodBudgetTests
{
    private static readonly Lazy<ManagedExceptionTranslationResult> Exact = new(() => Translate(512));
    private static ManagedExceptionTranslationResult Translate(int count)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("MethodBudget",new Version(1,0,0,0)));
        var obj = b.TypeReference("System","Object",b.Reference(library.Identity));
        var methods = new MethodDefinitionHandle[count - 1];
        for (int i = 0; i < methods.Length; i++)
        {
            if (i % 16 == 0) b.Type("Test","Owner" + i,baseType:obj);
            int value = i + 1;
            methods[i] = b.Method("Value" + i,[0,0,8],emit: il => { il.LoadConstantI4(value); il.OpCode(ILOpCode.Ret); });
        }
        b.Type("Test","Root",baseType:obj);
        var root = b.Method("Run",[0,2,8,8,8],emit: il => {
            il.LoadConstantI4(0);
            foreach (var method in methods) { il.Call(method); il.OpCode(ILOpCode.Add); }
            il.OpCode(ILOpCode.Ret);
        });
        var input = resolver.OpenInput(files.Write("input.dll",b.Build())).Value!;
        return ValueTranslator.TranslateIntegerObjects(resolver,input,root,library);
    }
    [Fact]
    public void Exact_and_excess_method_closures_have_distinct_outcomes()
    {
        var exact = Exact.Value;
        Assert.True(exact.Success,string.Join("; ",exact.Diagnostics));
        Assert.Equal(512,exact.Program!.Flow.Flow.Flow.Methods.Length);
        var excess = Translate(513);
        Assert.False(excess.Success);
        Assert.Contains(excess.Diagnostics,d => d.Id == DiagnosticIds.TranslationLimit
            && d.Message.Contains("512/512 method declarations",StringComparison.Ordinal));
        var flow = exact.Program.Flow.Flow.Flow;
        var method = flow.Methods[0];
        var extra = method with { Source = method.Source with { Token = 0x06001000 } };
        var invalid = exact.Program with { Flow = exact.Program.Flow with { Flow = exact.Program.Flow.Flow with {
            Flow = flow with { Methods = flow.Methods.Add(extra) } } } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Full_bounded_closure_executes_with_existing_frame_limits(bool optimized)
    {
        Assert.True(Exact.Value.Success,string.Join("; ",Exact.Value.Diagnostics));
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("FullMethodBudget", Exact.Value.Program!, (_, _) => 511 * 512 / 2)], [(0,0)]);
    }
}
