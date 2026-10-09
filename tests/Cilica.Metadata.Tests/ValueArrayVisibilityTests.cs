using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ValueArrayVisibilityTests
{
    private static readonly string[] Names = ["Internal", "Private", "Nested", "Readonly", "Empty", "Bounds"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ValueArrayVisibilityProbe), Names));
    [Theory] [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    public async Task Visible_value_arrays_preserve_nested_copies_and_owners(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name,i) =>
            (name,programs[i],typeof(ValueArrayVisibilityProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*3).Take(3).ToArray());
    }
    [Theory] [InlineData("missing")] [InlineData("default")] [InlineData("visibility")]
    [InlineData("public")] [InlineData("token")] [InlineData("parent")]
    public async Task Logical_and_scalar_value_array_access_requires_consistent_declaring_scopes(string mutation)
    {
        var program = (await Programs.Value)[1]; var flow = program.Flow.Flow.Flow;
        var value = flow.Types.Types.Single(t => t.Source.Name.EndsWith("/Secret", StringComparison.Ordinal));
        Assert.Equal(2, value.LexicalOwners.Length);
        var chain = value.LexicalOwners;
        var changed = value with {LexicalOwners = mutation switch {
            "missing" => [], "default" => default,
            "visibility" => chain.SetItem(0,chain[0] with {Visibility = TypeAttributes.NestedPublic}),
            "token" => chain.SetItem(0,chain[0] with {Token = chain[0].Token + 1}),
            "parent" => chain.SetItem(1,chain[1] with {Visibility = TypeAttributes.NotPublic}),
            _ => chain
        }, ExternallyVisible = mutation == "public" || value.ExternallyVisible};
        var values = flow.Types with {Types = flow.Types.Types.Replace(value,changed)};
        var invalid = program with {Flow = program.Flow with {Flow = new(flow with {Types = values})}};
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program);
        var invalidScalar = scalar with {Flow = scalar.Flow with {Flow = scalar.Flow.Flow with {Values = values}}};
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar));
    }
}
