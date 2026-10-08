using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class CharacterSearchTests
{
    private static readonly string[] Names = ["Basic", "Empty", "Start", "Range", "EmptyRange", "Dynamic", "NullOrder"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(CharacterSearchProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    [InlineData(false,4)] [InlineData(true,4)] [InlineData(false,5)] [InlineData(true,5)]
    [InlineData(false,6)] [InlineData(true,6)]
    public async Task Character_search_and_exact_range_faults_match_dotnet(bool optimized,int index)
    {
        int[] bounds=[int.MinValue,-1,0,1,2,3,6,7,8,int.MaxValue];
        int[] chars=[0,44,97,98,122,0x7f,0x80,0xd7ff,0xd800,0xdbff,0xdc00,0xdfff,0xe000,0xffff,-1,int.MaxValue];
        var first=Names[index] is "Range" or "EmptyRange" or "NullOrder" ? bounds : chars;
        var pairs=(from a in first from b in bounds select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(Names[index],(await Programs.Value)[index],typeof(CharacterSearchProbe).GetMethod(Names[index])!.CreateDelegate<Func<int,int,int>>())],pairs);
    }
    [Fact]
    public async Task String_and_comparison_mode_overloads_remain_diagnostic()
    {
        using var files=new ResolverFiles();
        var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=resolver.OpenInput(typeof(CharacterSearchProbe).Assembly.Location).Value!;
        foreach(var name in new[]{"UnsupportedString","UnsupportedComparison"})
        {
            var method=(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CharacterSearchProbe).GetMethod(name)!.MetadataToken);
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,method,implementation);
            Assert.False(result.Success); Assert.Contains(result.Diagnostics,d=>d.Message.Contains("No implementation method matches",StringComparison.Ordinal)&&d.Message.Contains("IndexOf",StringComparison.Ordinal));
        }
    }
}
