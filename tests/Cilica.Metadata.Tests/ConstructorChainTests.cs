using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ConstructorChainTests
{
    private static readonly string[] Names=["Trace","Recovery","Values","References","Branches","ClosedOwners","Retained"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(ConstructorChainProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)]
    public async Task Chains_arguments_initializers_faults_and_values_match_dotnet(bool optimized,int group)
    {
        var programs=await Programs.Value;
        var pairs=(from a in new[]{int.MinValue,-3,-2,-1,0,1,int.MaxValue} from b in new[]{int.MinValue,-3,-2,-1,0,1,int.MaxValue} select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,Names.Take(6).Select((name,i)=>(name,programs[i],typeof(ConstructorChainProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(2).ToArray(),pairs);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Original_independent_candidate_constructor_reduction_now_executes(bool optimized)
    {
        var programs=await CoreLibraryBuildTests.TranslateFixtures(typeof(ArraySegmentProbe),["ChainedConstructorGap"]);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("Original",programs[0],ArraySegmentProbe.ChainedConstructorGap)]);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Invalid_chains_fail_both_value_and_lowered_validation(bool lowered)
    {
        var original=(await Programs.Value)[4];
        ManagedExceptionIrValidator.Validate(original);var scalar=ValueScalarLowering.LowerExceptions(original);ScalarManagedExceptionIrValidator.Validate(scalar);
        var objects=scalar.Flow.Flow.Flow.Flow;
        var flow=original.Flow.Flow.Flow;
        var bindings=lowered?objects.Bindings:original.Flow.Bindings;
        var methods=lowered?objects.Flow.Methods:flow.Methods;
        var outer=methods.Single(m=>m.Source.Name.EndsWith("ChainBranch::.ctor",StringComparison.Ordinal)&&m.Parameters.Length==3);
        var block=outer.Blocks[0];var call=block.Statements.OfType<IrStatement.Invoke>().Single();
        var target=bindings.Single(b=>b.Method==call.Target);Assert.Equal(bindings.Single(b=>b.Method==IrMethodId.From(outer.Source)).Owner,target.Owner);
        foreach(string damage in new[]{"missing","repeat","nullcheck","virtual","receiver-alias","self-cycle","two-cycle","body-call","no-integer-profile"})
        {
            var changed=outer;var changedBindings=bindings;var changedMethods=methods;
            var statements=block.Statements;
            if(damage=="missing")statements=statements.Remove(call);
            if(damage=="repeat")statements=statements.Insert(statements.IndexOf(call)+1,call);
            if(damage=="nullcheck")statements=statements.Replace(call,call with{NullCheckReceiver=true});
            if(damage=="virtual")statements=statements.Replace(call,call with{NullCheckReceiver=true,VirtualDispatch=true});
            if(damage=="receiver-alias")
            {
                var receiver=statements.OfType<IrStatement.Define>().Single(s=>s.Value.Id==call.Arguments[0]);
                changed=changed with{Locals=changed.Locals.Add(receiver.Value.Type)};
                statements=statements.Replace(receiver,receiver with{Value=receiver.Value with{Expression=new IrExpression.Local(changed.Locals.Length-1)}});
            }
            if(damage=="self-cycle")statements=statements.Replace(call,call with{Target=IrMethodId.From(outer.Source),Arguments=[call.Arguments[0],call.Arguments[1],call.Arguments[1]]});
            if(damage=="two-cycle")
            {
                var clone=outer with{Source=outer.Source with{Token=0x0600fff0}};
                var cloneId=IrMethodId.From(clone.Source);
                clone=clone with{Blocks=clone.Blocks.SetItem(0,block with{Statements=statements.Replace(call,call with{Target=IrMethodId.From(outer.Source),Arguments=[call.Arguments[0],call.Arguments[1],call.Arguments[1]]})})};
                changedMethods=changedMethods.Add(clone);changedBindings=changedBindings.Add(bindings.Single(b=>b.Method==IrMethodId.From(outer.Source)) with{Method=cloneId});
                statements=statements.Replace(call,call with{Target=cloneId,Arguments=[call.Arguments[0],call.Arguments[1],call.Arguments[1]]});
            }
            if(damage is "self-cycle" or "two-cycle")
            {
                // Keep the displaced target reachable so the cycle check, rather
                // than the independent dead-method guard, decides this packet.
                statements=statements.Add(new IrStatement.Define(new IrValue(block.Values.Length,IrType.Reference(target.Owner),block.Terminator.ILOffset,
                    new IrExpression.NewObject(call.Target,[..call.Arguments.Skip(1)])),IrExceptionTarget.MethodExit));
            }
            if(damage=="body-call")
            {
                var body=outer.Blocks.Last();Assert.NotEqual(0,body.Id);
                int receiver=body.Values.First(v=>v.Expression is IrExpression.Parameter{Index:0}).Id;
                int number=body.Values.First(v=>v.Type==IrType.Int32).Id;
                changed=changed with{Blocks=changed.Blocks.Replace(body,body with{Statements=body.Statements.Add(call with{ILOffset=body.Terminator.ILOffset,Arguments=[receiver,number]})})};
            }
            changed=changed with{Blocks=changed.Blocks.SetItem(0,block with{Statements=statements})};changedMethods=changedMethods.Replace(outer,changed);
            ArgumentException error;
            if(lowered)
            {
                var edited=objects with{Bindings=changedBindings,Flow=objects.Flow with{Methods=changedMethods,Types=damage=="no-integer-profile"?objects.Flow.Types with{Integer64=false}:objects.Flow.Types}};
                var forged=scalar with{Flow=scalar.Flow with{Flow=scalar.Flow.Flow with{Flow=scalar.Flow.Flow.Flow with{Flow=edited}}}};
                error=Assert.Throws<ArgumentException>(()=>ScalarManagedExceptionIrValidator.Validate(forged));
            }
            else
            {
                var edited=flow with{Methods=changedMethods,Types=damage=="no-integer-profile"?flow.Types with{References=flow.Types.References with{Integer64=false}}:flow.Types};
                var forged=original with{Flow=original.Flow with{Bindings=changedBindings,Flow=original.Flow.Flow with{Flow=edited}}};
                error=Assert.Throws<ArgumentException>(()=>ManagedExceptionIrValidator.Validate(forged));
            }
            if(damage is "self-cycle" or "two-cycle")Assert.Contains("acyclic",error.Message,StringComparison.Ordinal);
        }
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Allocation_failures_unwind_chains_and_recovery_keeps_only_owned_payloads(bool optimized)
    {
        var program=(await Programs.Value)[6];var flow=program.Flow.Flow.Flow;
        var method=flow.Methods.Single(m=>m.Source.Name=="Cilica.Fixtures.Execution.ConstructorChainProbe::Create");
        string symbol=$"cilica_m_{method.Source.ContentHash}_{method.Source.Token:x8}";
        var artifact=ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string source=artifact.Text+$$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                for (std::size_t limit=1; limit<=6; ++limit) {
                    Instance heap; heap.collect_before_allocation=true; heap.object_limit=limit; Context c(heap);
                    assert(prepare_exceptions(c)==0);
                    Object* root={{symbol}}(c,nullptr);
                    assert(c.used==0 && c.top==nullptr && c.borrows==nullptr && c.depth==0);
                    if (limit<5) { assert(root==nullptr && c.pending!=0); }
                    else { assert(root!=nullptr && c.pending==0); }
                    root=nullptr; c.pending=0; c.exception=nullptr; c.image=nullptr; c.method=0; c.il=0;
                    assert(collect(c).status==MarkStatus::complete && heap.count==1);
                    heap.object_limit=4096;
                    root={{symbol}}(c,nullptr);
                    assert(root!=nullptr && c.pending==0 && c.used==0 && c.top==nullptr && c.depth==0);
                    { Frame frame; RootScope roots(c,frame,&root,1);
                      assert(collect(c).status==MarkStatus::complete && heap.count==4);
                      root=nullptr;
                      assert(collect(c).status==MarkStatus::complete && heap.count==1); }
                    assert(c.top==nullptr && c.borrows==nullptr);
                }
            }
            """;
        using var files=new ResolverFiles();string dir=files.DirectoryPath("chain-gc"),cpp=Path.Combine(dir,"chain.cpp"),exe=Path.Combine(dir,"chain");
        await File.WriteAllTextAsync(cpp,source,TestContext.Current.CancellationToken);NativeEvidence.Record(cpp,optimized);
        var build=await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX")??"clang++",["-std=c++20","-fno-exceptions","-fno-rtti","-Wall","-Wextra","-Werror",optimized?"-O2":"-O0","-Wframe-larger-than=2048","-fsanitize=address,undefined","-fno-sanitize-recover=all",cpp,"-o",exe],dir);
        Assert.True(build.ExitCode==0,build.Stderr);NativeEvidence.Record(cpp,optimized,exe);
        var run=await NativeProcess.Run(exe,[],dir);Assert.True(run.ExitCode==0,run.Stdout+run.Stderr);Assert.Empty(run.Stderr);
    }

}
