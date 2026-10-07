using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Linq;
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
public sealed class OrdinalPrefixTests
{
    private static readonly string[] Names=["Compare","Units","Identity","Empty","Fault","Composed","Literals"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(OrdinalPrefixProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)]
    public async Task Ordinal_prefix_preserves_utf16_identity_null_and_empty_behavior(bool optimized,int group)
    {
        var programs=await Programs.Value;
        int[] units=[0,1,65,127,255,0x7fff,0x8000,0xd7ff,0xd800,0xdbff,0xdc00,0xdfff,0xe000,0xfffe,0xffff];
        var probes=Names.Select((name,i)=>(name,programs[i],typeof(OrdinalPrefixProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>()));
        var pairs=group==0?(from a in Enumerable.Range(0,20) from b in Enumerable.Range(0,20) select(a,b)).ToArray()
            :group==1?(from a in units from b in units select(a,b)).ToArray():null;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,(group<2?probes.Skip(group).Take(1):probes.Skip(2)).ToArray(),pairs);
    }
    [Fact]
    public async Task Enum_contract_exact_grant_and_constant_proof_are_required()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(OrdinalPrefixProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name) => (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(OrdinalPrefixProbe).GetMethod(name)!.MetadataToken);
        foreach (string name in new[] { "IgnoreCase", "Culture", "Unknown", "Alias" })
        {
            var result = ValueTranslator.TranslateIntegerObjects(scope, input, Method(name), implementation);
            Assert.False(result.Success); Assert.Null(result.Program);
            Assert.Contains(result.Diagnostics, d => d.Message.Contains("directly proven StringComparison.Ordinal", StringComparison.Ordinal));
        }
        var culture = ValueTranslator.TranslateIntegerObjects(scope, input, Method("DefaultCulture"), implementation);
        Assert.False(culture.Success); Assert.Contains(culture.Diagnostics,d=>d.Message.Contains("No implementation method matches",StringComparison.Ordinal));
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions {
            Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var narrowed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != LibraryIntrinsicCapability.StringOrdinalPrefix)] }] };
        var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
        using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
        var rejected = ValueTranslator.TranslateIntegerObjects(denied, input, Method("Compare"), implementation);
        Assert.False(rejected.Success); Assert.Contains(rejected.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        using var image = new PEReader(File.OpenRead(built.AssemblyPath));
        var reader = image.GetMetadataReader();
        var type = resolver.ResolveType(implementation, "System", "StringComparison").Value!;
        var constants = reader.GetTypeDefinition(type.Handle).GetFields().Select(reader.GetFieldDefinition)
            .Where(f => !f.GetDefaultValue().IsNil).ToDictionary(f => reader.GetString(f.Name), f =>
                reader.GetBlobReader(reader.GetConstant(f.GetDefaultValue()).Value).ReadInt32());
        Assert.Equal(6, constants.Count);
        foreach (var value in Enum.GetValues<StringComparison>()) Assert.Equal((int)value, constants[value.ToString()]);
    }
    [Fact]
    public async Task Direct_calls_reject_before_jit_specific_null_receiver_behavior()
    {
        using var files=new ResolverFiles();
        var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        foreach(string? prefix in new string?[]{null,"","a"})
        {
            var b=new ResolutionAssemblyBuilder(new("DirectPrefix"+(prefix is null?0:prefix.Length+1),new Version(1,0,0,0)));
            var core=b.Reference(implementation.Identity);var text=b.TypeReference("System","String",core);
            var comparison=b.TypeReference("System","StringComparison",core);
            var nullFault=b.TypeReference("System","NullReferenceException",core);var argFault=b.TypeReference("System","ArgumentNullException",core);
            var call=b.Member(text,"StartsWith",0x20,2,2,0x0e,0x11,(byte)((MetadataTokens.GetRowNumber(comparison)<<2)|1));
            b.Type("Test","Caller");
            var root=b.Method("Run",[0,2,8,8,8],locals:[7,1,8],emit:body=>{
                var start=body.DefineLabel();var argHandler=body.DefineLabel();var nullHandler=body.DefineLabel();var done=body.DefineLabel();
                body.MarkLabel(start);body.OpCode(ILOpCode.Ldnull);
                if(prefix is null)body.OpCode(ILOpCode.Ldnull);else body.LoadString(b.String(prefix));
                body.LoadConstantI4(4);body.Call(call);body.StoreLocal(0);body.Branch(ILOpCode.Leave,done);
                body.MarkLabel(argHandler);body.OpCode(ILOpCode.Pop);body.LoadConstantI4(-3);body.StoreLocal(0);body.Branch(ILOpCode.Leave,done);
                body.MarkLabel(nullHandler);body.OpCode(ILOpCode.Pop);body.LoadConstantI4(-7);body.StoreLocal(0);body.Branch(ILOpCode.Leave,done);
                body.MarkLabel(done);body.LoadLocal(0);body.OpCode(ILOpCode.Ret);
                body.ControlFlowBuilder!.AddCatchRegion(start,argHandler,argHandler,nullHandler,argFault);
                body.ControlFlowBuilder.AddCatchRegion(start,argHandler,nullHandler,done,nullFault);
            });
            var input=scope.OpenInput(files.Write("caller"+(prefix is null?0:prefix.Length+1)+".dll",b.Build())).Value!;
            var translated=ValueTranslator.TranslateIntegerObjects(scope,input,root,implementation);
            Assert.False(translated.Success);
            Assert.Contains(translated.Diagnostics,d=>d.Message.Contains("require a proven non-null literal receiver",StringComparison.Ordinal));
        }
    }
    [Theory]
    [InlineData("arity")] [InlineData("operand")] [InlineData("scalar")] [InlineData("fault")]
    public async Task Prefix_helper_calls_remain_typed_at_both_detached_stages(string mutation)
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        var target = IrMethodId.From(Assert.Single(flow.Methods.Where(m => m.Source.Name.Contains("::StartsWithOrdinal", StringComparison.Ordinal))).Source);
        IrMethod Change(IrMethod method) => method with { Blocks = [.. method.Blocks.Select(block => block with { Statements = [.. block.Statements.Select(statement =>
            statement is IrStatement.Define { Value.Expression: IrExpression.Call call } define && call.Target == target
                ? define with { Value = define.Value with { Expression = call with { Arguments = mutation switch {
                    "arity" => [], "operand" => [-1, call.Arguments[1]], "scalar" => [block.Values.First(v => v.Type == IrType.Int32).Id, call.Arguments[1]], _ => call.Arguments } } },
                    Exceptional = mutation == "fault" ? null : define.Exceptional } : statement)] })] };
        Assert.Contains(flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values), v => v.Expression is IrExpression.Call call && call.Target == target);
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Methods = [.. flow.Methods.Select(Change)] } } } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program); var scalarFlow = scalar.Flow.Flow.Flow.Flow.Flow;
        var invalidScalar = scalar with { Flow = scalar.Flow with { Flow = scalar.Flow.Flow with { Flow = scalar.Flow.Flow.Flow with { Flow = scalar.Flow.Flow.Flow.Flow with { Flow = scalarFlow with { Methods = [.. scalarFlow.Methods.Select(Change)] } } } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar));
    }
}
