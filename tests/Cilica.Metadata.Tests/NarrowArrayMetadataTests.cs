using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class NarrowArrayMetadataTests
{
    private static readonly string[] Names = ["Count", "GenericCount", "Flags", "Identity", "Null"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(NarrowArrayMetadataProbe), Names));
    [Fact]
    public async Task Narrow_metadata_helpers_keep_exact_byte_and_character_views() => Assert.Equal(Names.Length, (await Programs.Value).Length);
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)]
    public async Task Native_metadata_operations_preserve_length_flags_identity_and_nulls(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name,i) =>
            (name, programs[i], typeof(NarrowArrayMetadataProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(2).ToArray());
    }
    [Fact]
    public async Task Element_operations_and_signed_aliases_remain_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await System.IO.File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(NarrowArrayMetadataProbe).Assembly.Location).Value!;
        foreach (var name in new[] { "UnsupportedGet", "UnsupportedCopy", "UnsupportedEnumeration", "UnsupportedClear", "UnsupportedSignedView" })
        {
            var result = ValueTranslator.TranslateIntegerObjects(scope, input,
                (System.Reflection.Metadata.MethodDefinitionHandle)System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(typeof(NarrowArrayMetadataProbe).GetMethod(name)!.MetadataToken), implementation);
            Assert.False(result.Success, name); Assert.Contains(result.Diagnostics, d => d.Message.Contains("Narrow", StringComparison.Ordinal) || d.Message.Contains("narrow", StringComparison.Ordinal));
        }
    }
    [Fact]
    public async Task Detached_narrow_helpers_cannot_claim_element_access_or_another_width()
    {
        var program = (await Programs.Value)[0];
        foreach (var plan in new[] { program.Flow.InterfaceDispatch!, ValueScalarLowering.LowerExceptions(program).Flow.Flow.Flow.Flow.InterfaceDispatch! })
        {
            var helper = plan.Methods.First(m => m.ArrayHelper && m.Parameters[0].Class?.ArrayElement?.Kind == ObjectArrayElementKind.Byte);
            foreach (var changed in new[] { helper with { Name = "Get", Parameters = helper.Parameters.Add(IrType.Int32) },
                helper with { Parameters = [IrType.Reference(plan.Types.Arrays.First(a => a.ArrayElement!.Kind == ObjectArrayElementKind.Char))] } })
                Assert.Throws<ArgumentException>(() => ObjectInterfacePlanValidator.Validate(plan with { Methods = plan.Methods.Replace(helper, changed) }));
        }
    }
    [Fact]
    public async Task Detached_byte_count_cannot_substitute_a_signed_generic_contract()
    {
        var program = (await Programs.Value)[1];
        foreach (var plan in new[] { program.Flow.InterfaceDispatch!, ValueScalarLowering.LowerExceptions(program).Flow.Flow.Flow.Flow.InterfaceDispatch! })
        {
            string signed = Cilica.Metadata.Resolution.ClosedGenericKey.Create(new([new Cilica.Metadata.Resolution.TypeSignature.Primitive(System.Reflection.Metadata.PrimitiveTypeCode.SByte)]), Cilica.Metadata.Resolution.SignatureList<Cilica.Metadata.Resolution.TypeSignature>.Empty);
            var contract = plan.Types.Interfaces.Single(c => c.ArrayContract == ObjectArrayInterfaceKind.Collection && c.Arguments.Single().Identity == signed);
            var map = plan.Dispatch.Single(m => m.Targets.Any(t => t.Receiver.ArrayElement?.Kind == ObjectArrayElementKind.Byte));
            var declaration = plan.Declarations.Single(d => IrMethodId.From(d.Source) == map.Declaration);
            var owner = ObjectTypeId.From(contract.Source);
            var forged = declaration with { Owner = owner, Source = declaration.Source with { Specialization = owner.Specialization }, Parameters = [IrType.Reference(owner)] };
            var bad = plan with { Declarations = plan.Declarations.Replace(declaration, forged),
                Dispatch = plan.Dispatch.Replace(map, map with { Declaration = IrMethodId.From(forged.Source) }) };
            var failure = Assert.Throws<ArgumentException>(() => ObjectInterfacePlanValidator.Validate(bad));
            Assert.Contains("Narrow array targets", failure.Message, StringComparison.Ordinal);
        }
    }

}
