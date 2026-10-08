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
public sealed class StringSplitTests
{
    private static readonly string[] Names=["Basic","Separators","Whitespace","Dynamic","Identity","EmptyIdentity"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(StringSplitProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    [InlineData(false,4)] [InlineData(true,4)] [InlineData(false,5)] [InlineData(true,5)]
    public async Task Character_splits_options_text_and_identity_match_dotnet(bool optimized,int i)
    {
        var programs=await Programs.Value;
        int[] flags=[int.MinValue,-1,0,1,2,3,4,int.MaxValue];
        int[] first=Names[i] switch {
            "Basic"=>Enumerable.Range(-1,11).ToArray(),
            "Separators"=>[0,9,32,58,133,160,0xd800,0xdc00,'x','y',-1,int.MaxValue],
            "Whitespace"=>[0,8,9,10,11,12,13,14,32,133,160,0x1680,0x180e,0x2000,0x2001,0x2002,0x2003,0x2004,0x2005,0x2006,0x2007,0x2008,0x2009,0x200a,0x200b,0x2028,0x2029,0x202f,0x205f,0x3000,0xfeff,0xd800,0xffff],
            _=>[int.MinValue,-1,0,1,2,3,int.MaxValue]
        };
        var pairs=(from a in first from b in flags select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(Names[i],programs[i],typeof(StringSplitProbe).GetMethod(Names[i])!.CreateDelegate<Func<int,int,int>>())],pairs);
    }
    [Fact]
    public async Task Other_split_overloads_remain_diagnostic()
    {
        using var files=new ResolverFiles();
        var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=resolver.OpenInput(typeof(StringSplitProbe).Assembly.Location).Value!;
        foreach(string name in new[]{"UnsupportedCount","UnsupportedArray"})
        {
            var method=(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(StringSplitProbe).GetMethod(name)!.MetadataToken);
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,method,implementation);
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics,d=>d.Message.Contains("No implementation method matches",StringComparison.Ordinal)&&d.Message.Contains("Split",StringComparison.Ordinal));
        }
    }
}
