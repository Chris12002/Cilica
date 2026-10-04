using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Cilica.Compiler;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ValueArrayIrTests
{
    internal static ValueArrayIrProgram Model()
    {
        var (program, bindings) = NativeValueReturnTests.Model(); var flow = program.Flow;
        var array = ObjectTypeId.Vector(flow.Types.References.ObjectBase, ObjectArrayElement.Int32);
        var reference = flow.Types.Types.Single().Fields.Single(f => f.Name == "Reference").Id;
        var make = flow.Methods.Single(m => m.Source.Name == "Make");
        ImmutableArray<IrStatement> statements = [.. make.Blocks[0].Statements, D(3, IrType.Int32, new IrExpression.Constant(2)), D(4, IrType.Reference(array), new IrExpression.NewArray(array, 3), true),
             D(5, IrType.Int32, new IrExpression.Constant(0)), new IrStatement.StoreElement(0, array, 4, 5, 0),
             D(6, make.ReturnType, new IrExpression.WithValueField(reference, 2, 4))];
        make = make with { Blocks = [make.Blocks[0] with { Statements = statements, Terminator = new IrReturn(1, 6) }] };
        return new(new(flow with { Types = flow.Types with { References = flow.Types.References with { Arrays = [array], Roots = flow.Types.References.Roots.Add(array) } },
            Methods = flow.Methods.Replace(flow.Methods.Single(m => m.Source.Name == "Make"), make) }), bindings);
    }
    private static IrStatement.Define D(int id, IrType type, IrExpression expression, bool fault = false) =>
        new(new(id, type, 0, expression), fault ? IrExceptionTarget.MethodExit : null);

    [Fact]
    public void Composition_plans_allocation_and_embedded_roots_and_lowers_return_packets()
    {
        var program = Model(); var roots = ValueRootAssignment.Assign(program);
        var method = program.Flow.Flow.Methods.Single(m => m.Source.Name == "Make");
        var plan = roots.Single(r => r.Method == IrMethodId.From(method.Source));
        Assert.Single(plan.Safepoints, p => p.Kind == ObjectSafepointKind.Allocation);
        Assert.Contains(plan.Slots, s => s.Kind == ObjectRootKind.Value && s.Index == 6 && !s.Fields.IsEmpty);
        var lowered = ValueScalarLowering.LowerArrays(program);
        ScalarLocationIrValidator.Validate(lowered);
        Assert.Contains(lowered.Flow.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Statements), s => s is IrStatement.StoreElement);
    }

    [Fact]
    public void Original_profiles_still_reject_allocating_value_flow()
    {
        var program = Model();
        Assert.Throws<ArgumentException>(() => ManagedLocationIrValidator.Validate(program.Flow));
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerLocations(program.Flow, program.Bindings));
    }

    [Theory]
    [InlineData("missing_fault")]
    [InlineData("wrong_length")]
    [InlineData("wrong_element")]
    [InlineData("nonstatic")]
    [InlineData("inaccessible_call")]
    [InlineData("class_operation")]
    public void Malformed_compositions_are_rejected_before_expansion(string kind)
    {
        var program = Model(); var flow = program.Flow.Flow;
        if (kind is "nonstatic" or "inaccessible_call")
        {
            var binding = program.Bindings.Single(b => flow.Methods.Single(m => IrMethodId.From(m.Source) == b.Method).Source.Name == "Make");
            binding = kind == "nonstatic" ? binding with { Kind = ObjectMethodKind.Instance }
                : binding with { Owner = binding.Owner with { Token = binding.Owner.Token + 1 }, Attributes = MethodAttributes.Static | MethodAttributes.Private };
            program = program with { Bindings = [.. program.Bindings.Select(b => b.Method == binding.Method ? binding : b)] };
        }
        else
        {
            var make = flow.Methods.Single(m => m.Source.Name == "Make"); var block = make.Blocks[0];
            var changed = block.Statements.Select(s => s switch
            {
                IrStatement.Define { Value.Id: 4 } d when kind == "missing_fault" => d with { Exceptional = null },
                IrStatement.Define { Value.Id: 4 } d when kind == "wrong_length" => d with { Value = d.Value with { Expression = ((IrExpression.NewArray)d.Value.Expression) with { Length = 2 } } },
                IrStatement.StoreElement store when kind == "wrong_element" => store with { Value = 2 },
                _ => s,
            }).ToImmutableArray();
            if (kind == "class_operation") changed = changed.Add(D(7, IrType.Reference(flow.Types.References.String!), new IrExpression.StringLiteral([]), true));
            var method = make with { Blocks = [block with { Statements = changed }] };
            program = program with { Flow = new(flow with { Methods = flow.Methods.Replace(make, method) }) };
        }
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerArrays(program));
    }
}
