using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class EnumValuesTests
{
    [Theory]
    [InlineData(false,"Limit")] [InlineData(true,"Limit")]
    [InlineData(false,"Ordered")] [InlineData(true,"Ordered")]
    [InlineData(false,"Flags")] [InlineData(true,"Flags")]
    [InlineData(false,"Fresh")] [InlineData(true,"Fresh")]
    [InlineData(false,"Empty")] [InlineData(true,"Empty")]
    [InlineData(false,"Views")] [InlineData(true,"Views")]
    [InlineData(false,"UnsignedAliases")] [InlineData(true,"UnsignedAliases")]
    [InlineData(false,"Aliases")] [InlineData(true,"Aliases")]
    [InlineData(false,"Private")] [InlineData(true,"Private")]
    [InlineData(false,"Faults")] [InlineData(true,"Faults")]
    public async Task Named_enum_values_order_identity_views_and_faults_match_dotnet(bool optimized,string name)
    {
        var program=(await CoreLibraryBuildTests.TranslateFixtures(typeof(EnumValuesProbe),[name]))[0];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(name,program,typeof(EnumValuesProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())]);
    }
    [Theory]
    [InlineData("UnsupportedDynamic")] [InlineData("UnsupportedAlias")] [InlineData("UnsupportedJoin")]
    [InlineData("UnsupportedArrayEnumeration")] [InlineData("UnsupportedLarge")] [InlineData("UnsupportedNull")] [InlineData("UnsupportedNonEnum")] [InlineData("UnsupportedWide")] [InlineData("UnsupportedNarrow")]
    public async Task Unproven_types_and_other_carriers_remain_diagnostic(string name)
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(EnumValuesProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(EnumValuesProbe).GetMethod(name)!.MetadataToken),implementation);
        Assert.False(result.Success);Assert.Null(result.Program);Assert.Contains(result.Diagnostics,d=>d.Message.Contains(name is "UnsupportedWide" or "UnsupportedNarrow" ? "Int32 enum" : name=="UnsupportedArrayEnumeration" ? "GetEnumerator" : "Enum.GetValues",StringComparison.Ordinal));
    }
    [Theory]
    [InlineData("GetValues")] [InlineData("GetTypeFromHandle")]
    public async Task Both_exact_method_grants_are_required(string member)
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        var bytes=await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken);
        var manifest=JsonSerializer.Deserialize<LibraryBuildManifest>(bytes,new JsonSerializerOptions{Converters={new JsonStringEnumConverter<LibraryIntrinsicCapability>()}})!;
        var artifact=Assert.Single(manifest.Artifacts);
        Assert.Single(artifact.Grants.Where(g=>g.Capability==LibraryIntrinsicCapability.EnumValues&&g.Member==member));
        var denied=manifest with{Artifacts=[artifact with{Grants=[..artifact.Grants.Where(g=>g.Capability!=LibraryIntrinsicCapability.EnumValues||g.Member!=member)]}]};
        var deniedBytes=AssemblyResolver.SerializeLibraryBuildManifest(denied);
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,deniedBytes,Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
        var input=scope.OpenInput(typeof(EnumValuesProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(EnumValuesProbe).GetMethod("Ordered")!.MetadataToken),implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.LibraryIntrinsicDenied);
    }
    [Fact]
    public async Task A_branch_into_the_typeof_sequence_cannot_manufacture_a_constant_type()
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var fixture=scope.OpenInput(typeof(EnumValuesProbe).Assembly.Location).Value!;
        var raw=new ResolutionAssemblyBuilder(new("EnumValuesJoin",new Version(1,0,0,0)));
        var sparse=raw.TypeReference("Cilica.Fixtures.Execution","ValuesSparse",raw.Reference(fixture.Identity));
        var flags=raw.TypeReference("Cilica.Fixtures.Execution","ValuesFlags",raw.Reference(fixture.Identity));
        var core=raw.Reference(implementation.Identity);
        var type=raw.TypeReference("System","Type",core);var handle=raw.TypeReference("System","RuntimeTypeHandle",core);
        var array=raw.TypeReference("System","Array",core);var enumeration=raw.TypeReference("System","Enum",core);
        byte[] Signature(EntityHandle result,EntityHandle argument,bool value)
        {
            var blob=new BlobBuilder();blob.WriteBytes(new byte[]{0,1,0x12});blob.WriteCompressedInteger(CodedIndex.TypeDefOrRef(result));
            blob.WriteByte(value?(byte)0x11:(byte)0x12);blob.WriteCompressedInteger(CodedIndex.TypeDefOrRef(argument));return blob.ToArray();
        }
        var fromHandle=raw.Member(type,"GetTypeFromHandle",Signature(type,handle,true));
        var getValues=raw.Member(enumeration,"GetValues",Signature(array,type,false));raw.Type("Test","Caller");
        var root=raw.Method("Run",[0,0,8],emit:il=>{
            var join=il.DefineLabel();il.OpCode(ILOpCode.Ldtoken);il.Token(flags);il.LoadConstantI4(0);il.Branch(ILOpCode.Brtrue,join);
            il.OpCode(ILOpCode.Pop);il.OpCode(ILOpCode.Ldtoken);il.Token(sparse);il.MarkLabel(join);
            il.Call(fromHandle);il.Call(getValues);il.OpCode(ILOpCode.Ldlen);il.OpCode(ILOpCode.Conv_i4);il.OpCode(ILOpCode.Ret);
        });
        var input=scope.OpenInput(files.Write("enum-values-join.dll",raw.Build())).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,root,implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("cannot cross control-flow",StringComparison.Ordinal));
    }

}
