using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class NativeTypedBorrowTests
{
    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(true, 0)] [InlineData(true, 1)]
    public async Task Stable_borrows_aliases_utf16_and_faults_match_dotnet(bool optimized, int enabled)
    {
        var run = IntegerObjectTranslationTests.Translate(nameof(TypedBorrowProbe.Run), fixture: typeof(TypedBorrowProbe));
        var fault = IntegerObjectTranslationTests.Translate(nameof(TypedBorrowProbe.Fault), fixture: typeof(TypedBorrowProbe));
        Assert.True(run.Success, string.Join("; ",run.Diagnostics)); Assert.True(fault.Success,string.Join("; ",fault.Diagnostics));
        var source=ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(run.Program!));
        Assert.Contains(Symbol(source.Source.ContentHash,nameof(TypedBorrowProbe.Snapshot))+"_kernel",source.Text,StringComparison.Ordinal);
        Assert.DoesNotContain(Symbol(source.Source.ContentHash,nameof(TypedBorrowProbe.Mutate))+"_kernel",source.Text,StringComparison.Ordinal);
        Assert.DoesNotContain(Symbol(source.Source.ContentHash,nameof(TypedBorrowProbe.Change))+"_kernel",source.Text,StringComparison.Ordinal);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("BorrowRun",run.Program!,TypedBorrowProbe.Run),("BorrowFault",fault.Program!,TypedBorrowProbe.Fault)],
            nativeFlags:["-DCILICA_TYPED_INTERNALS="+enabled,"-Wframe-larger-than=16384"]);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Typed_borrows_keep_carrier_lifetime_and_permission_checks(bool optimized)
    {
        var translated=IntegerObjectTranslationTests.Translate(nameof(TypedBorrowProbe.Run),fixture:typeof(TypedBorrowProbe));
        Assert.True(translated.Success,string.Join("; ",translated.Diagnostics));
        var artifact=ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(translated.Program!));
        string pair=Symbol(artifact.Source.ContentHash,nameof(TypedBorrowProbe.Pair));
        string source=artifact.Text+$$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            static void reject(Context& c,Location location) {
                LocationArgument args[2]{}; args[0].location=location;args[1].location=location;
                (void){{pair}}(c,args);assert(c.pending==6 && c.depth==1 && c.used==0);
                c.pending=0;c.image=nullptr;c.method=0;c.il=0;
            }
            int main() {
                Instance heap;Context c(heap);Location saved;
                {
                    Frame f;RootScope roots(c,f,nullptr,0);std::uint32_t values[2]{21,37};
                    BorrowScope borrow(c,f,values,2,nullptr);
                    assert(borrow.status==LocationStatus::complete);
                    saved=frame_location(c,borrow.frame.token,LocationOrigin::frame_integer,0).value;
                    LocationArgument args[2]{};args[0].location=saved;args[1].location=saved;
                    assert({{pair}}(c,args)==42 && c.pending==0 && c.used==0 && c.depth==1);
                    values[0]=27;assert({{pair}}(c,args)==54 && c.pending==0);
                    auto bad=saved;bad.slot=2;reject(c,bad);
                    bad=saved;bad.frame+=64;reject(c,bad);
                    bad=saved;bad.read_only=true;reject(c,bad);
                    bad=saved;bad.narrow=LocationNarrow::Int8;reject(c,bad);
                    Instance foreign;Context other(foreign);Frame of;RootScope otherRoots(other,of,nullptr,0);reject(other,saved);
                }
                assert(c.depth==0 && c.top==nullptr && c.borrows==nullptr && c.used==0);
                {Frame f;RootScope roots(c,f,nullptr,0);reject(c,saved);}
            }
            """;
        using var files=new ResolverFiles();string dir=files.DirectoryPath("typed-carriers"),cpp=Path.Combine(dir,"probe.cpp"),exe=Path.Combine(dir,"probe");
        File.WriteAllText(cpp,source);
        var build=await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX")??"clang++",
            ["-std=c++20",optimized?"-O2":"-O0","-DCILICA_TYPED_INTERNALS=1","-fno-exceptions","-fno-rtti","-Wall","-Wextra","-Werror","-Wframe-larger-than=16384","-fsanitize=address,undefined","-fno-sanitize-recover=all",cpp,"-o",exe],dir);
        Assert.True(build.ExitCode==0,build.Stderr);var run=await NativeProcess.Run(exe,[],dir);Assert.True(run.ExitCode==0,run.Stderr);Assert.Empty(run.Stderr);
    }
    [Fact]
    public async Task Typed_kernels_link_and_execute_across_translation_units()
    {
        var translated=IntegerObjectTranslationTests.Translate(nameof(TypedBorrowProbe.Run),fixture:typeof(TypedBorrowProbe));
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
            args[0].bits=17;args[1].bits=3;assert({{entry}}(c,args)=={{TypedBorrowProbe.Run(17,3)}} && c.pending==0);
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
    private static string Symbol(string hash,string name)=>"cilica_m_"+hash+"_"+typeof(TypedBorrowProbe).GetMethod(name)!.MetadataToken.ToString("x8",CultureInfo.InvariantCulture);
}
