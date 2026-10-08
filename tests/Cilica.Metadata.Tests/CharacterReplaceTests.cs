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
public sealed class CharacterReplaceTests
{
    private static readonly string[] Names=["Text","Identity","Repeated","Null","Evaluation"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(CharacterReplaceProbe),Names));
    [Fact]
    public async Task Replacement_validates_at_both_stages()
    {foreach(var p in await Programs.Value){ManagedExceptionIrValidator.Validate(p);ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(p));}}
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Replacement_preserves_code_units_identity_and_evaluation(bool optimized)
    {
        var programs=await Programs.Value;
        var values=new[]{int.MinValue,-1,0,1,7,120,255,256,0x2222,0xd800,0xdfff,65535,65536,int.MaxValue};
        var pairs=(from a in values from b in values select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,Names.Select((name,i)=>(name,programs[i],typeof(CharacterReplaceProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).ToArray(),pairs,expectedStaticRoots:0);
    }
    [Theory] [InlineData("UnsupportedString")] [InlineData("UnsupportedComparison")]
    public async Task Broader_replacement_overloads_remain_diagnostic(string name)
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(CharacterReplaceProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CharacterReplaceProbe).GetMethod(name)!.MetadataToken),implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("No implementation method matches",StringComparison.Ordinal));
    }
}
