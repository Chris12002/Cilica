using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class BoxedIntegerFormattingTests
{
    private static readonly string[] Names=["Signed","Unsigned","Wide","Mixed","Values","Syntax","Order"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(BoxedIntegerFormattingProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    public async Task Default_literal_integer_boxes_preserve_bits_padding_faults_and_callbacks(bool optimized,int group)
    {
        var programs=await Programs.Value;
        var pairs=(from a in new[]{int.MinValue,-1,0,1,2,3,int.MaxValue} from b in new[]{int.MinValue,-1,0,1,2,3,4,5,6,7,int.MaxValue} select(a,b)).ToArray();
        var saved=CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,Names.Select((name,i)=>(name,programs[i],typeof(BoxedIntegerFormattingProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(2).ToArray(),pairs);
        }finally{CultureInfo.CurrentCulture=saved;}
    }
    [Fact]
    public async Task Suffixes_dynamic_literals_object_aliases_and_other_scalars_stay_diagnostic()
    {
        using var files=new ResolverFiles();var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(BoxedIntegerFormattingProbe).Assembly.Location).Value!;
        foreach(string suffix in new[]{"Suffix","EmptySuffix","Dynamic","LiteralAlias","Alias","Boolean","Character","Narrow"}) {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(BoxedIntegerFormattingProbe).GetMethod("Unsupported"+suffix)!.MetadataToken),implementation);
            Assert.False(result.Success,suffix);Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.ExecutionUnsupported
                && d.Message.Contains(suffix is "Suffix" or "EmptySuffix" or "Dynamic" or "LiteralAlias" ? "literal without format suffixes"
                    : suffix=="Alias" ? "directly boxed" : "Scalar boxed formatting",StringComparison.Ordinal));
        }
    }
}
