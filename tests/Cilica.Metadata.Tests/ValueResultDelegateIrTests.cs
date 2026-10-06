using System;
using System.Linq;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading.Tasks;
using Cilica.Compiler;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ValueResultDelegateIrTests
{
    [Theory]
    [InlineData("missing")] [InlineData("wrong")] [InlineData("readonly")] [InlineData("fault")]
    public async Task Scalar_result_destinations_and_faults_are_validated(string mutation)
    {
        var scalar = ValueScalarLowering.LowerExceptions((await ValueResultDelegateTests.Programs.Value)[0]);
        var boxes = scalar.Flow.Flow; var objects = boxes.Flow.Flow; var flow = objects.Flow;
        var method = flow.Methods.Single(m => IrMethodId.From(m.Source) == flow.Entry);
        var block = method.Blocks.First(b => b.Statements.Any(s => s is IrStatement.InvokeDelegate));
        var call = block.Statements.OfType<IrStatement.InvokeDelegate>().Single(); var statements = block.Statements;
        if (mutation == "readonly")
        {
            var original = statements.OfType<IrStatement.Define>().Single(s => s.Value.Id == call.Arguments[^1]);
            statements = statements.Replace(original, original with { Value = original.Value with { Type = original.Value.Type with { ReadOnly = true } } });
        }
        else
        {
            var changed = mutation switch {
                "missing" => call with { Arguments = call.Arguments.RemoveAt(call.Arguments.Length - 1) },
                "wrong" => call with { Arguments = call.Arguments.SetItem(call.Arguments.Length - 1, call.Arguments[0]) },
                _ => call with { Exceptional = (IrExceptionTarget)99 } };
            statements = statements.Replace(call, changed);
        }
        var changedMethod = method with { Blocks = method.Blocks.Replace(block, block with { Statements = statements }) };
        var invalid = scalar with { Flow = scalar.Flow with { Flow = boxes with { Flow = boxes.Flow with { Flow = objects with { Flow = flow with { Methods = flow.Methods.Replace(method, changedMethod) } } } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalid));
    }
    [Theory]
    [InlineData("unknown")] [InlineData("reference")] [InlineData("borrow")] [InlineData("other-value")]
    public async Task Logical_result_layouts_and_target_signatures_are_checked(string mutation)
    {
        var program = (await ValueResultDelegateTests.Programs.Value)[mutation == "other-value" ? 7 : 0]; var flow = program.Flow.Flow.Flow; var catalog = flow.Types.References;
        var declaration = Assert.Single(catalog.Delegates); var result = mutation switch {
            "unknown" => IrType.Value(declaration.ReturnType.Class! with { Token = 0x0200ffff }),
            "reference" => IrType.Reference(catalog.ObjectBase),
            "other-value" => IrType.Value(flow.Types.Types.Select(t => ObjectTypeId.From(t.Source)).First(id => id != declaration.ReturnType.Class)),
            _ => IrType.Location(declaration.ReturnType) };
        var changed = catalog with { Delegates = catalog.Delegates.Replace(declaration, declaration with { ReturnType = result }) };
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Types = flow.Types with { References = changed } } } } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid));
    }
    [Fact]
    public async Task Scalar_signature_must_reconstruct_the_exact_logical_return_slots()
    {
        var scalar = ValueScalarLowering.LowerExceptions((await ValueResultDelegateTests.Programs.Value)[0]);
        var boxes = scalar.Flow.Flow; var objects = boxes.Flow.Flow; var flow = objects.Flow;
        var declaration = Assert.Single(flow.Types.Delegates); Assert.Equal(IrType.Void, declaration.ReturnType);
        int slot = declaration.Parameters.IndexOf(IrType.Location(IrType.Int64)); Assert.True(slot >= 0);
        var changed = declaration with { Parameters = declaration.Parameters.SetItem(slot, IrType.Location(IrType.Int32)) };
        var invalid = scalar with { Flow = scalar.Flow with { Flow = boxes with { Flow = boxes.Flow with { Flow = objects with { Flow = flow with { Types = flow.Types with { Delegates = flow.Types.Delegates.Replace(declaration, changed) } } } } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalid));
    }
    [Fact]
    public async Task Result_slots_share_the_existing_parameter_budget()
    {
        var program = (await ValueResultDelegateTests.Programs.Value)[4]; var flow = program.Flow.Flow.Flow;
        var layout = Assert.Single(flow.Types.Types); var id = ObjectTypeId.From(layout.Source); var childId = id with { Token = 0x0200eeee };
        var child = new ValueTypeLayout(layout.Source with { Token = childId.Token, Name = "ExpandedResultPair" }, layout.Attributes,
            [new(new(childId, 0x0400eeee), "First", FieldAttributes.Public, new ObjectStorage.Int32()),
             new(new(childId, 0x0400eeef), "Second", FieldAttributes.Public, new ObjectStorage.Int32())]);
        var fields = Enumerable.Range(0, 2050).Select(i => new ObjectField(new(id, 0x04008000 + i), "F" + i, FieldAttributes.Public, new ObjectStorage.Value(childId))).ToImmutableArray();
        var types = flow.Types with { Types = flow.Types.Types.Replace(layout, layout with { Fields = fields }).Add(child) };
        var changed = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Types = types } } } };
        ManagedExceptionIrValidator.Validate(changed);
        Assert.Contains("Expanded delegate result parameter limit", Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(changed)).Message, StringComparison.Ordinal);
    }
}
