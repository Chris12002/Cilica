using System;
using System.Linq;
using System.Collections.Immutable;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class InterfaceLexicalFactsTests
{
    private static ManagedExceptionIrProgram Program()
    {
        var result = IntegerObjectTranslationTests.Translate("Rooted", fixture: typeof(ComposedStringProbe));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        return result.Program!;
    }
    private static ImmutableArray<LexicalTypeOwner> Copy(ImmutableArray<LexicalTypeOwner> chain) =>
        [.. chain.Select(o => new LexicalTypeOwner(o.Token, o.Visibility, o.GenericArity))];
    [Fact]
    public void Independent_lexical_snapshots_agree_structurally()
    {
        var program = Program();
        var detached = program with { Flow = program.Flow with { Bindings = [.. program.Flow.Bindings.Select(b => b with { LexicalOwners = Copy(b.LexicalOwners) })] } };
        ManagedExceptionIrValidator.Validate(detached);
        var scalar = ValueScalarLowering.LowerExceptions(detached);
        var boxes = scalar.Flow.Flow; var objects = boxes.Flow.Flow;
        var copied = objects with { Bindings = [.. objects.Bindings.Select(b => b with { LexicalOwners = Copy(b.LexicalOwners) })] };
        ScalarManagedExceptionIrValidator.Validate(scalar with { Flow = scalar.Flow with { Flow = boxes with { Flow = boxes.Flow with { Flow = copied } } } });
    }
    [Theory]
    [InlineData("binding")] [InlineData("catalog")]
    public void Mapping_facts_cannot_drop_supplied_lexical_evidence(string mutation)
    {
        var program = Program(); var plan = program.Flow.InterfaceDispatch!;
        if (mutation == "binding")
        {
            var fact = plan.Methods.First(m => !m.Binding.LexicalOwners.IsEmpty);
            plan = plan with { Methods = plan.Methods.Replace(fact, fact with { Binding = fact.Binding with { LexicalOwners = [] } }) };
        }
        else
        {
            var type = plan.Types.Types.First(t => !t.LexicalOwners.IsEmpty);
            plan = plan with { Types = plan.Types with { Types = plan.Types.Types.Replace(type, type with { LexicalOwners = [] }) } };
        }
        var changed = program with { Flow = program.Flow with { InterfaceDispatch = plan } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(changed));
    }
}
