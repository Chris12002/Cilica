using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ListSortTests
{
    private static readonly string[] Names=["Integers","Adversarial","Faults","Inconsistent","Reentrant","Validation","References","Values","ReferenceFault","ValueFault"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(ListSortProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,3)] [InlineData(true,3)] [InlineData(false,4)] [InlineData(true,4)]
    public async Task Comparison_sort_preserves_order_mutations_and_faults(bool optimized,int group)
    {
        var programs=await Programs.Value;
        var probes=Names.Select((name,i)=>(name,programs[i],typeof(ListSortProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>()));
        if(group==0)
        {
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,probes.Take(1).ToArray(),
                (from a in new[]{0,1,2,3,15,16,17,31,32,63,64,79} from b in Enumerable.Range(0,6) select(a,b)).ToArray());
            // Two distinct inputs cover both large organ-pipe sizes without repeating
            // identical allocations under forced collection 49 times.
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,probes.Skip(1).Take(1).ToArray(),[(1,0),(-1,0)]);
        }
        else if(group==1)
        {
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,probes.Skip(2).Take(1).ToArray(),
                (from a in Enumerable.Range(0,81) from b in new[]{-1,1} select(a,b)).ToArray());
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,probes.Skip(3).Take(1).ToArray());
        }
        else if(group==2)
        {
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,probes.Skip(4).Take(1).ToArray(),
                (from a in new[]{0,1,3,7} from b in Enumerable.Range(0,6) select(a,b)).ToArray());
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,probes.Skip(5).Take(1).ToArray());
        }
        else await IntegerObjectTranslationTests.VerifyTranslated(optimized,probes.Skip(group*2).Take(2).ToArray());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Reentrant_and_validation(bool optimized) => Comparison_sort_preserves_order_mutations_and_faults(optimized,2);

    [Fact]
    public async Task Sort_display_name_requires_its_exact_grant_and_private_caller()
    {
        using var files=new ResolverFiles();
        var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime=resolver;
        var bytes=await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken);
        var manifest=JsonSerializer.Deserialize<LibraryBuildManifest>(bytes,new JsonSerializerOptions {Converters={new JsonStringEnumConverter<LibraryIntrinsicCapability>()}})!;
        var artifact=Assert.Single(manifest.Artifacts);
        var grant=Assert.Single(artifact.Grants.Where(g=>g.Capability==LibraryIntrinsicCapability.SortComparisonName));
        var narrowed=manifest with {Artifacts=[artifact with {Grants=artifact.Grants.Remove(grant)}]};
        var deniedBytes=AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
        using var denied=resolver.CreateTrustedLibraryCompilationScope(plan,deniedBytes,Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
        var input=resolver.OpenInput(typeof(ListSortProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(denied,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ListSortProbe).GetMethod("Integers")!.MetadataToken),implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.LibraryIntrinsicDenied);
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,bytes,built.ManifestSha256).Value!;
        var builder=new ResolutionAssemblyBuilder(new("ForeignSortName",new Version(1,0,0,0)));
        var helper=builder.TypeReference("Cilica.Runtime","ComparisonSort`1",builder.Reference(implementation.Identity));
        var closed=builder.Specification(0x15,0x12,(byte)((MetadataTokens.GetRowNumber(helper)<<2)|1),1,8);
        var name=builder.Member(closed,"ComparisonName",0,0,0x0e);
        builder.Type("Test","Caller");
        var entry=builder.Method("Run",[0,2,8,8,8],emit:body=>{body.Call(name);body.OpCode(ILOpCode.Pop);body.LoadConstantI4(0);body.OpCode(ILOpCode.Ret);});
        var foreign=scope.OpenInput(files.Write("foreign.dll",builder.Build())).Value!;
        result=ValueTranslator.TranslateIntegerObjects(scope,foreign,entry,implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("Explicit type use requires lexical declaring scope",StringComparison.Ordinal));
    }
}
