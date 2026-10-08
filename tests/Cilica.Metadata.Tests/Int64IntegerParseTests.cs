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
public sealed class Int64IntegerParseTests
{
    private static readonly string[] Names=["Parse","Roundtrip","Heap","NullOutput"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(Int64IntegerParseProbe),Names));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Invariant_integer_grammar_and_out_values_match_dotnet(bool optimized)
    {
        var programs=await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("Parse",programs[0],Int64IntegerParseProbe.Parse),("Heap",programs[2],Int64IntegerParseProbe.Heap)],
            (from a in Enumerable.Range(0,64) from b in new[]{0,1,2,-1} select(a,b)).ToArray());
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("Roundtrip",programs[1],Int64IntegerParseProbe.Roundtrip),("NullOutput",programs[3],Int64IntegerParseProbe.NullOutput)]);
    }
    [Fact]
    public void Owned_parser_matches_all_utf16_edge_characters_and_seeded_numbers()
    {
        void Check(string? text) {
            bool expected=long.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out long expectedValue);
            bool actual=Library.IntegerParsing.Int64Integer(text!,out long actualValue);
            Assert.Equal(expected,actual);Assert.Equal(expectedValue,actualValue);
        }
        for(int i=0;i<64;i++)Check(Int64IntegerParseProbe.Input(i));
        for(int i=0;i<=char.MaxValue;i++){Check(((char)i)+"17");Check("17"+((char)i));}
        var random=new Random(713);
        for(int i=0;i<512;i++){long value=random.NextInt64(long.MinValue,long.MaxValue);Check(value.ToString(CultureInfo.InvariantCulture));}
    }
    [Theory]
    [InlineData("code")] [InlineData("source")] [InlineData("instance")] [InlineData("width")]
    public async Task Both_stages_reject_forged_parser_declarations(string damage)
    {
        var program=(await Programs.Value)[0];var scalar=ValueScalarLowering.LowerExceptions(program);var objects=scalar.Flow.Flow.Flow.Flow;
        var original=objects.Bindings.Single(b=>b.IntegerParser is not null);
        var changed=damage switch {
            "code"=>original with {IntegerParser=original.IntegerParser! with {Code=PrimitiveTypeCode.UInt64}},
            "source"=>original with {IntegerParser=original.IntegerParser! with {Source=original.IntegerParser.Source with {Name="System.UInt64"}}},
            "width"=>original with {IntegerParser=original.IntegerParser! with {Code=PrimitiveTypeCode.Int32,Source=original.IntegerParser.Source with {Name="System.Int32"}}},
            _=>original with {Kind=ObjectMethodKind.Instance}
        };
        var bindings=objects.Bindings.Replace(original,changed);
        Assert.Throws<ArgumentException>(()=>ManagedExceptionIrValidator.Validate(program with {Flow=program.Flow with {Bindings=bindings}}));
        Assert.Throws<ArgumentException>(()=>ScalarManagedExceptionIrValidator.Validate(scalar with {Flow=scalar.Flow with {Flow=scalar.Flow.Flow with {Flow=scalar.Flow.Flow.Flow with {Flow=objects with {Bindings=bindings}}}}}));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Both_stages_reject_unproven_parser_operands(bool provider)
    {
        var program=(await Programs.Value)[0];var scalar=ValueScalarLowering.LowerExceptions(program);var objects=scalar.Flow.Flow.Flow.Flow;
        IrStatement Corrupt(IrStatement statement) => statement is IrStatement.Define d ? d.Value.Expression switch {
            IrExpression.InvariantCulture when provider => d with {Value=d.Value with {Type=IrType.NullReference,Expression=new IrExpression.Null()}},
            IrExpression.Constant {Value:7} when !provider => d with {Value=d.Value with {Expression=new IrExpression.Constant(8)}},
            _=>statement
        } : statement;
        var logical=program.Flow.Flow.Flow;
        logical=logical with {Methods=[..logical.Methods.Select(m=>m with {Blocks=[..m.Blocks.Select(b=>b with {Statements=[..b.Statements.Select(Corrupt)]})]})]};
        Assert.Throws<ArgumentException>(()=>ManagedExceptionIrValidator.Validate(program with {Flow=program.Flow with {Flow=new(logical)}}));
        objects=objects with {Flow=objects.Flow with {Methods=[..objects.Flow.Methods.Select(m=>m with {Blocks=[..m.Blocks.Select(b=>b with {Statements=[..b.Statements.Select(Corrupt)]})]})]}};
        Assert.Throws<ArgumentException>(()=>ScalarManagedExceptionIrValidator.Validate(scalar with {Flow=scalar.Flow with {Flow=scalar.Flow.Flow with {Flow=scalar.Flow.Flow.Flow with {Flow=objects}}}}));
    }
    [Fact]
    public async Task Exact_grant_and_proven_style_provider_are_required()
    {
        using var files=new ResolverFiles();var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        var bytes=await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken);
        var manifest=JsonSerializer.Deserialize<LibraryBuildManifest>(bytes,new JsonSerializerOptions {Converters={new JsonStringEnumConverter<LibraryIntrinsicCapability>()}})!;
        var artifact=Assert.Single(manifest.Artifacts);Assert.Single(artifact.Grants.Where(g=>g.Capability==LibraryIntrinsicCapability.InvariantIntegerParsing && g.Owner=="System.Int64" && g.Member=="TryParse"));
        var narrowed=manifest with {Artifacts=[artifact with {Grants=[..artifact.Grants.Where(g=>g.Capability!=LibraryIntrinsicCapability.InvariantIntegerParsing || g.Owner!="System.Int64" || g.Member!="TryParse")]}]};
        var deniedBytes=AssemblyResolver.SerializeLibraryBuildManifest(narrowed);var input=resolver.OpenInput(typeof(Int64IntegerParseProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name)=>(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(Int64IntegerParseProbe).GetMethod(name)!.MetadataToken);
        using(var denied=resolver.CreateTrustedLibraryCompilationScope(plan,deniedBytes,Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!) {
            var result=ValueTranslator.TranslateIntegerObjects(denied,input,Method("Parse"),implementation);
            Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.LibraryIntrinsicDenied);
            var narrow=(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(IntegerParseProbe).GetMethod("Parse")!.MetadataToken);
            Assert.True(ValueTranslator.TranslateIntegerObjects(denied,input,narrow,implementation).Success);
        }
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,bytes,built.ManifestSha256).Value!;
        foreach(var name in new[]{"UnsupportedStyle","UnsupportedProvider","UnsupportedDefault","UnsupportedDynamicStyle","UnsupportedFlagsText"}) {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,Method(name),implementation);Assert.False(result.Success,name);Assert.NotEmpty(result.Diagnostics);
        }
    }
}
