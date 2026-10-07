using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class CharacterStringTests
{
    private static readonly string[] Names = ["Copy", "Independent", "Identity", "Empty", "Repeated", "Nested"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(CharacterStringProbe), Names));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Character_constructor_preserves_code_units_identity_and_independence(bool optimized)
    {
        var programs=await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name,i)=>(name,programs[i],typeof(CharacterStringProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).ToArray(),
            pairs: (from a in new[]{int.MinValue,-1,0,1,255,256,55296,56320,65535,65536,int.MaxValue} from b in new[]{-1,0,1,65535,int.MaxValue} select (a,b)).ToArray());
    }

    [Fact]
    public async Task Constructor_factory_requires_exact_grants_call_form_and_source()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions {
            Converters = {new JsonStringEnumConverter<LibraryIntrinsicCapability>()} })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var input = resolver.OpenInput(typeof(CharacterStringProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name) => (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CharacterStringProbe).GetMethod(name)!.MetadataToken);
        foreach(var capability in new[] {LibraryIntrinsicCapability.StringCharacterConstruction, LibraryIntrinsicCapability.StringFromCodeUnits})
        {
            var deniedBytes=AssemblyResolver.SerializeLibraryBuildManifest(manifest with { Artifacts=[artifact with {Grants=[..artifact.Grants.Where(g=>g.Capability!=capability)]}]});
            using var denied=resolver.CreateTrustedLibraryCompilationScope(plan,deniedBytes,Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
            var result=ValueTranslator.TranslateIntegerObjects(denied,input,Method("Copy"),implementation);
            Assert.False(result.Success); Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.LibraryIntrinsicDenied);
        }
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,bytes,built.ManifestSha256).Value!;
        var section=ValueTranslator.TranslateIntegerObjects(scope,input,Method("UnsupportedSection"),implementation);
        Assert.False(section.Success); Assert.Contains(section.Diagnostics,d=>d.Message.Contains("No implementation method matches",StringComparison.Ordinal));
        foreach(bool wrongSource in new[]{false,true})
        {
            var builder=new ResolutionAssemblyBuilder(new("CharacterConstructorCaller"+wrongSource,new Version(1,0,0,0)));
            var core=builder.Reference(implementation.Identity);
            var text=builder.TypeReference("System","String",core);
            var byteType=builder.TypeReference("System","Byte",core);
            var constructor=builder.Member(text,".ctor",0x20,1,1,0x1d,3);
            builder.Type("Test","Caller");
            var root=builder.Method("Run",[0,0,8],emit:il=>{
                if(wrongSource){il.LoadConstantI4(1);il.OpCode(ILOpCode.Newarr);il.Token(byteType);il.OpCode(ILOpCode.Newobj);il.Token(constructor);il.OpCode(ILOpCode.Pop);}
                else {il.OpCode(ILOpCode.Ldnull);il.OpCode(ILOpCode.Ldnull);il.Call(constructor);}
                il.LoadConstantI4(0);il.OpCode(ILOpCode.Ret);
            });
            var caller=scope.OpenInput(files.Write("caller"+wrongSource+".dll",builder.Build())).Value!;
            var result=ValueTranslator.TranslateIntegerObjects(scope,caller,root,implementation);
            Assert.False(result.Success); Assert.Contains(result.Diagnostics,d=>d.Message.Contains(wrongSource
                ? "Character-string constructor requires the exact Char[] source" : "Character-string construction requires the exact selected Char[] newobj declaration",StringComparison.Ordinal));
        }
    }
    [Theory]
    [InlineData("arity")] [InlineData("operand")] [InlineData("scalar")] [InlineData("fault")]
    public async Task Character_factory_calls_remain_typed_at_both_detached_stages(string mutation)
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        var target = IrMethodId.From(Assert.Single(flow.Methods.Where(m => m.Source.Name.Contains("::CopyCharacters", StringComparison.Ordinal))).Source);
        IrMethod Change(IrMethod method) => method with { Blocks = [.. method.Blocks.Select(block => block with { Statements = [.. block.Statements.Select(statement =>
            statement is IrStatement.Define { Value.Expression: IrExpression.Call call } define && call.Target == target
                ? define with { Value = define.Value with { Expression = call with { Arguments = mutation switch {
                    "arity" => [], "operand" => [-1], "scalar" => [block.Values.First(v => v.Type == IrType.Int32).Id], _ => call.Arguments } } },
                    Exceptional = mutation == "fault" ? null : define.Exceptional } : statement)] })] };
        Assert.Contains(flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values), v => v.Expression is IrExpression.Call call && call.Target == target);
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Methods = [.. flow.Methods.Select(Change)] } } } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program); var scalarFlow = scalar.Flow.Flow.Flow.Flow.Flow;
        var invalidScalar = scalar with { Flow = scalar.Flow with { Flow = scalar.Flow.Flow with { Flow = scalar.Flow.Flow.Flow with { Flow = scalar.Flow.Flow.Flow.Flow with { Flow = scalarFlow with { Methods = [.. scalarFlow.Methods.Select(Change)] } } } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar));
    }
}
