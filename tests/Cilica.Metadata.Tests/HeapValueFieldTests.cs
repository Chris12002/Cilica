using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class HeapValueFieldTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Fixture = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(HeapValueProbe), "Copies", "Aliases", "EmptyAndNull", "FaultOrder", "CyclesAndDispatch"));

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Heap_value_copies_aliases_roots_and_faults_match_dotnet(bool optimized)
    {
        var programs = await Fixture.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [
            ("Copies", programs[0], HeapValueProbe.Copies), ("Aliases", programs[1], HeapValueProbe.Aliases),
            ("EmptyAndNull", programs[2], HeapValueProbe.EmptyAndNull), ("FaultOrder", programs[3], HeapValueProbe.FaultOrder),
            ("CyclesAndDispatch", programs[4], HeapValueProbe.CyclesAndDispatch)]);
    }

    [Fact]
    public async Task Compound_struct_return_stores_remain_diagnostic_until_destination_order_is_lowered()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await System.IO.File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(HeapValueProbe).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input,
            (System.Reflection.Metadata.MethodDefinitionHandle)System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(typeof(HeapValueProbe).GetMethod("CompoundStore")!.MetadataToken), implementation);
        Assert.False(result.Success); Assert.Null(result.Program);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("destination/exception-order", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Expanded_fields_retain_original_identity_and_detached_catalog_evidence()
    {
        var program = (await Fixture.Value)[0];
        var logical = program.Flow.Flow.Flow.Types;
        Assert.Contains(logical.References.Types.SelectMany(t => t.Fields), f => f.Storage is ObjectStorage.Value);
        var owner = logical.References.Types.First(t => t.Fields.Any(f => f.Storage is ObjectStorage.Value));
        var inline = owner.Fields.First(f => f.Storage is ObjectStorage.Value);
        var forged = owner with { Fields = owner.Fields.Replace(inline, inline with { Storage = new ObjectStorage.Value(logical.ValueBase) }) };
        Assert.Throws<ArgumentException>(() => ValueLayoutPlanner.Build(logical with {
            References = logical.References with { Types = logical.References.Types.Replace(owner, forged) }
        }));
        var scalar = ValueScalarLowering.LowerExceptions(program);
        var flow = scalar.Flow.Flow.Flow.Flow.Flow;
        Assert.DoesNotContain(flow.Types.Types.SelectMany(t => t.Fields), f => f.Storage is ObjectStorage.Value);
        Assert.Contains(flow.Types.Types.SelectMany(t => t.Fields), f => f.Id.Component > 0 && f.Storage is ObjectStorage.Reference);
        Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(logical.References));
        var layout = flow.Types.Types.First(t => t.Fields.Any(f => f.Id.Component == 0));
        var field = layout.Fields.First(f => f.Id.Component == 0);
        var bad = layout with { Fields = layout.Fields.Replace(field, field with { Id = field.Id with { Component = 3 } }) };
        Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(flow.Types with { Types = flow.Types.Types.Replace(layout, bad) }));
    }
}
