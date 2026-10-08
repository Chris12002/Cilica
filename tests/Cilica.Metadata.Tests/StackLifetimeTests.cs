using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class StackLifetimeTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Popped_references_are_released_and_failed_growth_preserves_stack(bool optimized)
    {
        var program=(await CoreLibraryBuildTests.TranslateFixtures(typeof(StackProbe),["Lifetime"]))[0];var flow=program.Flow.Flow.Flow;
        var artifact=ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string Symbol(string name) { var m=flow.Methods.Single(m=>m.Source.Name=="Cilica.Fixtures.Execution.StackProbe::"+name);return $"cilica_m_{m.Source.ContentHash}_{m.Source.Token:x8}"; }
        string source=artifact.Text+$$"""
        #include <cassert>
        using namespace {{artifact.EntryPoint[..^4]}}_closure;
        int main() {
            Instance heap; heap.collect_before_allocation=true; Context c(heap);
            assert(prepare_exceptions(c)==0);
            Object* held[2]={nullptr,nullptr}; Frame frame; RootScope roots(c,frame,held,2);
            held[0]={{Symbol("MakeTracked")}}(c,nullptr);
            assert(c.pending==0 && held[0]!=nullptr);
            assert(collect(c).status==MarkStatus::complete && heap.count==10);
            LocationArgument args[1]; args[0].reference=held[0];
            held[1]={{Symbol("PopTracked")}}(c,args); assert(held[1]!=nullptr && c.pending==0);
            assert(collect(c).status==MarkStatus::complete && heap.count==10);
            held[1]=nullptr;
            assert(collect(c).status==MarkStatus::complete && heap.count==9);
            for(int i=0;i<5;i++) {{Symbol("PopTracked")}}(c,args);
            assert(c.pending==0 && collect(c).status==MarkStatus::complete && heap.count==4);
            held[0]=nullptr; args[0].reference=nullptr;
            assert(collect(c).status==MarkStatus::complete && heap.count==2);

            held[0]={{Symbol("MakeTracked")}}(c,nullptr);args[0].reference=held[0];
            {{Symbol("PushNullTracked")}}(c,args);{{Symbol("PushNullTracked")}}(c,args);
            assert(c.pending==0 && {{Symbol("CountTracked")}}(c,args)==8);
            assert(collect(c).status==MarkStatus::complete && heap.count==10);
            heap.object_limit=heap.count;
            {{Symbol("PushNullTracked")}}(c,args);assert(c.pending!=0);
            c.pending=0;c.exception=nullptr;heap.object_limit=4096;
            assert({{Symbol("CountTracked")}}(c,args)==8);
            {{Symbol("PushNullTracked")}}(c,args);
            assert(c.pending==0 && {{Symbol("CountTracked")}}(c,args)==9);
            for(int i=0;i<9;i++) {{Symbol("PopTracked")}}(c,args);
            assert(c.pending==0 && collect(c).status==MarkStatus::complete && heap.count==4);
            held[0]=nullptr;args[0].reference=nullptr;
            assert(collect(c).status==MarkStatus::complete && heap.count==2);
            assert(c.used==0 && c.borrows==nullptr);
        }
        """;
        using var files=new ResolverFiles();string directory=files.DirectoryPath("stack-lifetime"),cpp=Path.Combine(directory,"stack.cpp"),exe=Path.Combine(directory,"stack");
        await File.WriteAllTextAsync(cpp,source,TestContext.Current.CancellationToken);
        var build=await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX")??"clang++",["-std=c++20","-fno-exceptions","-fno-rtti","-Wall","-Wextra","-Werror",optimized?"-O2":"-O0","-Wframe-larger-than=2048","-fsanitize=address,undefined","-fno-sanitize-recover=all",cpp,"-o",exe],directory);
        Assert.True(build.ExitCode==0,build.Stderr);var run=await NativeProcess.Run(exe,[],directory);Assert.True(run.ExitCode==0,run.Stdout+run.Stderr);Assert.Empty(run.Stderr);
    }
}
