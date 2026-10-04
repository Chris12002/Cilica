using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ValueObjectIrTests
{
    internal static ValueObjectIrProgram Model()
    {
        var (values, bindings) = NativeValueReturnTests.Model(); var flow = values.Flow;
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(LocationProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var objects = ObjectTranslator.Translate(resolver, input,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(LocationProbe).GetMethod("Build")!.MetadataToken), library).Program!;
        var node = objects.Flow.Types.Types.Single(t => t.Source.Name.EndsWith("LocationNode", StringComparison.Ordinal));
        var nodeType = IrType.Reference(ObjectTypeId.From(node.Source));
        var constructor = objects.Bindings.Single(b => b.Owner == nodeType.Class && b.Kind == ObjectMethodKind.Constructor).Method;
        var make = flow.Methods.Single(m => m.Source.Name == "Make");
        var reference = flow.Types.Types.Single().Fields.Single(f => f.Name == "Reference").Id;
        ImmutableArray<IrStatement> statements = [.. make.Blocks[0].Statements,
            NativeScalarLocationTests.D(3, nodeType, new IrExpression.NewObject(constructor, [0]), true),
            NativeScalarLocationTests.D(4, make.ReturnType, new IrExpression.WithValueField(reference, 2, 3))];
        var changed = make with { Blocks = [make.Blocks[0] with { Statements = statements, Terminator = new IrReturn(1, 4) }] };
        var references = flow.Types.References with
        {
            Roots = [.. flow.Types.References.Roots.Concat(objects.Flow.Types.Roots).Distinct()],
            Types = [.. flow.Types.References.Types.Concat(objects.Flow.Types.Types).DistinctBy(t => ObjectTypeId.From(t.Source))],
        };
        var constructors = objects.Bindings.Where(b => b.Kind is ObjectMethodKind.Constructor or ObjectMethodKind.ObjectBaseConstructor).ToImmutableArray();
        return new(new(flow with { Types = flow.Types with { References = references },
            Methods = [.. flow.Methods.Replace(make, changed), .. objects.Flow.Methods.Where(m => constructors.Any(b => b.Method == IrMethodId.From(m.Source)))] }),
            [.. bindings, .. constructors]);
    }

    [Fact]
    public void Constructs_objects_inside_value_returns_and_preserves_allocation_roots()
    {
        var program = Model(); ValueObjectIrValidator.Validate(program);
        var plans = ValueRootAssignment.Assign(program);
        Assert.Single(plans.SelectMany(p => p.Safepoints), p => p.Kind == ObjectSafepointKind.Allocation);
        var lowered = ValueScalarLowering.LowerObjects(program); ScalarLocationIrValidator.Validate(lowered);
        Assert.Contains(lowered.Flow.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values), v => v.Expression is IrExpression.NewObject);
    }

    [Fact]
    public void Earlier_value_profiles_remain_closed_to_class_allocation()
    {
        var program = Model();
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerArrays(new(program.Flow, program.Bindings)));
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerLocations(program.Flow, program.Bindings));
    }

    [Theory]
    [InlineData("missing_base")]
    [InlineData("repeated_base")]
    [InlineData("readonly_outside_constructor")]
    [InlineData("missing_allocation_fault")]
    [InlineData("wrong_allocation_argument")]
    public void Shared_constructor_and_field_rules_reject_malformed_composition(string kind)
    {
        var program = Model(); var flow = program.Flow.Flow;
        IrMethod original, changed;
        if (kind is "missing_base" or "repeated_base")
        {
            var binding = program.Bindings.First(b => b.Kind == ObjectMethodKind.Constructor);
            original = flow.Methods.Single(m => IrMethodId.From(m.Source) == binding.Method);
            var block = original.Blocks[0]; var call = block.Statements.OfType<IrStatement.Invoke>().Single();
            var statements = kind == "missing_base" ? block.Statements.Remove(call)
                : block.Statements.Insert(block.Statements.IndexOf(call), call);
            changed = original with { Blocks = [block with { Statements = statements }] };
        }
        else
        {
            original = flow.Methods.Single(m => m.Source.Name == "Make"); var block = original.Blocks[0];
            var statements = block.Statements;
            if (kind == "readonly_outside_constructor")
            {
                var field = flow.Types.References.Types.SelectMany(t => t.Fields).Single(f => f.Name == "Initial").Id;
                statements = statements.Insert(4, new IrStatement.StoreField(0, field, 3, 0));
            }
            else
            {
                var allocation = (IrStatement.Define)statements[3];
                allocation = kind == "missing_allocation_fault" ? allocation with { Exceptional = null }
                    : allocation with { Value = allocation.Value with { Expression = ((IrExpression.NewObject)allocation.Value.Expression) with { Arguments = [2] } } };
                statements = statements.SetItem(3, allocation);
            }
            changed = original with { Blocks = [block with { Statements = statements }] };
        }
        program = program with { Flow = new(flow with { Methods = flow.Methods.Replace(original, changed) }) };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerObjects(program));
    }
}
