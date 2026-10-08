using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class CandidateScalarArraySafetyTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram>> Program = new(async () => (await CandidateScalarArrayReceiverTests.Programs.Value)[7]);
    [Theory]
    [InlineData("width")] [InlineData("fault")] [InlineData("array")] [InlineData("slot")] [InlineData("narrow")] [InlineData("index")]
    public async Task Both_stages_validate_integer_view_width_narrowing_bounds_operands_and_fault_edges(string mutation)
    {
        var program=await Program.Value;var flow=program.Flow.Flow.Flow;
        var wrongArray=ObjectTypeId.Vector(flow.Types.References.ObjectBase,ObjectArrayElement.UInt32);
        ImmutableArray<IrMethod> Alter(ImmutableArray<IrMethod> methods)=>[..methods.Select(m=>m with {
            Blocks=[..m.Blocks.Select(b=>b with { Statements=[..b.Statements.Select(s=> {
                if(s is not IrStatement.Define d)return s;
                if(d.Value.Expression is not (IrExpression.AddressElement or IrExpression.AddressArraySlot))return s;
                if(mutation=="width")return d with { Value=d.Value with { Type=IrType.Location(IrType.Int64) } };
                if(mutation=="narrow")return d with { Value=d.Value with { Type=IrType.Location(IrType.Int32) with { Narrow=IntegerCastTarget.UInt8 } } };
                if(mutation=="fault")return d with { Exceptional=null };
                var expression=d.Value.Expression switch {
                    IrExpression.AddressElement a=>mutation=="index"?a with { Index=a.Array }:a with { ArrayType=wrongArray },
                    IrExpression.AddressArraySlot a=>mutation=="slot"?a with { Slot=1 }:mutation=="index"?a with { Index=a.Array }:a with { ArrayType=wrongArray },
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
        string rootSymbol=$"t_{types.ObjectBase.ContentHash}_{types.ObjectBase.Token:x8}", vector=rootSymbol+"_vector_i4";
        var artifact=ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string source=artifact.Text+$$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            [[gnu::noinline]] static void invalid_carriers(Context& c, Location good) {
                std::uint32_t value=0; std::uint64_t wide=0; Object* reference=nullptr;
                auto bad=good; bad.slot=1; assert(load_location_integer(c,bad,value)==LocationStatus::invalid);
                bad=good; bad.element=2; assert(load_location_integer(c,bad,value)==LocationStatus::invalid);
                bad=good; bad.frame=1; assert(load_location_integer(c,bad,value)==LocationStatus::invalid);
                bad=good; bad.narrow=LocationNarrow::UInt8; assert(load_location_integer(c,bad,value)==LocationStatus::invalid);
                bad=good; bad.declaring=&{{rootSymbol}}_descriptor; assert(load_location_integer(c,bad,value)==LocationStatus::invalid);
                bad=good; bad.declaring=&{{rootSymbol}}_vector_u4_descriptor; assert(load_location_integer(c,bad,value)==LocationStatus::invalid);
                bad=good; bad.read_only=true; assert(store_location_integer(c,bad,17)==LocationStatus::read_only);
                assert(load_location_wide_integer(c,good,wide)==LocationStatus::invalid);
                assert(load_location_reference(c,good,reference)==LocationStatus::invalid);
                assert(store_location_wide_integer(c,good,17)==LocationStatus::invalid);
                assert(store_location_reference(c,good,nullptr)==LocationStatus::invalid);
            }
            [[gnu::noinline]] static void excluded(Context& c,const Descriptor& type) {
                Object* owner=allocate_array(c,type,2);
                Frame frame;RootScope root(c,frame,&owner,1);
                assert(array_location(c,owner,&type,0,0).status==LocationStatus::invalid);
                assert(array_location(c,owner,&{{vector}}_descriptor,0,0).status==LocationStatus::invalid);
            }
            [[gnu::noinline]] static void bounds(Context& c,Object* owner) {
                assert(array_location(c,nullptr,&{{vector}}_descriptor,0xffffffffu,0).status==LocationStatus::null_reference);
                assert(array_location(c,owner,&{{vector}}_descriptor,0xffffffffu,0).status==LocationStatus::index_bounds);
                assert(array_location(c,owner,&{{vector}}_descriptor,0,1).status==LocationStatus::invalid);
                assert(array_location(c,owner,&{{rootSymbol}}_vector_u4_descriptor,0,0).status==LocationStatus::invalid);
            }
            [[gnu::noinline]] static void foreign(Location slot) {
                Instance heap;Context other(heap);std::uint32_t value=0;
                assert(load_location_integer(other,slot,value)==LocationStatus::invalid);
            }
            [[gnu::noinline]] static void carriers(const Descriptor& actual) {
                Instance heap;Context c(heap);Object* owner=allocate_array(c,actual,2);
                Frame frame;RootScope roots(c,frame,&owner,1);heap.collect_before_allocation=true;
                auto slot=array_location(c,owner,&{{vector}}_descriptor,1,0);
                assert(slot.status==LocationStatus::complete);
                assert(store_location_integer(c,slot.value,0xffffffffu)==LocationStatus::complete);
                excluded(c,{{rootSymbol}}_vector_u1_descriptor);
                excluded(c,{{rootSymbol}}_vector_char_descriptor);
                excluded(c,{{rootSymbol}}_vector_bool_descriptor);
                excluded(c,{{rootSymbol}}_vector_i8_descriptor);
                assert(collect(c).status==MarkStatus::complete&&heap.count==1);
                std::uint32_t value=0;assert(load_location_integer(c,slot.value,value)==LocationStatus::complete&&value==0xffffffffu);
                invalid_carriers(c,slot.value);bounds(c,owner);foreign(slot.value);
                c.instance.marking=true;assert(load_location_integer(c,slot.value,value)==LocationStatus::busy);c.instance.marking=false;
                owner=nullptr;assert(collect(c).status==MarkStatus::complete&&heap.count==0);
                assert(load_location_integer(c,slot.value,value)==LocationStatus::invalid);
            }
            int main() { carriers({{vector}}_descriptor);carriers({{rootSymbol}}_vector_u4_descriptor); }
            """;
        using var files=new ResolverFiles();string directory=files.DirectoryPath("integer-array-borrows"),cpp=Path.Combine(directory,"borrow.cpp"),exe=Path.Combine(directory,"borrow");
        await File.WriteAllTextAsync(cpp,source,TestContext.Current.CancellationToken);
        var build=await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX")??"clang++",
            ["-std=c++20","-fno-exceptions","-fno-rtti","-Wall","-Wextra","-Werror",optimized?"-O2":"-O0","-Wframe-larger-than=2048","-fsanitize=address,undefined","-fno-sanitize-recover=all",cpp,"-o",exe],directory);
        Assert.True(build.ExitCode==0,build.Stderr);
        var run=await NativeProcess.Run(exe,[],directory);Assert.True(run.ExitCode==0,run.Stdout+run.Stderr);Assert.Empty(run.Stderr);
    }
}
