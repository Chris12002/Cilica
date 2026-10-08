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
public sealed class ThrowingInt64ParseTests
{
    private static readonly string[] Names=["Catalog","Precedence","Roundtrip","CodeUnits"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(ThrowingInt64ParseProbe),Names));
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Throwing_grammar_fault_precedence_and_messages_match_dotnet(bool optimized)
    {
        var programs=await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("Catalog",programs[0],ThrowingInt64ParseProbe.Catalog)],(from a in Enumerable.Range(0,64) from b in new[]{0,1,2} select(a,b)).ToArray());
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("Precedence",programs[1],ThrowingInt64ParseProbe.Precedence)],(from a in Enumerable.Range(0,32) from b in new[]{0,1,2} select(a,b)).ToArray());
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("Roundtrip",programs[2],ThrowingInt64ParseProbe.Roundtrip)]);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("CodeUnits",programs[3],ThrowingInt64ParseProbe.CodeUnits)],(from a in new[]{0,9,10,13,32,48,57,65,133,160,0xd800,0xdc00,0xffff,-1,int.MaxValue} from b in new[]{0,1} select(a,b)).ToArray());
    }
    [Fact]
    public void Owned_throwing_parser_matches_reduced_regressions_and_seeded_numbers()
    {
        (long Value,string Kind,string Message,string Parameter) Observe(Func<long> parse) {
            try{return(parse(),"","","");}catch(Exception error){return(0,error.GetType().FullName!,error.Message,(error as ArgumentException)?.ParamName??"");}
        }
        void Check(string? text)=>Assert.Equal(Observe(()=>long.Parse(text!,CultureInfo.InvariantCulture)),Observe(()=>Library.IntegerParsing.ParseInt64Integer(text!)));
        for(int i=0;i<64;i++)Check(Int64ParseProbe.Input(i));
        for(int i=0;i<32;i++)Check(ThrowingInt64ParseProbe.SpecialInput(i));
        for(int i=0;i<=char.MaxValue;i++) {
            string c=((char)i).ToString();Check(c+"17");Check("17"+c);Check(c+"9223372036854775808");Check("9223372036854775808"+c);
        }
        Check("-9223372036854775808");Check("9223372036854775807");
        var random=new Random(9182);for(int i=0;i<512;i++)Check(random.NextInt64(long.MinValue,long.MaxValue).ToString(CultureInfo.InvariantCulture));
    }
    [Theory]
    [InlineData("code")] [InlineData("source")] [InlineData("instance")] [InlineData("mode")] [InlineData("missing")]
    public async Task Both_stages_reject_forged_parser_declarations(string damage)
    {
        var program=(await Programs.Value)[0];var scalar=ValueScalarLowering.LowerExceptions(program);var objects=scalar.Flow.Flow.Flow.Flow;
        var original=objects.Bindings.Single(b=>b.IntegerParser is not null);
        var changed=damage switch {
            "mode"=>original with {ThrowingIntegerParser=false},
            "missing"=>original with {IntegerParser=null},
            "code"=>original with {IntegerParser=original.IntegerParser! with {Code=PrimitiveTypeCode.UInt64}},
            "source"=>original with {IntegerParser=original.IntegerParser! with {Source=original.IntegerParser.Source with {Name="System.UInt64"}}},
            _=>original with {Kind=ObjectMethodKind.Instance}
        };
        var bindings=objects.Bindings.Replace(original,changed);
        Assert.Throws<ArgumentException>(()=>ManagedExceptionIrValidator.Validate(program with {Flow=program.Flow with {Bindings=bindings}}));
        Assert.Throws<ArgumentException>(()=>ScalarManagedExceptionIrValidator.Validate(scalar with {Flow=scalar.Flow with {Flow=scalar.Flow.Flow with {Flow=scalar.Flow.Flow.Flow with {Flow=objects with {Bindings=bindings}}}}}));
    }
    [Fact]
    public async Task Both_stages_reject_unproven_parser_operands()
    {
        var program=(await Programs.Value)[0];var scalar=ValueScalarLowering.LowerExceptions(program);var objects=scalar.Flow.Flow.Flow.Flow;
        IrStatement Corrupt(IrStatement statement) => statement is IrStatement.Define d ? d.Value.Expression switch {
            IrExpression.InvariantCulture => d with {Value=d.Value with {Type=IrType.NullReference,Expression=new IrExpression.Null()}},
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
        var artifact=Assert.Single(manifest.Artifacts);Assert.Single(artifact.Grants.Where(g=>g.Capability==LibraryIntrinsicCapability.InvariantIntegerParsing && g.Owner=="System.Int64" && g.Member=="Parse"));
        var narrowed=manifest with {Artifacts=[artifact with {Grants=[..artifact.Grants.Where(g=>g.Capability!=LibraryIntrinsicCapability.InvariantIntegerParsing || g.Owner!="System.Int64" || g.Member!="Parse")]}]};
        var deniedBytes=AssemblyResolver.SerializeLibraryBuildManifest(narrowed);var input=resolver.OpenInput(typeof(ThrowingInt64ParseProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name)=>(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ThrowingInt64ParseProbe).GetMethod(name)!.MetadataToken);
        using(var denied=resolver.CreateTrustedLibraryCompilationScope(plan,deniedBytes,Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!) {
            var result=ValueTranslator.TranslateIntegerObjects(denied,input,Method("Catalog"),implementation);
            Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.LibraryIntrinsicDenied);
        }
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,bytes,built.ManifestSha256).Value!;
        foreach(var name in new[]{"UnsupportedStyle","UnsupportedProvider","UnsupportedDefault","UnsupportedDynamicStyle","UnsupportedProviderAlias"}) {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,Method(name),implementation);Assert.False(result.Success,name);Assert.NotEmpty(result.Diagnostics);
        }
    }
}
