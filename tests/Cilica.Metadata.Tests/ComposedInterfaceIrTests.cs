using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ComposedInterfaceIrTests
{
    // Compose independently translated closures at an authored IR boundary. This
    // exercises scalar dispatch without claiming CIL discovery has been extended.
    private static ManagedExceptionIrProgram Compose(string name)
    {
        var objects = ObjectInterfaceTranslationTests.Translate(name);
        var translated = IntegerObjectTranslationTests.Translate(nameof(WideObjects.Values));
        Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
        var values = translated.Program!; var flow = values.Flow.Flow.Flow;
        static ImmutableArray<T> Union<T, TKey>(ImmutableArray<T> first, ImmutableArray<T> second, Func<T, TKey> key) where TKey : notnull =>
            [.. first.Concat(second).DistinctBy(key)];
        var references = flow.Types.References with {
            Roots = Union(flow.Types.References.Roots, objects.Flow.Types.Roots, t => t),
            Types = Union(flow.Types.References.Types, objects.Flow.Types.Types, t => ObjectTypeId.From(t.Source)),
            Interfaces = Union(flow.Types.References.Interfaces, objects.Flow.Types.Interfaces, t => ObjectTypeId.From(t.Source)),
            Arrays = Union(flow.Types.References.Arrays, objects.Flow.Types.Arrays, t => t) };
        var types = flow.Types with { References = references };
        var first = flow.Methods.Single(m => IrMethodId.From(m.Source) == flow.Entry);
        var second = objects.Flow.Methods.Single(m => IrMethodId.From(m.Source) == objects.Flow.Entry);
        var source = first.Source with { Token = 0x060f0000 + (objects.Flow.Entry.Token & 0xffff), Name = "AuthoredComposition" };
        var entry = IrMethodId.From(source);
        var wrapper = new IrMethod(source, [IrType.Int32, IrType.Int32], IrType.Int32, [new(0, 0, [], [
            new IrStatement.Define(new(0, IrType.Int32, 0, new IrExpression.Parameter(0))),
            new IrStatement.Define(new(1, IrType.Int32, 1, new IrExpression.Parameter(1))),
            new IrStatement.Define(new(2, IrType.Int32, 2, new IrExpression.Call(flow.Entry, [0, 1])), IrExceptionTarget.MethodExit),
            new IrStatement.Define(new(3, IrType.Int32, 3, new IrExpression.Call(objects.Flow.Entry, second.Parameters.Length == 1 ? [1] : [0, 1])), IrExceptionTarget.MethodExit),
            new IrStatement.Define(new(4, IrType.Int32, 4, new IrExpression.Binary(Int32Operation.AddWrapping, 2, 3)))], new IrReturn(5, 4))], []);
        if (name == nameof(InterfaceCallProbe.NullCall))
        {
            ObjectTypeId Exception(string name) => ObjectTypeId.From(references.Types.Single(t => t.Source.Name == "System." + name).Source);
            var nullType = Exception(nameof(NullReferenceException)); var divideType = Exception(nameof(DivideByZeroException));
            IrBlock Catch(int id, ObjectTypeId type, int result) => new(id, id * 10, [IrType.Reference(type)], [
                new IrStatement.Define(new(0, IrType.Int32, id * 10, new IrExpression.Constant(result))),
                new IrStatement.StoreLocal(id * 10 + 1, 1, 0)], new IrLeave(id * 10 + 2, new(4, [])));
            wrapper = wrapper with { Locals = [IrType.Int32, IrType.Int32], Blocks = [
                new(0, 0, [], [.. wrapper.Blocks[0].Statements.Take(3), new IrStatement.StoreLocal(3, 0, 2)], new IrJump(4, new(1, []))),
                new(1, 10, [], [new IrStatement.Define(new(0, IrType.Int32, 10, new IrExpression.Parameter(1))),
                    new IrStatement.Define(new(1, IrType.Int32, 11, new IrExpression.Call(objects.Flow.Entry, [0])), IrExceptionTarget.MethodExit),
                    new IrStatement.StoreLocal(12, 1, 1)], new IrLeave(13, new(4, []))),
                Catch(2, nullType, 41), Catch(3, divideType, 53),
                new(4, 40, [], [new IrStatement.Define(new(0, IrType.Int32, 40, new IrExpression.Local(0))),
                    new IrStatement.Define(new(1, IrType.Int32, 41, new IrExpression.Local(1))),
                    new IrStatement.Define(new(2, IrType.Int32, 42, new IrExpression.Binary(Int32Operation.AddWrapping, 0, 1)))], new IrReturn(43, 2))],
                ExceptionRegions = [new(1, 2, 2, 3, IrExceptionClause.Catch, nullType), new(1, 2, 3, 4, IrExceptionClause.Catch, divideType)] };
        }
        var methods = Union(flow.Methods, objects.Flow.Methods, m => IrMethodId.From(m.Source)).Add(wrapper);
        var bindings = Union(values.Flow.Bindings, objects.Bindings, b => b.Method).Add(
            values.Flow.Bindings.Single(b => b.Method == flow.Entry) with { Method = entry, Attributes = MethodAttributes.Public | MethodAttributes.Static });
        return new(new(new(new(entry, methods, types)), bindings) { InterfaceDispatch = objects.InterfaceDispatch! with { Types = references } }, values.Exceptions);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Scalar_interface_dispatch_composes_with_wide_values_and_fault_runtime(bool optimized)
    {
        (string Name, Func<int, int, int> Oracle)[] probes = [
            (nameof(InterfaceCallProbe.Run), InterfaceCallProbe.Run),
            (nameof(InterfaceCallProbe.ReferenceFlow), (_, value) => InterfaceCallProbe.ReferenceFlow(value)),
            (nameof(InterfaceCallProbe.MetadataOnlySlot), (_, value) => InterfaceCallProbe.MetadataOnlySlot(value)),
            (nameof(InterfaceCallProbe.NullCall), (_, value) => {
                try { return InterfaceCallProbe.NullCall(value); }
                catch (NullReferenceException) { return 41; }
                catch (DivideByZeroException) { return 53; }
            })];
        return IntegerObjectTranslationTests.VerifyTranslated(optimized, probes.Select(p =>
            (p.Name, Compose(p.Name), (Func<int, int, int>)((a, b) => unchecked(WideObjects.Values(a, b) + p.Oracle(a, b))))).ToArray());
    }

    [Theory]
    [InlineData("catalog")] [InlineData("target")] [InlineData("specialization")]
    public void Composed_interface_plans_are_checked_before_lowering(string mutation)
    {
        var program = Compose(nameof(InterfaceCallProbe.Run));
        ManagedExceptionIrValidator.Validate(program);
        var plan = program.Flow.InterfaceDispatch!;
        plan = mutation switch {
            "catalog" => plan with { Types = plan.Types with { Integer64 = false } },
            "specialization" => plan with { Declarations = plan.Declarations.SetItem(0, plan.Declarations[0] with {
                Source = plan.Declarations[0].Source with { Specialization = "invalid" } }) },
            _ => plan with { Dispatch = plan.Dispatch.SetItem(0, plan.Dispatch[0] with { Targets = [] }) } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(program with { Flow = program.Flow with { InterfaceDispatch = plan } }));
    }
}
