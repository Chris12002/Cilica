using System;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class NativeTypedLookupTests
{
    [Theory]
    [InlineData(false,0,0)] [InlineData(false,1,0)] [InlineData(true,0,0)] [InlineData(true,1,0)]
    [InlineData(false,1,1)] [InlineData(true,1,1)]
    public async Task Fields_reference_results_dispatch_staged_aliases_and_fault_replay_match_dotnet(bool optimized,int enabled,int verify)
    {
        var run=IntegerObjectTranslationTests.Translate(nameof(TypedLookupProbe.Run),fixture:typeof(TypedLookupProbe));
        var fault=IntegerObjectTranslationTests.Translate(nameof(TypedLookupProbe.Fault),fixture:typeof(TypedLookupProbe));
        Assert.True(run.Success,string.Join("; ",run.Diagnostics));Assert.True(fault.Success,string.Join("; ",fault.Diagnostics));
        var artifact=ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(run.Program!));
        foreach(string method in new[]{nameof(TypedLookupProbe.Find),nameof(TypedLookupProbe.TryFind),nameof(TypedLookupProbe.Apply),nameof(TypedLookupProbe.WriteAlias),nameof(TypedLookupProbe.HeapAlias)})
            Assert.True(artifact.Text.Contains(Symbol(artifact.Source.ContentHash,method)+"_kernel",StringComparison.Ordinal), method);
        string effect="cilica_m_"+artifact.Source.ContentHash+"_"+typeof(EffectfulTypedLookup).GetMethod("Read")!.MetadataToken.ToString("x8",CultureInfo.InvariantCulture);
        Assert.DoesNotContain(effect+"_kernel",artifact.Text,StringComparison.Ordinal);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("LookupRun",run.Program!,TypedLookupProbe.Run),("LookupFault",fault.Program!,TypedLookupProbe.Fault)],
            nativeFlags:["-DCILICA_TYPED_INTERNALS="+enabled,"-DCILICA_VERIFY_INTERNAL_REFERENCES="+verify,"-DCILICA_VERIFY_BORROW_LIFETIMES=1","-Wframe-larger-than=16384"]);
    }
    [Theory]
    [InlineData(false,0)] [InlineData(false,1)] [InlineData(true,0)] [InlineData(true,1)]
    public async Task Kernel_entry_keeps_type_and_array_storage_checks_while_diagnostics_check_internal_reads(bool optimized,int verify)
    {
        var translated=IntegerObjectTranslationTests.Translate(nameof(TypedLookupProbe.Run),fixture:typeof(TypedLookupProbe));
        Assert.True(translated.Success,string.Join("; ",translated.Diagnostics));
        var program=translated.Program!;
        var artifact=ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string hash=artifact.Source.ContentHash;
        string cell="t_"+hash+"_"+typeof(TypedLookupCell).MetadataToken.ToString("x8",CultureInfo.InvariantCulture);
        string other="t_"+hash+"_"+typeof(PureTypedLookup).MetadataToken.ToString("x8",CultureInfo.InvariantCulture);
        var array=program.Flow.Flow.Flow.Types.References.Arrays.Single(a=>a.ArrayElement!.Type?.Token==typeof(TypedLookupCell).MetadataToken);
        string descriptor=$"t_{array.ContentHash}_{array.Token:x8}_vector_ref_{cell}";
        string find=Symbol(hash,nameof(TypedLookupProbe.Find)),read=Symbol(hash,nameof(TypedLookupProbe.ArrayRead));
        string source=artifact.Text+$$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation=true; Context c(heap);
                Object* slots[3]{}; Frame frame; RootScope roots(c,frame,slots,3);
                slots[0]=allocate<{{cell}}>(c,{{cell}}_descriptor);
                slots[1]=allocate<{{other}}>(c,{{other}}_descriptor);
                slots[2]=allocate_array(c,{{descriptor}}_descriptor,1);
                assert(slots[0] && slots[1] && slots[2] && c.pending==0);
                auto* array=reinterpret_cast<ArrayObject*>(slots[2]);
                array->references[0]=slots[0];
                // Valid warm calls use native kernels without arena space.
                {
                    Context warm(heap,0); Frame f; RootScope scope(warm,f,slots,3);
                    assert({{find}}_typed(warm,slots[0],0)==slots[0] && warm.pending==0);
                    assert({{read}}_typed(warm,slots[2],0)==1 && warm.pending==0 && warm.used==0);
                }
                // Wrong known descriptors must be rejected before field/element reads.
                (void){{find}}_typed(c,slots[1],0); assert(c.pending==6); c.pending=0;
                (void){{read}}_typed(c,slots[0],0); assert(c.pending==6); c.pending=0;
                // Even normal execution checks array storage at a typed entry boundary.
                auto* saved=array->references; array->references=nullptr;
                (void){{read}}_typed(c,slots[2],0); assert(c.pending==6); c.pending=0;
                if (CILICA_VERIFY_INTERNAL_REFERENCES) {
                    std::uint32_t result=0;
                    assert(!{{read}}_kernel(c,slots[2],0,result) && c.pending==6); c.pending=0;
                }
                array->references=saved;
                // Private storage corruption is diagnostic; do not execute the
                // trusted kernel with corrupted elements in the normal mode.
                if (CILICA_VERIFY_INTERNAL_REFERENCES) {
                    array->references[0]=slots[1]; std::uint32_t result=0;
                    assert(!{{read}}_kernel(c,slots[2],0,result) && c.pending==0);
                    array->references[0]=reinterpret_cast<Object*>(std::uintptr_t{1});
                    assert(!{{read}}_kernel(c,slots[2],0,result) && c.pending==0);
                    array->references[0]=slots[0];
                }
                assert(collect(c).status==MarkStatus::complete && heap.count==3);
                assert(c.depth==1 && c.used==0 && c.borrows==nullptr);
            }
            """;
        using var files=new ResolverFiles();string dir=files.DirectoryPath("typed-reference-proof"),cpp=Path.Combine(dir,"probe.cpp"),exe=Path.Combine(dir,"probe");
        File.WriteAllText(cpp,source);
        var build=await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX")??"clang++",
            ["-std=c++20",optimized?"-O2":"-O0","-DCILICA_TYPED_INTERNALS=1","-DCILICA_VERIFY_INTERNAL_REFERENCES="+verify,
             "-fno-exceptions","-fno-rtti","-Wall","-Wextra","-Werror","-fsanitize=address,undefined","-fno-sanitize-recover=all",cpp,"-o",exe],dir);
        Assert.True(build.ExitCode==0,build.Stderr);
        var run=await NativeProcess.Run(exe,[],dir);Assert.True(run.ExitCode==0,run.Stdout+run.Stderr);Assert.Empty(run.Stderr);
    }
    [Theory]
    [InlineData(false,1,0,false)] [InlineData(true,1,0,false)] [InlineData(true,1,1,false)]
    [InlineData(true,0,0,false)] [InlineData(true,1,0,true)] [InlineData(true,1,1,true)]
    public async Task Lookup_kernels_link_and_execute_across_translation_units(bool optimized,int enabled,int verify,bool fault)
    {
        string method = fault ? nameof(TypedLookupProbe.Fault) : nameof(TypedLookupProbe.Run);
        var translated=IntegerObjectTranslationTests.Translate(method,fixture:typeof(TypedLookupProbe));
        Assert.True(translated.Success,string.Join("; ",translated.Diagnostics));
        var module=ObjectCppEmitter.EmitExceptionModule(ValueScalarLowering.LowerExceptions(translated.Program!),1);
        Assert.Contains("// Proven typed kernel:",module.Header.Text,StringComparison.Ordinal);
        foreach(var file in module.Sources) Assert.DoesNotContain("// Proven typed kernel:",file.Text,StringComparison.Ordinal);
        using var files=new ResolverFiles();string dir=files.DirectoryPath("typed-module");
        foreach(var f in module.Sources.Prepend(module.Header))File.WriteAllText(Path.Combine(dir,f.Name),f.Text);
        string main=Path.Combine(dir,"main.cpp"),peer=Path.Combine(dir,"peer.cpp");
        string entry="cilica_m_"+module.Source.ContentHash+"_"+module.Source.Token.ToString("x8",CultureInfo.InvariantCulture);
        string kernel=Symbol(module.Source.ContentHash,fault ? nameof(TypedLookupProbe.WriteThenFault) : nameof(TypedLookupProbe.Find))+"_kernel";
        var text=new System.Text.StringBuilder($$"""
            #include "{{module.Header.Name}}"
            #include <cassert>
            using namespace {{module.EntryPoint[..^4]}}_closure;
            extern decltype(&{{kernel}}) peer_kernel();
            [[gnu::noinline]] static void check(int a,int b,int expected) {
                Instance heap;heap.collect_before_allocation=true;Context c(heap);LocationArgument args[2]{};
                args[0].bits=std::bit_cast<std::uint32_t>(a);args[1].bits=std::bit_cast<std::uint32_t>(b);
                assert({{entry}}(c,args)==expected && c.pending==0 && c.exception==nullptr);
                assert(c.depth==0 && c.used==0 && c.top==nullptr && c.borrows==nullptr);
            }
            int main(){assert(peer_kernel()==&{{kernel}});
            """);
        foreach(int a in new[]{0,1,-1,127,32768,int.MinValue,int.MaxValue})
            foreach(int b in new[]{0,1,2,3,-1,int.MinValue,int.MaxValue})
                text.Append(CultureInfo.InvariantCulture,$"check({a},{b},{(fault ? TypedLookupProbe.Fault(a,b) : TypedLookupProbe.Run(a,b))});\n");
        text.Append("}\n");File.WriteAllText(main,text.ToString());
        File.WriteAllText(peer,$$"""
            #include "{{module.Header.Name}}"
            using namespace {{module.EntryPoint[..^4]}}_closure;
            decltype(&{{kernel}}) peer_kernel(){return &{{kernel}};}
            """);
        string compiler=Environment.GetEnvironmentVariable("CILICA_CXX")??"clang++";var objects=new List<string>();
        foreach(string cpp in module.Sources.Select(f=>Path.Combine(dir,f.Name)).Append(main).Append(peer))
        {
            string obj=cpp+".o";var build=await NativeProcess.Run(compiler,["-std=c++20",optimized?"-O2":"-O0",
                "-DCILICA_TYPED_INTERNALS="+enabled,"-DCILICA_VERIFY_INTERNAL_REFERENCES="+verify,"-DCILICA_VERIFY_BORROW_LIFETIMES="+verify,
                "-fno-exceptions","-fno-rtti","-Wall","-Wextra","-Werror","-Wframe-larger-than=16384",
                "-fsanitize=address,undefined","-fno-sanitize-recover=all","-c",cpp,"-o",obj],dir);
            Assert.True(build.ExitCode==0,build.Stderr);objects.Add(obj);
        }
        string exe=Path.Combine(dir,"probe");var link=await NativeProcess.Run(compiler,["-fsanitize=address,undefined",..objects,"-o",exe],dir);Assert.True(link.ExitCode==0,link.Stderr);
        var run=await NativeProcess.Run(exe,[],dir);Assert.True(run.ExitCode==0,run.Stderr);Assert.Empty(run.Stderr);
    }
    private static string Symbol(string hash,string name)=>"cilica_m_"+hash+"_"+typeof(TypedLookupProbe).GetMethod(name)!.MetadataToken.ToString("x8",CultureInfo.InvariantCulture);
}
