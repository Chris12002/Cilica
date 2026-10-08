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
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class OrdinalOrderingGrantTests
{
    [Fact]
    public async Task Each_ordinal_overload_requires_its_exact_bridge_grant()
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        var manifest=JsonSerializer.Deserialize<LibraryBuildManifest>(await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),new JsonSerializerOptions{Converters={new JsonStringEnumConverter<LibraryIntrinsicCapability>()}})!;
        var artifact=Assert.Single(manifest.Artifacts);
        foreach(var(member,probe)in new[]{("OrderBy","Ascending"),("OrderByDescending","Descending"),("ThenBy","Chain"),("ThenByDescending","Repeated")})
        {
            var grant=Assert.Single(artifact.Grants.Where(g=>g.Capability==LibraryIntrinsicCapability.OrdinalOrdering&&g.Member==member));
            var bytes=AssemblyResolver.SerializeLibraryBuildManifest(manifest with{Artifacts=[artifact with{Grants=artifact.Grants.Remove(grant)}]});
            using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,bytes,Convert.ToHexStringLower(SHA256.HashData(bytes))).Value!;
            var input=scope.OpenInput(typeof(OrdinalOrderingProbe).Assembly.Location).Value!;
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(OrdinalOrderingProbe).GetMethod(probe)!.MetadataToken),implementation);
            Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.LibraryIntrinsicDenied);
        }
    }
    [Fact]
    public async Task Application_code_cannot_call_the_bridge_directly()
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var b=new ResolutionAssemblyBuilder(new("DirectOrdinalBridge",new Version(1,0,0,0)));
        var helper=b.TypeReference("Cilica.Runtime","OrdinalOrderBridge`1",b.Reference(implementation.Identity));
        var selected=b.Specification(0x15,0x12,checked((byte)((MetadataTokens.GetRowNumber(helper)<<2)|1)),1,8);
        // Exact public helper resolved by method definition, then instantiate its class owner.
        var enumerable=b.TypeReference("System.Linq","IOrderedEnumerable`1",b.Reference(implementation.Identity));
        var source=b.TypeReference("System.Collections.Generic","IEnumerable`1",b.Reference(implementation.Identity));
        var function=b.TypeReference("System","Func`2",b.Reference(implementation.Identity));
        var comparer=b.TypeReference("System.Collections.Generic","IComparer`1",b.Reference(implementation.Identity));
        byte Ref(TypeReferenceHandle h)=>checked((byte)((MetadataTokens.GetRowNumber(h)<<2)|1));
        var call=b.Member(selected,"OrderBy",0,3,0x15,0x12,Ref(enumerable),1,0x13,0,0x15,0x12,Ref(source),1,0x13,0,0x15,0x12,Ref(function),2,0x13,0,0x0e,0x15,0x12,Ref(comparer),1,0x0e);
        b.Type("Test","Caller");var root=b.Method("Run",[0,0,8],emit:il=>{il.OpCode(ILOpCode.Ldnull);il.OpCode(ILOpCode.Ldnull);il.OpCode(ILOpCode.Ldnull);il.Call(call);il.OpCode(ILOpCode.Pop);il.LoadConstantI4(0);il.OpCode(ILOpCode.Ret);});
        var input=scope.OpenInput(files.Write("direct.dll",b.Build())).Value!;var result=ValueTranslator.TranslateIntegerObjects(scope,input,root,implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("compiler-selected",StringComparison.Ordinal));
    }
}
