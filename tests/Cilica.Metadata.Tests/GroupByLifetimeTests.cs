using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class GroupByLifetimeTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Disposed_query_and_retained_group_preserve_their_pinned_graphs(bool optimized)
    {
        var program=(await CoreLibraryBuildTests.TranslateFixtures(typeof(GroupByProbe),["Lifetime"]))[0];var flow=program.Flow.Flow.Flow;
        var artifact=ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string Symbol(string name) { var m=flow.Methods.Single(m=>m.Source.Name=="Cilica.Fixtures.Execution.GroupByProbe::"+name);return $"cilica_m_{m.Source.ContentHash}_{m.Source.Token:x8}"; }
        string source=artifact.Text+$$"""
        #include <cassert>
        using namespace {{artifact.EntryPoint[..^4]}}_closure;
        int main() {
            Instance heap; heap.collect_before_allocation=true; Context c(heap);
            assert(prepare_exceptions(c)==0);
            Object* held[2]={nullptr,nullptr}; Frame frame; RootScope roots(c,frame,held,2);
            held[0]={{Symbol("MakeTracked")}}(c,nullptr);
            assert(c.pending==0 && held[0]!=nullptr);
            assert(collect(c).status==MarkStatus::complete && heap.count==20);
            LocationArgument args[1]; args[0].reference=held[0];
            held[1]={{Symbol("ReadTracked")}}(c,args); assert(held[1]!=nullptr && c.pending==0);
            {{Symbol("StopTracked")}}(c,args);
            assert(c.pending==0 && {{Symbol("ReadTracked")}}(c,args)==nullptr);
            assert(collect(c).status==MarkStatus::complete && heap.count==20);
            {{Symbol("StopTracked")}}(c,args);
            assert(c.pending==0 && collect(c).status==MarkStatus::complete && heap.count==20);
            held[0]=nullptr; args[0].reference=nullptr;
            assert(collect(c).status==MarkStatus::complete && heap.count==13);
            held[1]=nullptr;
            assert(collect(c).status==MarkStatus::complete && heap.count==1);
            assert(c.used==0 && c.borrows==nullptr);
        }
        """;
        using var files=new ResolverFiles();string directory=files.DirectoryPath("grouping-lifetime"),cpp=Path.Combine(directory,"grouping.cpp"),exe=Path.Combine(directory,"grouping");
        await File.WriteAllTextAsync(cpp,source,TestContext.Current.CancellationToken);
        var build=await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX")??"clang++",["-std=c++20","-fno-exceptions","-fno-rtti","-Wall","-Wextra","-Werror",optimized?"-O2":"-O0","-Wframe-larger-than=2048","-fsanitize=address,undefined","-fno-sanitize-recover=all",cpp,"-o",exe],directory);
        Assert.True(build.ExitCode==0,build.Stderr);var run=await NativeProcess.Run(exe,[],directory);Assert.True(run.ExitCode==0,run.Stdout+run.Stderr);Assert.Empty(run.Stderr);
    }
}
