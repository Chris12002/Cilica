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
public sealed class CharacterDelegateTests
{
    private static readonly string[] Names=["Capture","Two","LiveLoop","Faults","NullTarget","SoleReceiver","AllUnits"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(CharacterDelegateProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)]
    public async Task Character_callbacks_preserve_roots_mutations_and_faults(bool optimized,int group)
    {
        var programs=await Programs.Value;
        int[] units=[0,1,-1,65,255,256,32767,32768,0xd800,0xdfff,0xffff,0x10000,int.MinValue,int.MaxValue];
        var pairs=(from a in units from b in new[]{0,1,-1,65,0xffff} select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,Names.Take(6).Select((name,i)=>
            (name,programs[i],typeof(CharacterDelegateProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(2).ToArray(),pairs);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task All_UTF16_units_survive_delegate_arguments(bool optimized)
    {
        var p=await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("CharUnits",p[6],CharacterDelegateProbe.AllUnits)],Enumerable.Range(0,2048).Select(i=>(i*32,0)).ToArray());
    }
    [Theory] [InlineData("UnsupportedByte")] [InlineData("UnsupportedUShort")] [InlineData("UnsupportedResult")]
    public async Task Other_narrow_signatures_remain_diagnostic(string name)
    {
        using var files=new ResolverFiles();
        var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(CharacterDelegateProbe).Assembly.Location).Value!;
        var method=(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CharacterDelegateProbe).GetMethod(name)!.MetadataToken);
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,method,implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id=="CIL2001" && d.Message.Contains("Delegate requires",StringComparison.Ordinal));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Character_LINQ_live_mutation_matches_dotnet(bool optimized)
    {
        var p=await CoreLibraryBuildTests.TranslateFixtures(typeof(CharacterDelegateProbe),"LiveAny");
        int[] units=[0,1,-1,65,255,256,32767,32768,0xd800,0xdfff,0xffff,0x10000,int.MinValue,int.MaxValue];
        var pairs=(from a in units from b in new[]{0,1,-1,65,0xffff} select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("LiveAny",p[0],CharacterDelegateProbe.LiveAny)],pairs);
    }
    [Fact]
    public void Standalone_delegate_profile_still_rejects_character_parameters()
    {
        using var resolver=Cilica.Metadata.Resolution.AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library=resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var input=resolver.OpenInput(typeof(CharacterDelegateProbe).Assembly.Location).Value!;
        var method=(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CharacterDelegateProbe).GetMethod("Two")!.MetadataToken);
        var result=ValueTranslator.TranslateDelegates(resolver,input,method,library);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("Delegate requires",StringComparison.Ordinal));
    }
}
