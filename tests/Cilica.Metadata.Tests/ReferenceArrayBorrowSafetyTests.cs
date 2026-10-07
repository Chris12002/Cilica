using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ReferenceArrayBorrowSafetyTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram>> Program=new(async()=>
        (await CoreLibraryBuildTests.TranslateFixtures(typeof(ReferenceArrayBorrowProbe),"Cache"))[0]);
    [Theory]
    [InlineData("width")] [InlineData("fault")] [InlineData("array")] [InlineData("slot")]
    public async Task Both_stages_validate_reference_slot_identity_and_fault_edges(string mutation)
    {
        var program=await Program.Value;var flow=program.Flow.Flow.Flow;
        var wrongArray=ObjectTypeId.Vector(flow.Types.References.ObjectBase,ObjectArrayElement.Reference(flow.Types.References.ObjectBase));
        ImmutableArray<IrMethod> Alter(ImmutableArray<IrMethod> methods)=>[..methods.Select(m=>m with {
            Blocks=[..m.Blocks.Select(b=>b with { Statements=[..b.Statements.Select(s=> {
                if(s is not IrStatement.Define d)return s;
                if(d.Value.Expression is not (IrExpression.AddressElement or IrExpression.AddressArraySlot))return s;
                if(mutation=="width")return d with { Value=d.Value with { Type=IrType.Location(IrType.Int32) } };
                if(mutation=="fault")return d with { Exceptional=null };
                var expression=d.Value.Expression switch {
                    IrExpression.AddressElement a=>a with { ArrayType=wrongArray },
                    IrExpression.AddressArraySlot a=>mutation=="slot"?a with { Slot=1 }:a with { ArrayType=wrongArray },
                    _=>d.Value.Expression };
                return d with { Value=d.Value with { Expression=expression } };
            })] })] })];
        if(mutation!="slot")Assert.Throws<ArgumentException>(()=>ManagedExceptionIrValidator.Validate(program with { Flow=program.Flow with {
            Flow=program.Flow.Flow with { Flow=flow with { Methods=Alter(flow.Methods) } } } }));
        var scalar=ValueScalarLowering.LowerExceptions(program);var boxes=scalar.Flow.Flow;var objects=boxes.Flow.Flow;
        Assert.Throws<ArgumentException>(()=>ScalarManagedExceptionIrValidator.Validate(scalar with { Flow=scalar.Flow with {
            Flow=boxes with { Flow=boxes.Flow with { Flow=objects with { Flow=objects.Flow with { Methods=Alter(objects.Flow.Methods) } } } } } }));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Native_carriers_check_owners_slots_readonly_payloads_and_stale_arrays(bool optimized)
    {
        var program=await Program.Value;var types=program.Flow.Flow.Flow.Types.References;
        var cell=ObjectTypeId.From(types.Types.Single(t=>t.Source.Name=="Cilica.Fixtures.Execution.RefLocalCell").Source);
        string Symbol(ObjectTypeId t)=>$"t_{t.ContentHash}_{t.Token:x8}";
        string cellSymbol=Symbol(cell),rootSymbol=Symbol(types.ObjectBase),vector=rootSymbol+"_vector_ref_"+cellSymbol;
        var artifact=ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string source=artifact.Text+$$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            [[gnu::noinline]] static void invalid_carriers(Context& c, Location good, Object* wrong) {
                Object* value=nullptr;
                auto bad=good; bad.slot=1; assert(load_location_reference(c,bad,value)==LocationStatus::invalid);
                bad=good; bad.element=2; assert(load_location_reference(c,bad,value)==LocationStatus::invalid);
                bad=good; bad.frame=1; assert(load_location_reference(c,bad,value)==LocationStatus::invalid);
                bad=good; bad.declaring=&{{rootSymbol}}_descriptor; assert(load_location_reference(c,bad,value)==LocationStatus::invalid);
                bad=good; bad.read_only=true; assert(store_location_reference(c,bad,nullptr)==LocationStatus::read_only);
                assert(store_location_reference(c,good,wrong)==LocationStatus::invalid);
            }
            [[gnu::noinline]] static void bounds(Context& c,Object* owner) {
                assert(array_location(c,nullptr,&{{vector}}_descriptor,0xffffffffu,0).status==LocationStatus::null_reference);
                assert(array_location(c,owner,&{{vector}}_descriptor,0xffffffffu,0).status==LocationStatus::index_bounds);
                assert(array_location(c,owner,&{{vector}}_descriptor,0,1).status==LocationStatus::invalid);
            }
            [[gnu::noinline]] static void foreign(Location slot) {
                Instance heap;Context other(heap);Object* value=nullptr;
                assert(load_location_reference(other,slot,value)==LocationStatus::invalid);
            }
            int main() {
                Instance heap;Context c(heap);Object* owner=allocate_array(c,{{vector}}_descriptor,2);
                Frame frame;RootScope roots(c,frame,&owner,1);heap.collect_before_allocation=true;
                auto slot=array_location(c,owner,&{{vector}}_descriptor,1,0);
                assert(slot.status==LocationStatus::complete);
                Object* cell=allocate<{{cellSymbol}}>(c,{{cellSymbol}}_descriptor);
                assert(store_location_reference(c,slot.value,cell)==LocationStatus::complete);
                assert(collect(c).status==MarkStatus::complete&&heap.count==2);
                Object* value=nullptr;assert(load_location_reference(c,slot.value,value)==LocationStatus::complete&&value==cell);
                Object* wrong=allocate<{{rootSymbol}}>(c,{{rootSymbol}}_descriptor);
                invalid_carriers(c,slot.value,wrong);bounds(c,owner);foreign(slot.value);
                assert(store_location_reference(c,slot.value,nullptr)==LocationStatus::complete);
                assert(collect(c).status==MarkStatus::complete&&heap.count==1);
                owner=nullptr;assert(collect(c).status==MarkStatus::complete&&heap.count==0);
                assert(load_location_reference(c,slot.value,value)==LocationStatus::invalid);
            }
            """;
        using var files=new ResolverFiles();string directory=files.DirectoryPath("reference-array-borrows"),cpp=Path.Combine(directory,"borrow.cpp"),exe=Path.Combine(directory,"borrow");
        await File.WriteAllTextAsync(cpp,source,TestContext.Current.CancellationToken);
        var build=await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX")??"clang++",
            ["-std=c++20","-fno-exceptions","-fno-rtti","-Wall","-Wextra","-Werror",optimized?"-O2":"-O0","-Wframe-larger-than=2048","-fsanitize=address,undefined","-fno-sanitize-recover=all",cpp,"-o",exe],directory);
        Assert.True(build.ExitCode==0,build.Stderr);
        var run=await NativeProcess.Run(exe,[],directory);Assert.True(run.ExitCode==0,run.Stdout+run.Stderr);Assert.Empty(run.Stderr);
    }
}
