using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class CandidateCharacterArrayTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateCharacterArrayProbe), "Payload", "Independent", "AllUnits", "ReceiverFault"));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Copies_payload_independence_empty_identity_and_faults_match_dotnet(bool optimized)
    {
        var p = await Programs.Value;
        var pairs = (from scenario in Enumerable.Range(0,7) from index in Enumerable.Range(-4,24) select (scenario,index)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("CharacterPayload",p[0],CandidateCharacterArrayProbe.Payload), ("CharacterIndependence",p[1],CandidateCharacterArrayProbe.Independent)],pairs);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("CharacterReceiver",p[3],CandidateCharacterArrayProbe.ReceiverFault)]);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Every_utf16_code_unit_is_copied_exactly(bool optimized)
    {
        var p = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("CharacterUnits",p[2],CandidateCharacterArrayProbe.AllUnits)],Enumerable.Range(0,2048).Select(i=>(i*32,0)).ToArray());
    }
    [Theory]
    [InlineData("ReadOnlyReference",false)] [InlineData("ReadOnlyReference",true)]
    public async Task Minimal_decoder_compositions_match_dotnet(string name,bool optimized)
    {
        var p = await CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateCharacterArrayProbe),name);
        var oracle = typeof(CandidateCharacterArrayProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>();
        var pairs = name=="ReadOnlyReference"
            ? (from scenario in Enumerable.Range(0,7) from index in Enumerable.Range(-1,21) select(scenario,index)).ToArray()
            : new[]{(0,0),(65,0),(97,0),(0xd800,0),(0xffff,0),(-1,0)};
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(name,p[0],oracle)],pairs);
    }
    [Theory]
    [InlineData("MissingSection", "CIL3010", "ToCharArray")]
    [InlineData("CharacterPredicate", "CIL2001", "Narrow vector interfaces admit")]
    public async Task Remaining_candidate_requirements_have_exact_diagnostics(string name, string id, string message)
    {
        using var files=new ResolverFiles();
        var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=resolver.OpenInput(typeof(CandidateCharacterArrayProbe).Assembly.Location).Value!;
        var method=(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CandidateCharacterArrayProbe).GetMethod(name)!.MetadataToken);
        var translated=ValueTranslator.TranslateIntegerObjects(scope,input,method,implementation);
        Assert.False(translated.Success);
        Assert.Contains(translated.Diagnostics,d=>d.Id==id && d.Message.Contains(message,StringComparison.Ordinal));
    }
}
