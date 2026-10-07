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
public sealed class DictionaryAddReadTests
{
    private static readonly string[] Names=["Int32","UInt32","Int64","UInt64","Strings","NullOrder","Mutation","Payloads","NamedDuplicate","NamedMissing","NamedReentrantDuplicate"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(DictionaryAddReadProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)]
    [InlineData(false,3)] [InlineData(true,3)] [InlineData(false,4)] [InlineData(true,4)] [InlineData(false,5)] [InlineData(true,5)]
    public async Task Checked_insertion_lookup_key_text_and_callback_order_match_dotnet(bool optimized,int group)
    {
        var programs=await Programs.Value;
        var pairs=(from a in new[]{int.MinValue,-7,-1,0,1,7,int.MaxValue} from b in new[]{int.MinValue,-7,-1,0,1,2,3,4,5,6,7,int.MaxValue} select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,Names.Select((name,i)=>(name,programs[i],typeof(DictionaryAddReadProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(2).ToArray(),pairs:pairs);
    }
    [Fact]
    public async Task Key_text_requires_exact_grant_and_rejects_general_formattable_keys()
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        var bytes=await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken);
        var input=resolver.OpenInput(typeof(DictionaryAddReadProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name)=>(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(DictionaryAddReadProbe).GetMethod(name)!.MetadataToken);
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,bytes,built.ManifestSha256).Value!;
        var custom=ValueTranslator.TranslateIntegerObjects(scope,input,Method("UnsupportedFormattable"),implementation);
        Assert.False(custom.Success);Assert.Contains(custom.Diagnostics,d=>d.Message.Contains("IFormattable",StringComparison.Ordinal));
        var manifest=JsonSerializer.Deserialize<LibraryBuildManifest>(bytes,new JsonSerializerOptions{Converters={new JsonStringEnumConverter<LibraryIntrinsicCapability>()}})!;
        var artifact=Assert.Single(manifest.Artifacts);
        var changed=manifest with{Artifacts=[artifact with{Grants=[..artifact.Grants.Where(g=>g.Capability!=LibraryIntrinsicCapability.DictionaryKeyText)]}]};
        var deniedBytes=AssemblyResolver.SerializeLibraryBuildManifest(changed);
        using var denied=resolver.CreateTrustedLibraryCompilationScope(plan,deniedBytes,Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
        foreach(string name in new[]{"Int32","Strings","NamedDuplicate"})
        {
            var result=ValueTranslator.TranslateIntegerObjects(denied,input,Method(name),implementation);
            Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.LibraryIntrinsicDenied);
        }
        var hostile=new ResolutionAssemblyBuilder(new("HostileDictionaryKeyText",new Version(1,0,0,0)));
        var helper=hostile.TypeReference("Cilica.Runtime","DictionaryKeyText`1",hostile.Reference(implementation.Identity));
        var owner=hostile.Specification(0x15,0x12,checked((byte)((MetadataTokens.GetRowNumber(helper)<<2)|1)),1,8);
        var call=hostile.Member(owner,"Render",0,1,0x0e,0x13,0);
        hostile.Type("Test","Caller");var root=hostile.Method("Run",[0,0,8],emit:il=>{il.LoadConstantI4(0);il.Call(call);il.OpCode(ILOpCode.Pop);il.LoadConstantI4(0);il.OpCode(ILOpCode.Ret);});
        var caller=scope.OpenInput(files.Write("hostile.dll",hostile.Build())).Value!;
        var rejected=ValueTranslator.TranslateIntegerObjects(scope,caller,root,implementation);
        Assert.False(rejected.Success);Assert.Contains(rejected.Diagnostics,d=>d.Message.Contains("lexical declaring scope",StringComparison.Ordinal));
    }
}
