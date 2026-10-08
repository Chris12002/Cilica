using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class LongTupleTests
{
    private static readonly string[] Names = ["Single", "Fields", "Copies", "Heap", "Arrays", "Boxing", "Nullable", "InvalidRest", "PublicTupleInterfaceIsInsufficient", "NestedDefaultRest", "Equality", "RestEffects", "HeapReturn"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(LongTupleProbe), Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)] [InlineData(false,4)] [InlineData(true,4)]
    public async Task Tuple_storage_rest_validation_and_equality_match_dotnet(bool optimized,int group)
    {
        var programs=await Programs.Value;
        var pairs=(from a in new[]{int.MinValue,-7,-1,0,1,7,int.MaxValue} from b in new[]{-7,-1,0,1,2,3,4,5,6,7} select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name,i)=>(name,programs[i],typeof(LongTupleProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*3).Take(3).ToArray(),pairs:pairs);
    }
    [Fact]
    public async Task Shape_grant_and_private_caller_are_required_and_other_tuple_members_remain_diagnostic()
    {
        using var files=new ResolverFiles();
        var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime=resolver;
        var bytes=await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken);
        var input=resolver.OpenInput(typeof(LongTupleProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name)=>(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(LongTupleProbe).GetMethod(name)!.MetadataToken);
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,bytes,built.ManifestSha256).Value!;
        foreach(var name in new[]{"UnsupportedHash","UnsupportedText"})
        {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,Method(name),implementation);
            Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("no implemented managed body",StringComparison.Ordinal));
        }
        var manifest=JsonSerializer.Deserialize<LibraryBuildManifest>(bytes,new JsonSerializerOptions{Converters={new JsonStringEnumConverter<LibraryIntrinsicCapability>()}})!;
        var artifact=Assert.Single(manifest.Artifacts);
        var changed=manifest with{Artifacts=[artifact with{Grants=[..artifact.Grants.Where(g=>g.Capability!=LibraryIntrinsicCapability.ClosedTupleShape)]}]};
        var deniedBytes=AssemblyResolver.SerializeLibraryBuildManifest(changed);
        using var denied=resolver.CreateTrustedLibraryCompilationScope(plan,deniedBytes,Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
        var rejected=ValueTranslator.TranslateIntegerObjects(denied,input,Method("Fields"),implementation);
        Assert.False(rejected.Success);Assert.Contains(rejected.Diagnostics,d=>d.Id==DiagnosticIds.LibraryIntrinsicDenied);
        var hostile=new ResolutionAssemblyBuilder(new("HostileTupleShape",new Version(1,0,0,0)));
        var helper=hostile.TypeReference("Cilica.Runtime","TupleShape`1",hostile.Reference(implementation.Identity));
        var owner=hostile.Specification(0x15,0x12,checked((byte)((MetadataTokens.GetRowNumber(helper)<<2)|1)),1,8);
        var call=hostile.Member(owner,"IsTuple",0,0,2);
        hostile.Type("Test","Caller");
        var root=hostile.Method("Run",[0,0,8],emit:il=>{il.Call(call);il.OpCode(ILOpCode.Ret);});
        var image=scope.OpenInput(files.Write("hostile.dll",hostile.Build())).Value!;
        var resultCaller=ValueTranslator.TranslateIntegerObjects(scope,image,root,implementation);
        Assert.False(resultCaller.Success);Assert.Contains(resultCaller.Diagnostics,d=>d.Message.Contains("lexical declaring scope",StringComparison.Ordinal));
    }
}
