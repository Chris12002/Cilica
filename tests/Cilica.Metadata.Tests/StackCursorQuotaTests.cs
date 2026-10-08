using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class StackCursorQuotaTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Failed_stack_growth_preserves_cursor_and_success_invalidates_it(bool optimized)
    {
        var program=(await CoreLibraryBuildTests.TranslateFixtures(typeof(StackCursorQuotaProbe),["Run"]))[0];var flow=program.Flow.Flow.Flow;
        var artifact=ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string Symbol(string name){var m=flow.Methods.Single(m=>m.Source.Name=="Cilica.Fixtures.Execution.StackCursorQuotaProbe::"+name);return $"cilica_m_{m.Source.ContentHash}_{m.Source.Token:x8}";}
        string source=artifact.Text+$$"""
        #include <cassert>
        using namespace {{artifact.EntryPoint[..^4]}}_closure;
        int main(){
            Instance heap;heap.collect_before_allocation=true;Context c(heap);assert(prepare_exceptions(c)==0);
            {
                Object* held[1]={nullptr};Frame frame;RootScope roots(c,frame,held,1);
                held[0]={{Symbol("Make")}}(c,nullptr);assert(c.pending==0 && held[0]!=nullptr);
                LocationArgument args[1];args[0].reference=held[0];assert({{Symbol("Count")}}(c,args)==8);
                assert(collect(c).status==MarkStatus::complete);heap.object_limit=heap.count;
                {{Symbol("Push")}}(c,args);assert(c.pending!=0 && c.used==0 && c.borrows==nullptr);
                c.pending=0;c.exception=nullptr;heap.object_limit=4096;
                assert({{Symbol("Count")}}(c,args)==8 && c.pending==0);
                assert({{Symbol("Read")}}(c,args)==7 && c.pending==0);
                {{Symbol("Push")}}(c,args);assert(c.pending==0 && {{Symbol("Count")}}(c,args)==9);
                assert({{Symbol("Rejected")}}(c,args)==1 && c.pending==0);
                held[0]=nullptr;args[0].reference=nullptr;
            }
            assert(collect(c).status==MarkStatus::complete);
            std::size_t permanent=1;
            for(auto* p=heap.literals;p;p=p->literal_next)++permanent;
            for(auto* p=heap.exception_resources;p;p=p->literal_next)++permanent;
            for(auto* p=heap.empty_arrays;p;p=p->empty_next)++permanent;
            assert(heap.count==permanent && c.pending==0 && c.exception==nullptr && c.used==0 && c.top==nullptr && c.borrows==nullptr);
        }
        """;
        using var files=new ResolverFiles();string dir=files.DirectoryPath("stack-quota"),cpp=Path.Combine(dir,"stack.cpp"),exe=Path.Combine(dir,"stack");
        await File.WriteAllTextAsync(cpp,source,TestContext.Current.CancellationToken);NativeEvidence.Record(cpp,optimized);
        var build=await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX")??"clang++",["-std=c++20","-fno-exceptions","-fno-rtti","-Wall","-Wextra","-Werror",optimized?"-O2":"-O0","-Wframe-larger-than=2048","-fsanitize=address,undefined","-fno-sanitize-recover=all",cpp,"-o",exe],dir);
        Assert.True(build.ExitCode==0,build.Stderr);NativeEvidence.Record(cpp,optimized,exe);
        var run=await NativeProcess.Run(exe,[],dir);Assert.True(run.ExitCode==0,run.Stdout+run.Stderr);Assert.Empty(run.Stderr);
    }
}
