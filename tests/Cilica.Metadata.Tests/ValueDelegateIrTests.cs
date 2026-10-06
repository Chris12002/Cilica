using System;
using System.Linq;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading.Tasks;
using Cilica.Compiler;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ValueDelegateIrTests
{
    [Theory]
    [InlineData("missing")] [InlineData("wrong")] [InlineData("future")] [InlineData("fault")]
    public async Task Logical_and_scalar_call_packets_are_checked(string mutation)
    {
        var program = (await ValueDelegateTests.Programs.Value)[0];
        var flow = program.Flow.Flow.Flow;
        var entry = flow.Methods.Single(m => IrMethodId.From(m.Source) == flow.Entry);
        IrMethod Mutate(IrMethod method)
        {
            var block = method.Blocks.First(b => b.Values.Any(v => v.Expression is IrExpression.CallDelegate));
            var statement = (IrStatement.Define)block.Statements.First(s => s is IrStatement.Define { Value.Expression: IrExpression.CallDelegate });
            var call = (IrExpression.CallDelegate)statement.Value.Expression;
            var arguments = mutation switch {
                "missing" => call.Arguments.RemoveAt(0),
                "wrong" => call.Arguments.SetItem(0, call.Delegate),
                "future" => call.Arguments.SetItem(0, 99999), _ => call.Arguments };
            var changed = statement with { Value = statement.Value with { Expression = call with { Arguments = arguments } }, Exceptional = mutation == "fault" ? null : statement.Exceptional };
            return method with { Blocks = method.Blocks.Replace(block, block with { Statements = block.Statements.Replace(statement, changed) }) };
        }
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Methods = flow.Methods.Replace(entry, Mutate(entry)) } } } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program); var boxes = scalar.Flow.Flow; var objects = boxes.Flow.Flow; var lowered = objects.Flow;
        var scalarEntry = lowered.Methods.Single(m => IrMethodId.From(m.Source) == lowered.Entry);
        var badScalar = scalar with { Flow = scalar.Flow with { Flow = boxes with { Flow = boxes.Flow with { Flow = objects with { Flow = lowered with { Methods = lowered.Methods.Replace(scalarEntry, Mutate(scalarEntry)) } } } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(badScalar));
    }
    [Theory]
    [InlineData("unknown")] [InlineData("reference")] [InlineData("borrow")]
    public async Task Logical_signature_layouts_require_exact_included_values(string mutation)
    {
        var program = (await ValueDelegateTests.Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        var catalog = flow.Types.References; var declaration = Assert.Single(catalog.Delegates); var original = declaration.Parameters[0];
        var changedType = mutation switch {
            "unknown" => IrType.Value(original.Class! with { Token = 0x0200ffff }),
            "reference" => IrType.Value(catalog.ObjectBase), _ => IrType.Location(original) };
        var types = flow.Types with { References = catalog with { Delegates = catalog.Delegates.Replace(declaration, declaration with { Parameters = declaration.Parameters.SetItem(0, changedType) }) } };
        Assert.Throws<ArgumentException>(() => ValueLayoutPlanner.Build(types));
    }
    [Fact]
    public async Task Scalar_signature_must_match_its_preserved_logical_layout()
    {
        var program = ValueScalarLowering.LowerExceptions((await ValueDelegateTests.Programs.Value)[0]);
        var boxes = program.Flow.Flow; var objects = boxes.Flow.Flow; var flow = objects.Flow;
        var declaration = Assert.Single(flow.Types.Delegates); int index = declaration.Parameters.IndexOf(IrType.Int64); Assert.True(index >= 0);
        var changed = flow.Types with { Delegates = flow.Types.Delegates.Replace(declaration, declaration with { Parameters = declaration.Parameters.SetItem(index, IrType.Int32) }) };
        var invalid = program with { Flow = program.Flow with { Flow = boxes with { Flow = boxes.Flow with { Flow = objects with { Flow = flow with { Types = changed } } } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalid));
    }
    [Fact]
    public async Task Struct_identity_is_checked_before_scalar_fields_are_erased()
    {
        var program = (await ValueDelegateTests.Programs.Value)[14]; var flow = program.Flow.Flow.Flow;
        var declarations = flow.Types.References.Delegates; Assert.Equal(2, declarations.Length);
        var first = declarations.First(d => d.ParameterCount == 2); var second = declarations.First(d => d.ParameterCount == 1);
        var changed = first with { Parameters = first.Parameters.SetItem(0, second.Parameters[0]) };
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Types = flow.Types with { References = flow.Types.References with { Delegates = declarations.Replace(first, changed) } } } } } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid));
    }
    [Fact]
    public async Task Expanded_delegate_signature_keeps_the_existing_parameter_budget()
    {
        var program = (await ValueDelegateTests.Programs.Value)[4]; var flow = program.Flow.Flow.Flow;
        var layout = Assert.Single(flow.Types.Types); var id = ObjectTypeId.From(layout.Source);
        var childId = id with { Token = 0x0200eeee };
        var child = new ValueTypeLayout(layout.Source with { Token = childId.Token, Name = "ExpandedPair" }, layout.Attributes,
            [new(new(childId, 0x0400eeee), "First", FieldAttributes.Public, new ObjectStorage.Int32()),
             new(new(childId, 0x0400eeef), "Second", FieldAttributes.Public, new ObjectStorage.Int32())]);
        var fields = Enumerable.Range(0, 2050).Select(i => new ObjectField(new(id, 0x04008000 + i), "F" + i, FieldAttributes.Public, new ObjectStorage.Value(childId))).ToImmutableArray();
        var types = flow.Types with { Types = flow.Types.Types.Replace(layout, layout with { Fields = fields }).Add(child) };
        var changed = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Types = types } } } };
        ManagedExceptionIrValidator.Validate(changed);
        Assert.Contains("Expanded delegate parameter limit", Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(changed)).Message, StringComparison.Ordinal);
    }

}
