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
public sealed class CandidateDictionaryComparerTests
{
    private static readonly string[] Names=["Selection","Alias","Collisions","Faults","NullKey","Construction","Roots","Integers","ValueCopies","DefaultValueKey"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateDictionaryComparerProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    [InlineData(false,4)] [InlineData(true,4)] [InlineData(false,5)] [InlineData(true,5)]
    [InlineData(false,6)] [InlineData(true,6)] [InlineData(false,7)] [InlineData(true,7)]
    [InlineData(false,8)] [InlineData(true,8)]
    public async Task Selected_comparer_callbacks_values_identity_and_faults_match_dotnet(bool optimized,int index)
    {
        int[] inputs=[-1,0,1,2,3,4,5,6,7,127,int.MinValue,int.MaxValue];
        var pairs=(from a in inputs from b in new[]{-1,0,1,7,int.MaxValue} select(a,b)).ToArray();
        var program=(await Programs.Value)[index];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(Names[index],program,
            typeof(CandidateDictionaryComparerProbe).GetMethod(Names[index])!.CreateDelegate<Func<int,int,int>>())],pairs,expectedStaticRoots:index==1||index==9?1:0);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public Task Explicit_default_comparer_preserves_mutable_value_key_receiver(bool optimized)
        => Selected_comparer_callbacks_values_identity_and_faults_match_dotnet(optimized,9);
    [Theory] [InlineData("UnsupportedIgnoreCase","get_OrdinalIgnoreCase")] [InlineData("UnsupportedCulture","get_CurrentCulture")]
    public async Task Missing_text_comparers_remain_exact_diagnostics(string name,string member)
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(CandidateDictionaryComparerProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CandidateDictionaryComparerProbe).GetMethod(name)!.MetadataToken),implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id=="CIL3010"&&d.Message.Contains(member,StringComparison.Ordinal));
    }
}
