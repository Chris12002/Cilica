using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class CandidateCrcMappingTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Unsigned_crc_table_matches_dotnet(bool optimized)
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(CandidateCrcMappingProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CandidateCrcMappingProbe).GetMethod("UnsignedTable")!.MetadataToken),implementation);
        Assert.True(result.Success,string.Join("; ",result.Diagnostics));
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("UnsignedTable",result.Program!,CandidateCrcMappingProbe.UnsignedTable)]);
    }
}
