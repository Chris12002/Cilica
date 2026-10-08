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
public sealed class StringJoinTests
{
    private static readonly string[] Names=["Array","List","Cursor","Identity","Projection","Growth","Null"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(StringJoinProbe),Names));
    [Fact]
    public async Task Join_validates_at_both_stages()
    {foreach(var p in await Programs.Value){ManagedExceptionIrValidator.Validate(p);ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(p));}}
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Strings_storage_and_cursor_order_match_dotnet(bool optimized)
    {
        var programs=await Programs.Value;
        var pairs=(from a in Enumerable.Range(0,32) from b in new[]{0,1,2,3} select(a,b)).ToArray();
        for(int i=0;i<Names.Length;i++)
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(Names[i],programs[i],typeof(StringJoinProbe).GetMethod(Names[i])!.CreateDelegate<Func<int,int,int>>())],pairs,expectedStaticRoots:0);
    }
    [Theory] [InlineData("UnsupportedGeneric")] [InlineData("UnsupportedChar")]
    public async Task Broader_join_overloads_remain_diagnostic(string name)
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(StringJoinProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(StringJoinProbe).GetMethod(name)!.MetadataToken),implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("No implementation method matches",StringComparison.Ordinal));
    }
}
