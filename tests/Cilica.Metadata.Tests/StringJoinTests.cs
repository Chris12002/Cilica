using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class StringJoinTests
{
    private static readonly string[] Names=["Array","List","Cursor","Identity","Projection","Growth","Null","ExactArray","ArrayIdentity","ArrayOrder","ArrayGrowth"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(StringJoinProbe),Names));
    [Fact]
    public async Task Join_validates_at_both_stages()
    {foreach(var p in await Programs.Value){ManagedExceptionIrValidator.Validate(p);ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(p));}}
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Strings_storage_and_cursor_order_match_dotnet(bool optimized)
    {
        var programs=await Programs.Value;
        var pairs=(from a in Enumerable.Range(0,32) from b in new[]{0,1,2,3} select(a,b)).ToArray();
        for(int i=0;i<Names.Length;i++)
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(Names[i],programs[i],typeof(StringJoinProbe).GetMethod(Names[i])!.CreateDelegate<Func<int,int,int>>())],pairs,expectedStaticRoots:0);
    }
    private static readonly Lazy<Task<ManagedExceptionIrProgram>> QuotaProgram=new(async()=>(await CoreLibraryBuildTests.TranslateFixtures(typeof(StringJoinProbe),["ArrayQuota"]))[0]);
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Exact_array_join_recovers_from_buffer_and_result_allocation_limits(bool optimized)
    {
        var program=await QuotaProgram.Value;var artifact=ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string symbol="cilica_m_"+program.Flow.Flow.Flow.Entry.ContentHash+"_"+program.Flow.Flow.Flow.Entry.Token.ToString("x8",System.Globalization.CultureInfo.InvariantCulture);
        StringJoinProbe.ArrayQuota(0,0);int expected=StringJoinProbe.ArrayQuota(1,0);StringJoinProbe.ArrayQuota(-1,0);
        string text=artifact.Text+$$"""
            #include <cassert>
            int main() {
                using namespace {{artifact.EntryPoint[..^4]}}_closure;
                Instance heap;heap.collect_before_allocation=true;Context initialize(heap);LocationArgument args[2]{};
                assert({{symbol}}(initialize,args)==1 && initialize.pending==0);
                args[0].bits=1;assert({{symbol}}(initialize,args)=={{expected}} && initialize.pending==0);
                args[0].bits=0xffffffffu;assert({{symbol}}(initialize,args)==0 && initialize.pending==0);
                assert(collect(initialize).status==MarkStatus::complete);const auto permanent=heap.count;
                args[0].bits=0;assert({{symbol}}(initialize,args)==1 && initialize.pending==0);
                assert(collect(initialize).status==MarkStatus::complete);const auto retained=heap.count;assert(retained>permanent);
                args[0].bits=1;
                for(std::size_t allowance : {0u,1u}) {
                    heap.object_limit=retained+allowance;Context failure(heap);(void){{symbol}}(failure,args);
                    assert(failure.pending==5 && failure.top==nullptr && failure.borrows==nullptr && failure.used==0 && failure.depth==0);
                    heap.object_limit=4096;assert(collect(initialize).status==MarkStatus::complete && heap.count==retained);
                    Context recovery(heap);assert({{symbol}}(recovery,args)=={{expected}} && recovery.pending==0);
                    assert(recovery.top==nullptr && recovery.borrows==nullptr && recovery.used==0);
                    assert(collect(recovery).status==MarkStatus::complete && heap.count==retained);
                }
                heap.limit=heap.bytes;Context bytes(heap);(void){{symbol}}(bytes,args);
                assert(bytes.pending==5 && bytes.top==nullptr && bytes.borrows==nullptr && bytes.used==0);
                heap.limit=1048576;Context recovered(heap);assert({{symbol}}(recovered,args)=={{expected}} && recovered.pending==0);
                args[0].bits=0xffffffffu;assert({{symbol}}(recovered,args)==0 && recovered.pending==0);
                assert(collect(recovered).status==MarkStatus::complete && heap.count==permanent);
            }
            """;
        using var files=new ResolverFiles();string dir=files.DirectoryPath("join-quota"),cpp=Path.Combine(dir,"join.cpp"),exe=Path.Combine(dir,"join");
        await File.WriteAllTextAsync(cpp,text,TestContext.Current.CancellationToken);NativeEvidence.Record(cpp,optimized);
        var build=await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX")??"clang++",
            ["-std=c++20","-fno-exceptions","-fno-rtti","-Wall","-Wextra","-Werror",optimized?"-O2":"-O0","-Wframe-larger-than=2048","-fsanitize=address,undefined","-fno-sanitize-recover=all",cpp,"-o",exe],dir);
        Assert.True(build.ExitCode==0,build.Stderr);NativeEvidence.Record(cpp,optimized,exe);
        var run=await NativeProcess.Run(exe,[],dir);Assert.True(run.ExitCode==0,run.Stdout+run.Stderr);Assert.Empty(run.Stderr);
    }
    [Theory] [InlineData("UnsupportedGeneric")] [InlineData("UnsupportedChar")] [InlineData("UnsupportedRange")] [InlineData("UnsupportedObject")]
    public async Task Broader_join_overloads_remain_diagnostic(string name)
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(StringJoinProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(StringJoinProbe).GetMethod(name)!.MetadataToken),implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("No implementation method matches",StringComparison.Ordinal));
    }
}
