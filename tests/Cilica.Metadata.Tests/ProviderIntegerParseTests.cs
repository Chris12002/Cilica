using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
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
public sealed class ProviderIntegerParseTests
{
    private static readonly string[] Names=["Catalog","Precedence","Roundtrip","CodeUnits"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(ProviderIntegerParseProbe),Names));
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Throwing_grammar_fault_precedence_and_messages_match_dotnet(bool optimized)
    {
        var programs=await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("Catalog",programs[0],ProviderIntegerParseProbe.Catalog)],(from a in Enumerable.Range(0,64) from b in new[]{0,1,2} select(a,b)).ToArray());
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("Precedence",programs[1],ProviderIntegerParseProbe.Precedence)],(from a in Enumerable.Range(0,32) from b in new[]{0,1,2} select(a,b)).ToArray());
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("Roundtrip",programs[2],ProviderIntegerParseProbe.Roundtrip)]);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("CodeUnits",programs[3],ProviderIntegerParseProbe.CodeUnits)],(from a in new[]{0,9,10,13,32,48,57,65,133,160,0xd800,0xdc00,0xffff,-1,int.MaxValue} from b in new[]{0,1} select(a,b)).ToArray());
    }
    [Fact]
    public void Owned_throwing_parser_matches_reduced_regressions_and_seeded_numbers()
    {
        (int Value,string Kind,string Message,string Parameter) Observe(Func<int> parse) {
            try{return(parse(),"","","");}catch(Exception error){return(0,error.GetType().FullName!,error.Message,(error as ArgumentException)?.ParamName??"");}
        }
        void Check(string? text)=>Assert.Equal(Observe(()=>int.Parse(text!,CultureInfo.InvariantCulture)),Observe(()=>Library.IntegerParsing.ParseInt32Integer(text!)));
        for(int i=0;i<64;i++)Check(IntegerParseProbe.Input(i));
        for(int i=0;i<32;i++)Check(ProviderIntegerParseProbe.SpecialInput(i));
        for(int i=0;i<=char.MaxValue;i++) {
            string c=((char)i).ToString();Check(c+"17");Check("17"+c);Check(c+"2147483648");Check("2147483648"+c);
        }
        Check("-2147483648");Check("2147483647");
        var random=new Random(9182);for(int i=0;i<512;i++)Check(random.NextInt64(-3000000000,3000000001).ToString(CultureInfo.InvariantCulture));
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
            "code"=>original with {IntegerParser=original.IntegerParser! with {Code=PrimitiveTypeCode.UInt32}},
            "source"=>original with {IntegerParser=original.IntegerParser! with {Source=original.IntegerParser.Source with {Name="System.UInt32"}}},
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
        using var image=new PEReader(File.OpenRead(built.AssemblyPath));var reader=image.GetMetadataReader();
        int ParameterCount(LibraryIntrinsicGrant grant) {
            var signature=reader.GetBlobReader(reader.GetMethodDefinition((MethodDefinitionHandle)MetadataTokens.EntityHandle(grant.Token)).Signature);
            Assert.False(signature.ReadSignatureHeader().IsGeneric);return signature.ReadCompressedInteger();
        }
        var artifact=Assert.Single(manifest.Artifacts);var selected=Assert.Single(artifact.Grants.Where(g=>g.Capability==LibraryIntrinsicCapability.InvariantIntegerParsing && g.Owner=="System.Int32" && g.Member=="Parse" && ParameterCount(g)==2));
        var narrowed=manifest with {Artifacts=[artifact with {Grants=[..artifact.Grants.Where(g=>g.Token!=selected.Token)]}]};
        var deniedBytes=AssemblyResolver.SerializeLibraryBuildManifest(narrowed);var input=resolver.OpenInput(typeof(ProviderIntegerParseProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name)=>(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ProviderIntegerParseProbe).GetMethod(name)!.MetadataToken);
        using(var denied=resolver.CreateTrustedLibraryCompilationScope(plan,deniedBytes,Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!) {
            var result=ValueTranslator.TranslateIntegerObjects(denied,input,Method("Catalog"),implementation);
            Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.LibraryIntrinsicDenied);
            var existing=(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ThrowingIntegerParseProbe).GetMethod("Catalog")!.MetadataToken);
            Assert.True(ValueTranslator.TranslateIntegerObjects(denied,input,existing,implementation).Success);
        }
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,bytes,built.ManifestSha256).Value!;
        foreach(var name in new[]{"UnsupportedStyle","UnsupportedProvider","UnsupportedDefault","UnsupportedDynamicStyle","UnsupportedProviderAlias","UnsupportedInt64IntegerStyle"}) {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,Method(name),implementation);Assert.False(result.Success,name);Assert.NotEmpty(result.Diagnostics);
        }
    }
}
