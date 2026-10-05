using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using Cilica.Metadata.Resolution;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class SwitchTranslationTests
{
    private static ManagedExceptionTranslationResult Authored(int count, bool wide = false, bool badTarget = false)
    {
        var builder = new ResolutionAssemblyBuilder(new("Switch" + count + wide + badTarget, new Version(1,0,0,0)));
        builder.Type("", "Probe");
        var root = builder.Method("Run", [0,2,8,8,8], emit: il => {
            il.LoadArgument(0); il.LoadArgument(1);
            if (wide) il.OpCode(ILOpCode.Conv_i8);
            il.OpCode(ILOpCode.Switch); il.CodeBuilder.WriteInt32(count);
            // The default and all duplicate cases enter the same remaining-stack return.
            for (int i=0;i<count;++i) il.CodeBuilder.WriteInt32(badTarget ? -4 : 0);
            il.OpCode(ILOpCode.Ret);
        });
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var runtime=resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var image=resolver.OpenInput(files.Write("input.dll",builder.Build())).Value!;
        return ValueTranslator.TranslateIntegerObjects(resolver,image,root,runtime);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public Task Empty_and_duplicate_tables_preserve_the_remaining_stack(bool optimized)
    {
        var empty=Authored(0); var duplicate=Authored(5);
        Assert.True(empty.Success,string.Join("; ",empty.Diagnostics));
        Assert.True(duplicate.Success,string.Join("; ",duplicate.Diagnostics));
        return IntegerObjectTranslationTests.VerifyTranslated(optimized,[("EmptyTable",empty.Program!,(a,_)=>a),("DuplicateTable",duplicate.Program!,(a,_)=>a)]);
    }
    [Fact]
    public void Expanded_switch_accepts_the_exact_block_budget()
    {
        var result=Authored(IrLimits.MaximumBlocks-1);
        Assert.True(result.Success,string.Join("; ",result.Diagnostics));
        Assert.Equal(IrLimits.MaximumBlocks,Assert.Single(result.Program!.Flow.Flow.Flow.Methods).Blocks.Length);
    }
    [Theory] [InlineData("wide")] [InlineData("operand")] [InlineData("budget")]
    public void Invalid_selectors_targets_and_expanded_block_limits_are_diagnostic(string kind)
    {
        var result=Authored(kind=="budget" ? IrLimits.MaximumBlocks : 2,wide:kind=="wide",badTarget:kind=="operand");
        Assert.False(result.Success); Assert.NotEmpty(result.Diagnostics);
        if (kind=="budget") Assert.Contains(result.Diagnostics,d=>d.Id==Cilica.Metadata.DiagnosticIds.TranslationLimit);
    }
    [Theory] [InlineData("Dense")] [InlineData("Protected")] [InlineData("Loop")]
    public void Switch_ladders_validate_with_original_region_order_and_legacy_rejection(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture:typeof(SwitchProbe));
        Assert.True(result.Success,string.Join("; ",result.Diagnostics));
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program!));
        Assert.False(IntegerObjectTranslationTests.Translate(name,enabled:false,fixture:typeof(SwitchProbe)).Success);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Switch_cases_defaults_loops_and_unwind_match_dotnet(bool optimized)
    {
        (string Name,Func<int,int,int> Oracle)[] probes = [("Dense",SwitchProbe.Dense),("Protected",SwitchProbe.Protected),("Loop",SwitchProbe.Loop)];
        var pairs = (from value in new[]{int.MinValue,-1,0,1,int.MaxValue}
            from mode in new[]{int.MinValue,-1,0,1,2,3,4,5,6,int.MaxValue} select (value,mode)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,probes.Select(p=> {
            var translated=IntegerObjectTranslationTests.Translate(p.Name,fixture:typeof(SwitchProbe));
            Assert.True(translated.Success,string.Join("; ",translated.Diagnostics)); return (p.Name,translated.Program!,p.Oracle);
        }).ToArray(),pairs);
    }
}
