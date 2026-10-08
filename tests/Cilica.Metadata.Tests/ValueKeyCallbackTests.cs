using System;
using System.IO;
using System.Linq;
using System.Reflection;
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

public sealed class ValueKeyCallbackTests
{
    private static readonly string[] Names = ["Direct", "PublicSet", "InternalSet", "InternalLookup", "PrivateSet", "ExplicitSet", "GenericHash", "BorrowedSet", "BorrowedDictionary", "HashFault", "GenericSet"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(HiddenValueKeyProbe), Names));

    [Fact]
    public async Task Selected_callbacks_validate_in_both_representations()
    {
        var programs = await Programs.Value;
        for (int i = 0; i < programs.Length; ++i)
        {
            ManagedExceptionIrValidator.Validate(programs[i]);
            ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(programs[i]));
            var calls = programs[i].Flow.Flow.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values).Select(v => v.Expression).OfType<IrExpression.Call>().Where(c => c.ValueKeyContract is not null);
            if (Names[i] is "Direct" or "PublicSet" or "GenericHash") Assert.Empty(calls);
            else Assert.NotEmpty(calls);
        }
    }
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    [InlineData(false, 4)] [InlineData(true, 4)] [InlineData(false, 5)] [InlineData(true, 5)]
    public async Task Hidden_keys_borrows_faults_and_cleanup_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(HiddenValueKeyProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group * 2).Take(2).ToArray());
    }

    [Theory]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 5)] [InlineData(true, 5)]
    public async Task Missing_or_forged_contracts_are_rejected(bool lowered, int index)
    {
        var program = (await Programs.Value)[index];
        var flow = program.Flow.Flow.Flow;
        var logical = flow.Methods.First(m => m.Blocks.SelectMany(b => b.Values).Any(v => v.Expression is IrExpression.Call { ValueKeyContract: not null }));
        var scalar = ValueScalarLowering.LowerExceptions(program);
        var objects = scalar.Flow.Flow.Flow.Flow;
        var method = lowered ? objects.Flow.Methods.Single(m => m.Source == logical.Source) : logical;
        var block = method.Blocks.First(b => b.Values.Any(v => v.Expression is IrExpression.Call { ValueKeyContract: not null }));
        var statement = block.Statements.OfType<IrStatement.Define>().First(s => s.Value.Expression is IrExpression.Call { ValueKeyContract: not null });
        var call = (IrExpression.Call)statement.Value.Expression;
        var proof = call.ValueKeyContract!;
        var mutations = new[] {
            call with { ValueKeyContract = null },
            call with { ValueKeyContract = proof with { Contract = proof.Declaration.Owner } },
            call with { ValueKeyContract = proof with { IsExplicit = !proof.IsExplicit } },
            call with { ValueKeyContract = proof with { Declaration = proof.Declaration with { Method = IrMethodId.From(method.Source) } } },
            call with { ValueKeyContract = proof with { Declaration = proof.Declaration with { OwnerExternallyVisible = !proof.Declaration.OwnerExternallyVisible } } },
            call with { ValueKeyContract = proof with { Declaration = proof.Declaration with { Attributes = MethodAttributes.Public } } },
            call with { NullCheckReceiver = true },
            call with { VirtualDispatch = true },
            call with { Arguments = [] }
        };
        foreach (var changed in mutations.Append(call))
        {
            var edited = method with { Blocks = method.Blocks.Replace(block, block with { Statements = block.Statements.Replace(statement,
                statement with { Value = statement.Value with { Expression = changed }, Exceptional = ReferenceEquals(changed, call) ? null : statement.Exceptional }) }) };
            if (lowered)
            {
                var changedObjects = objects with { Flow = objects.Flow with { Methods = objects.Flow.Methods.Replace(method, edited) } };
                var invalid = scalar with { Flow = scalar.Flow with { Flow = scalar.Flow.Flow with { Flow = new(changedObjects) } } };
                Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalid));
            }
            else
            {
                var invalid = program with { Flow = program.Flow with { Flow = new(flow with { Methods = flow.Methods.Replace(method, edited) }) } };
                Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid));
            }
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Application_calls_cannot_claim_core_callback_privileges(bool lowered)
    {
        var programs = await Programs.Value;
        var proof = programs[2].Flow.Flow.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values)
            .Select(v => v.Expression).OfType<IrExpression.Call>().Select(c => c.ValueKeyContract)
            .First(p => p is not null && p.Contract == programs[2].Flow.Flow.Flow.Types.References.ObjectBase)!;
        var program = programs[0];
        var flow = program.Flow.Flow.Flow;
        var scalar = ValueScalarLowering.LowerExceptions(program);
        var objects = scalar.Flow.Flow.Flow.Flow;
        var methods = lowered ? objects.Flow.Methods : flow.Methods;
        var method = methods.First(m => m.Blocks.SelectMany(b => b.Values).Any(v => v.Expression is IrExpression.Call c && c.Target == proof.Declaration.Method));
        var block = method.Blocks.First(b => b.Values.Any(v => v.Expression is IrExpression.Call c && c.Target == proof.Declaration.Method));
        var statement = block.Statements.OfType<IrStatement.Define>().First(s => s.Value.Expression is IrExpression.Call c && c.Target == proof.Declaration.Method);
        var call = (IrExpression.Call)statement.Value.Expression;
        var edited = method with { Blocks = method.Blocks.Replace(block, block with { Statements = block.Statements.Replace(statement,
            statement with { Value = statement.Value with { Expression = call with { ValueKeyContract = proof } } }) }) };
        if (lowered)
        {
            var changedObjects = objects with { Flow = objects.Flow with { Methods = methods.Replace(method, edited) } };
            var invalid = scalar with { Flow = scalar.Flow with { Flow = scalar.Flow.Flow with { Flow = new(changedObjects) } } };
            Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalid));
        }
        else
        {
            var invalid = program with { Flow = program.Flow with { Flow = new(flow with { Methods = methods.Replace(method, edited) }) } };
            Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid));
        }
    }
    [Theory]
    [InlineData(LibraryIntrinsicCapability.ClosedKeyEqual)] [InlineData(LibraryIntrinsicCapability.ClosedKeyHash)]
    public async Task Exact_grants_remain_required(LibraryIntrinsicCapability capability)
    {
        using var files = new ResolverFiles(); var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime = resolver;
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), new JsonSerializerOptions { Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var bytes = AssemblyResolver.SerializeLibraryBuildManifest(manifest with { Artifacts = [artifact with { Grants = [..artifact.Grants.Where(g => g.Capability != capability)] }] });
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, Convert.ToHexStringLower(SHA256.HashData(bytes))).Value!;
        var input = scope.OpenInput(typeof(HiddenValueKeyProbe).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(HiddenValueKeyProbe).GetMethod("InternalSet")!.MetadataToken), implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
    }
    [Theory]
    [InlineData("InternalDictionary", "boxing")] [InlineData("UnsupportedPrivateGeneric", "Calls require")]
    public async Task Independent_key_text_and_private_generic_construction_remain_diagnostic(string name, string diagnostic)
    {
        using var files = new ResolverFiles(); var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(HiddenValueKeyProbe).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(HiddenValueKeyProbe).GetMethod(name)!.MetadataToken), implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains(diagnostic, StringComparison.Ordinal));
    }
}
