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
    [InlineData(false,0)] [InlineData(false,1)] [InlineData(true,0)] [InlineData(true,1)]
    public async Task Fields_reference_results_dispatch_staged_aliases_and_fault_replay_match_dotnet(bool optimized,int enabled)
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
            nativeFlags:["-DCILICA_TYPED_INTERNALS="+enabled,"-DCILICA_VERIFY_INTERNAL_REFERENCES=1","-DCILICA_VERIFY_BORROW_LIFETIMES=1","-Wframe-larger-than=16384"]);
    }
    [Fact]
    public async Task Lookup_kernels_link_and_execute_across_translation_units()
    {
        var translated=IntegerObjectTranslationTests.Translate(nameof(TypedLookupProbe.Run),fixture:typeof(TypedLookupProbe));
        Assert.True(translated.Success,string.Join("; ",translated.Diagnostics));
        var module=ObjectCppEmitter.EmitExceptionModule(ValueScalarLowering.LowerExceptions(translated.Program!),1);
        using var files=new ResolverFiles();string dir=files.DirectoryPath("typed-module");
        foreach(var f in module.Sources.Prepend(module.Header))File.WriteAllText(Path.Combine(dir,f.Name),f.Text);
        string main=Path.Combine(dir,"main.cpp"),entry="cilica_m_"+module.Source.ContentHash+"_"+module.Source.Token.ToString("x8",CultureInfo.InvariantCulture);
        File.WriteAllText(main,$$"""
            #include "{{module.Header.Name}}"
            #include <cassert>
            using namespace {{module.EntryPoint[..^4]}}_closure;
            int main(){Instance heap;heap.collect_before_allocation=true;Context c(heap);LocationArgument args[2]{};
            args[0].bits=17;args[1].bits=3;assert({{entry}}(c,args)=={{TypedLookupProbe.Run(17,3)}} && c.pending==0);
            assert(c.depth==0 && c.used==0 && c.top==nullptr && c.borrows==nullptr);}
            """);
        string compiler=Environment.GetEnvironmentVariable("CILICA_CXX")??"clang++";var objects=new List<string>();
        foreach(string cpp in module.Sources.Select(f=>Path.Combine(dir,f.Name)).Append(main))
        {
            string obj=cpp+".o";var build=await NativeProcess.Run(compiler,["-std=c++20","-O2","-DCILICA_TYPED_INTERNALS=1","-fno-exceptions","-fno-rtti","-Wall","-Wextra","-Werror","-fsanitize=address,undefined","-fno-sanitize-recover=all","-c",cpp,"-o",obj],dir);
            Assert.True(build.ExitCode==0,build.Stderr);objects.Add(obj);
        }
        string exe=Path.Combine(dir,"probe");var link=await NativeProcess.Run(compiler,["-fsanitize=address,undefined",..objects,"-o",exe],dir);Assert.True(link.ExitCode==0,link.Stderr);
        var run=await NativeProcess.Run(exe,[],dir);Assert.True(run.ExitCode==0,run.Stderr);Assert.Empty(run.Stderr);
    }
    private static string Symbol(string hash,string name)=>"cilica_m_"+hash+"_"+typeof(TypedLookupProbe).GetMethod(name)!.MetadataToken.ToString("x8",CultureInfo.InvariantCulture);
}
