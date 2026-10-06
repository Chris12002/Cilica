using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class GenericDelegateTests
{
    [Theory]
    [InlineData("Return", true)] [InlineData("ExactTypes", false)]
    public void Explicit_delegate_profile_accepts_closed_calls_and_retains_its_cast_limit(string name, bool supported)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(GenericDelegateProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var root = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(GenericDelegateProbe).GetMethod(name)!.MetadataToken);
        var result = ValueTranslator.TranslateDelegates(resolver, input, root, library);
        Assert.Equal(supported, result.Success);
        if (supported) ScalarDelegateIrValidator.Validate(ValueScalarLowering.LowerDelegates(result.Program!));
        else Assert.Contains(result.Diagnostics, d => d.Message.Contains("Instruction 'isinst'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Detached_ir_keeps_same_shape_specializations_and_cast_visibility_distinct()
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(GenericDelegateProbe), "ExactTypes"))[0];
        var flow = program.Flow.Flow.Flow;
        var entry = flow.Methods.Single(m => IrMethodId.From(m.Source) == flow.Entry);
        var call = entry.Blocks.SelectMany(b => b.Values).Select(v => v.Expression).OfType<IrExpression.CallDelegate>().First();
        var other = flow.Types.References.Delegates.Select(d => ObjectTypeId.From(d.Source)).Single(id => id != call.Type);
        Assert.Equal(call.Type.ContentHash, other.ContentHash);
        Assert.Equal(call.Type.Token, other.Token);
        Assert.NotEqual(call.Type.Specialization, other.Specialization);
        var changed = entry with { Blocks = [.. entry.Blocks.Select(b => b with { Statements = [.. b.Statements.Select(s =>
            s is IrStatement.Define d && d.Value.Expression == call
                ? d with { Value = d.Value with { Expression = call with { Type = other } } } : s)] })] };
        var wrongCall = program with { Flow = program.Flow with { Flow = new(flow with { Methods = flow.Methods.Replace(entry, changed) }) } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(wrongCall));
        var inaccessible = flow.Types.References.Delegates.Select(d => d with { ExternallyVisible = false }).ToImmutableArray();
        var hidden = program with { Flow = program.Flow with { Flow = new(flow with {
            Types = flow.Types with { References = flow.Types.References with { Delegates = inaccessible } } }) } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(hidden));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Closed_delegates_and_mapped_actions_match_dotnet(bool optimized)
    {
        var probes = new (string Name, Func<int, int, int> Oracle)[] {
            (nameof(GenericDelegateProbe.ReferenceParameter), GenericDelegateProbe.ReferenceParameter),
            (nameof(GenericDelegateProbe.Capture), GenericDelegateProbe.Capture),
            (nameof(GenericDelegateProbe.Static), GenericDelegateProbe.Static),
            (nameof(GenericDelegateProbe.Return), GenericDelegateProbe.Return),
            (nameof(GenericDelegateProbe.ExactTypes), GenericDelegateProbe.ExactTypes),
            (nameof(GenericDelegateProbe.Null), GenericDelegateProbe.Null),
            (nameof(GenericDelegateProbe.InvalidCast), GenericDelegateProbe.InvalidCast) };
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(GenericDelegateProbe), probes.Select(p => p.Name).ToArray());
        foreach (var program in programs)
        {
            var delegates = program.Flow.Flow.Flow.Types.References.Delegates;
            Assert.NotEmpty(delegates);
            Assert.All(delegates, d => Assert.NotEmpty(d.Source.Specialization));
        }
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            probes.Select((p, i) => (p.Name, programs[i], p.Oracle)).ToArray());
    }

    [Fact]
    public async Task Unimplemented_closed_delegate_storage_is_rejected()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var resolverLifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(GenericDelegateProbe).Assembly.Location).Value!;
        foreach (string name in new[] { "UnsupportedWide" })
        {
            var result = ValueTranslator.TranslateIntegerObjects(scope, input,
                (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(GenericDelegateProbe).GetMethod(name)!.MetadataToken), implementation);
            Assert.False(result.Success); Assert.Null(result.Program);
            Assert.Contains(result.Diagnostics, d => d.Message.Contains("direct wide/narrow parameters/results and byref signatures require later lowering.", StringComparison.Ordinal));
        }
    }
}
