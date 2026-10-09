using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class BoxAllocatorCapacityTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Large_box_catalog_keeps_native_stack_bounded_and_allocation_typed(bool optimized)
    {
        var (program, original) = NativeBoxedPayloadTests.Model();
        var types = ImmutableArray.CreateBuilder<ValueTypeLayout>();
        string hash = new('a',64);
        // Within the production 256-type bound, large enough to reproduce the
        // old unoptimized allocator dispatch frame with sanitizers enabled.
        for (int i=0;i<240;++i)
        {
            var type = new ObjectTypeId(hash,0x02000100+i);
            ImmutableArray<ObjectField> fields = i%2==0 ? [] : [new(new(type,0x04001000+i),"Number",FieldAttributes.Public,new ObjectStorage.Int32())];
            types.Add(new(new("fixture",hash,type.Token,"Box"+i.ToString(CultureInfo.InvariantCulture)),TypeAttributes.Public|TypeAttributes.Sealed,fields));
        }
        var values = original with { Types=types.ToImmutable(), Roots=[.. types.Select(t=>ObjectTypeId.From(t.Source))] };
        var artifact = ObjectCppEmitter.EmitBoxedRuntime(program,values);
        string Descriptor(ValueTypeLayout type) => "&box_"+hash+"_"+type.Source.Token.ToString("x8",CultureInfo.InvariantCulture)+"_descriptor";
        var text = new StringBuilder(artifact.Text).Append(CultureInfo.InvariantCulture,$$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            static const Descriptor* descriptors[]={{{string.Join(",",types.Select(Descriptor))}}};
            [[gnu::noinline]] static void check(bool forced) {
                Instance heap;heap.collect_before_allocation=forced;Context c(heap);
                Object* roots[2]{};Frame frame;RootScope scope(c,frame,roots,2);
                Argument packet[1];packet[0].bits=197u;
                for(std::size_t i=0;i<240;++i) {
                    auto* type=descriptors[i];auto count=i%2;
                    auto first=box_value(c,type,packet,count);roots[0]=first.value;
                    auto second=box_value(c,type,packet,count);roots[1]=second.value;
                    assert(first.status==BoxStatus::complete && second.status==BoxStatus::complete);
                    assert(first.value!=second.value && first.value->type==type && second.value->type==type);
                    assert(heap.owns(first.value) && heap.owns(second.value));
                    Argument copy[1];assert(copy_box(c,first.value,type,copy,count)==BoxStatus::complete);
                    if(count!=0)assert(copy[0].bits==197u);
                    assert(collect(c).status==MarkStatus::complete && heap.count==2);
                    roots[0]=nullptr;roots[1]=nullptr;
                    assert(collect(c).status==MarkStatus::complete && heap.count==0);
                    heap.object_limit=0;
                    assert(box_value(c,type,packet,count).status==BoxStatus::allocation && heap.count==0);
                    heap.object_limit=4096;c.pending=0;
                }
                const Descriptor copied=*descriptors[0];
                for(const Descriptor* bad:{&copied,reinterpret_cast<const Descriptor*>(std::uintptr_t{1}),static_cast<const Descriptor*>(nullptr)}) {
                    assert(!known_box(bad));assert(allocate_box_storage(c,bad)==nullptr && c.pending==0 && heap.count==0);
                    assert(box_value(c,bad,nullptr,0).status==BoxStatus::invalid && c.pending==0 && heap.count==0);
                }
            }
            int main(){check(false);check(true);}
            """);
        using var files=new ResolverFiles();string dir=files.DirectoryPath("box-catalog"),cpp=Path.Combine(dir,"boxes.cpp"),exe=Path.Combine(dir,"boxes");
        await File.WriteAllTextAsync(cpp,text.ToString(),TestContext.Current.CancellationToken);NativeEvidence.Record(cpp,optimized);
        var build=await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX")??"clang++",
            ["-std=c++20","-fno-exceptions","-fno-rtti","-Wall","-Wextra","-Werror",optimized?"-O2":"-O0","-Wframe-larger-than=2048","-fsanitize=address,undefined","-fno-sanitize-recover=all",cpp,"-o",exe],dir);
        Assert.True(build.ExitCode==0,build.Stderr);NativeEvidence.Record(cpp,optimized,exe);
        var run=await NativeProcess.Run(exe,[],dir);Assert.True(run.ExitCode==0,run.Stdout+run.Stderr);Assert.Empty(run.Stderr);
    }
}
