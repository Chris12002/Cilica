using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ConstructorJoinTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public Task Readonly_constructor_stores_after_null_checks_match_dotnet(bool optimized) =>
        IntegerObjectTranslationTests.Verify(optimized,[("Flow",ConstructorJoinProbe.Flow)],typeof(ConstructorJoinProbe));
    [Theory] [InlineData(false)] [InlineData(true)]
    public void Detached_constructor_bodies_cannot_reinitialize_or_reenter_the_prefix(bool reenter)
    {
        var translated=IntegerObjectTranslationTests.Translate("Flow",fixture:typeof(ConstructorJoinProbe));
        Assert.True(translated.Success,string.Join("; ",translated.Diagnostics));
        var program=ValueScalarLowering.LowerExceptions(translated.Program!);
        var boxes=program.Flow.Flow; var locations=boxes.Flow; var objects=locations.Flow;
        var method=objects.Flow.Methods.Single(m=>m.Source.Name.Contains("ConstructorJoined::.ctor",StringComparison.Ordinal));
        var block=method.Blocks.Last();
        if (reenter) block=block with { Terminator=new IrJump(block.Terminator.ILOffset,new(0,[])) };
        else
        {
            var original=method.Blocks[0].Statements.OfType<IrStatement.Invoke>().First();
            int receiver=block.Values.First(v=>v.Expression is IrExpression.Parameter { Index:0 }).Id;
            block=block with { Statements=block.Statements.Add(original with { ILOffset=block.Terminator.ILOffset, Arguments=[receiver] }) };
        }
        var changed=method with { Blocks=method.Blocks.Replace(method.Blocks.Last(),block) };
        objects=objects with { Flow=objects.Flow with { Methods=objects.Flow.Methods.Replace(method,changed) } };
        var forged=program with { Flow=program.Flow with { Flow=boxes with { Flow=locations with { Flow=objects } } } };
        Assert.Throws<ArgumentException>(()=>ScalarManagedExceptionIrValidator.Validate(forged));
    }
    [Fact]
    public void A_mutable_argument_slot_cannot_supply_replayed_receiver_provenance()
    {
        using var files=new ResolverFiles();
        using var resolver=AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var runtime=resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var builder=new ResolutionAssemblyBuilder(new("MutableConstructor",new Version(1,0,0,0)));
        var objectType=builder.TypeReference("System","Object",builder.Reference(runtime.Identity));
        var baseConstructor=builder.Member(objectType,".ctor",0x20,0,1);
        builder.Type("","Cell",baseType:objectType);
        var field=builder.FieldWithAttributes("Number",FieldAttributes.Public|FieldAttributes.InitOnly,0x06,0x08);
        var constructor=builder.Method(".ctor",[0x20,1,1,8],attributes:MethodAttributes.Public|MethodAttributes.SpecialName|MethodAttributes.RTSpecialName,
            emit:il=> {
                var join=il.DefineLabel();
                il.LoadArgument(0);il.Call(baseConstructor);
                il.LoadArgument(0);il.LoadArgument(1);il.Branch(ILOpCode.Brfalse_s,join);
                il.LoadArgumentAddress(0);il.OpCode(ILOpCode.Ldnull);il.OpCode(ILOpCode.Stind_ref);
                il.MarkLabel(join);il.LoadArgument(1);il.OpCode(ILOpCode.Stfld);il.Token(field);il.OpCode(ILOpCode.Ret);
            });
        builder.Type("","Probe");
        var root=builder.Method("Run",[0,2,8,8,8],emit:il=> {
            il.LoadArgument(0);il.OpCode(ILOpCode.Newobj);il.Token(constructor);il.OpCode(ILOpCode.Ldfld);il.Token(field);il.OpCode(ILOpCode.Ret);
        });
        var input=resolver.OpenInput(files.Write("input.dll",builder.Build())).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(resolver,input,root,runtime);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("readonly receiver",StringComparison.Ordinal));
    }
}
