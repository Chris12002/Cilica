using System;
using System.Globalization;
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
public sealed class DefaultIntegerTextTests
{
    private static readonly string[] Names = ["FormatSigned32", "FormatUnsigned32", "FormatSigned64", "FormatUnsigned64", "IntegerHeapText", "IntegerText", "IntegerTextIdentity"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(DefaultIntegerTextProbe), Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    public async Task Parameterless_text_matches_invariant_oracle_code_units_and_identity(bool optimized, int group)
    {
        var programs=await Programs.Value;
        var probes=Names.Select((name,i)=>(name,programs[i],typeof(DefaultIntegerTextProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>()));
        var saved=CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
            if(group==0) await IntegerObjectTranslationTests.VerifyTranslated(optimized,probes.Take(4).ToArray(),
                (from index in Enumerable.Range(0,17) from position in Enumerable.Range(-1,22) select(index,position)).ToArray());
            else
            {
                await IntegerObjectTranslationTests.VerifyTranslated(optimized,probes.Skip(4).Take(2).ToArray());
                await IntegerObjectTranslationTests.VerifyTranslated(optimized,probes.Skip(6).ToArray(),
                    [.. new[]{0,1,9,10,99,100,255,298,299,300,-1}.Select(value=>(value,0))]);
            }
        }
        finally { CultureInfo.CurrentCulture=saved; }
    }
    [Theory]
    [InlineData("Int32", "FormatSigned32")] [InlineData("UInt32", "FormatUnsigned32")]
    [InlineData("Int64", "FormatSigned64")] [InlineData("UInt64", "FormatUnsigned64")]
    public async Task Parameterless_overload_needs_its_own_exact_grant(string owner,string probe)
    {
        using var files=new ResolverFiles();var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        var manifest=JsonSerializer.Deserialize<LibraryBuildManifest>(await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),
            new JsonSerializerOptions{Converters={new JsonStringEnumConverter<LibraryIntrinsicCapability>()}})!;
        var type=resolver.ResolveType(implementation,"System",owner).Value!;
        var method=resolver.ResolveClosedDeclaration(new(type,SignatureList<TypeSignature>.Empty),"ToString",0,new MemberResolutionBudget()).Value!;
        var artifact=Assert.Single(manifest.Artifacts);
        var grant=Assert.Single(artifact.Grants.Where(g=>g.Capability==LibraryIntrinsicCapability.InvariantIntegerFormatting && g.Token==MetadataTokens.GetToken(method.Handle)));
        var bytes=AssemblyResolver.SerializeLibraryBuildManifest(manifest with {Artifacts=[artifact with {Grants=artifact.Grants.Remove(grant)}]});
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,bytes,Convert.ToHexStringLower(SHA256.HashData(bytes))).Value!;
        var input=scope.OpenInput(typeof(DefaultIntegerTextProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(DefaultIntegerTextProbe).GetMethod(probe)!.MetadataToken),implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.LibraryIntrinsicDenied);
    }
}
