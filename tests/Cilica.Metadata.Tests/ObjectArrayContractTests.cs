using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using Cilica.Compiler;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ObjectArrayContractTests
{
    private static readonly string Hash = new('a', 64);
    private static ObjectTypeId Id(int row) => new(Hash, 0x02000000 | row);
    private static readonly ObjectTypeId Object = Id(2), Contract = Id(3), ChildContract = Id(4),
        Parent = Id(5), Child = Id(6), Other = Id(7), Driver = Id(8);
    private static readonly ObjectTypeId Integers = ObjectTypeId.Vector(Object, ObjectArrayElement.Int32);
    private static ObjectTypeId Vector(ObjectTypeId element) => ObjectTypeId.Vector(Object, ObjectArrayElement.Reference(element));
    private static readonly ObjectTypeId[] Nominal = [Object, Contract, ChildContract, Parent, Child, Other, Driver];
    private static readonly ObjectTypeId[] Arrays = [Integers, Vector(Object), Vector(Contract), Vector(ChildContract), Vector(Parent), Vector(Child), Vector(Other)];
    private static readonly ObjectTypeId[] All = [.. Nominal, .. Arrays];
    private interface IRoot { }
    private interface IChild : IRoot { }
    private class ParentOracle : IChild { }
    private sealed class ChildOracle : ParentOracle { }
    private sealed class OtherOracle { }
    private sealed class DriverOracle { }
    private static TypeSource Source(ObjectTypeId id) => new("fixture", id.ContentHash, id.Token, "Example.Type" + id.Token);
    private static ObjectTypeLayout Layout(ObjectTypeId id, ObjectTypeId? parent, params ObjectTypeId[] contracts) =>
        new(Source(id), parent, TypeAttributes.Public, []) { Interfaces = [.. contracts] };
    private static ObjectTypeCatalog Catalog() => new([.. All], Object,
        [Layout(Object, null), Layout(Parent, Object, ChildContract), Layout(Child, Parent), Layout(Other, Object), Layout(Driver, Object)])
    {
        Interfaces = [new(Source(Contract), TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract, []),
            new(Source(ChildContract), TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract, [Contract])],
        Arrays = [.. Arrays],
    };
    private static IrStatement.Define Define(int id, IrType type, IrExpression expression, bool fault = false) =>
        new(new(id, type, 0, expression), fault ? IrExceptionTarget.MethodExit : null);
    private static IrMethod Method(ImmutableArray<IrType> parameters, IrType result, ImmutableArray<IrStatement> statements, int returned) =>
        new(new("fixture", Hash, 0x06000001, "Example.Driver::Run"), parameters, result,
            [new(0, 0, [], statements, new IrReturn(1, returned))], [.. All.Select(IrType.Reference)]);
    private static ReferenceIrProgram Flow(IrMethod method, ObjectTypeCatalog? catalog = null) =>
        new(IrMethodId.From(method.Source), [method], catalog ?? Catalog());
    private static ObjectIrProgram Program(IrMethod method, ObjectTypeCatalog? catalog = null) =>
        new(Flow(method, catalog), [new(IrMethodId.From(method.Source), Driver, ObjectMethodKind.Static,
            MethodAttributes.Public | MethodAttributes.Static, true)]);
    private static IrMethod Operations(bool references = false)
    {
        var array = references ? Vector(Child) : Integers;
        var view = references ? Vector(Parent) : Integers;
        var element = references ? IrType.Reference(Parent) : IrType.Int32;
        return Method([IrType.Int32, IrType.Int32, IrType.Int32], IrType.Int32,
            [Define(0, IrType.Int32, new IrExpression.Parameter(0)),
             Define(1, IrType.Reference(array), new IrExpression.NewArray(array, 0), true),
             Define(2, IrType.Int32, new IrExpression.Parameter(1)),
             references ? Define(3, element, new IrExpression.Local(Array.IndexOf(All, Parent)))
                 : Define(3, element, new IrExpression.Parameter(2)),
             new IrStatement.StoreElement(0, view, 1, 2, 3),
             Define(4, element, new IrExpression.LoadElement(view, 1, 2), true),
             Define(5, IrType.Int32, new IrExpression.ArrayLength(1), true)], 5);
    }

    [Fact]
    public void All_assignment_pairs_match_dotnet_for_nominal_types_vectors_and_covariance()
    {
        Type[] runtime = [typeof(object), typeof(IRoot), typeof(IChild), typeof(ParentOracle), typeof(ChildOracle), typeof(OtherOracle), typeof(DriverOracle),
            typeof(int[]), typeof(object[]), typeof(IRoot[]), typeof(IChild[]), typeof(ParentOracle[]), typeof(ChildOracle[]), typeof(OtherOracle[])];
        for (int source = 0; source < All.Length; source++) for (int target = 0; target < All.Length; target++)
        {
            var method = Method([IrType.Reference(All[source])], IrType.Reference(All[target]),
                [Define(0, IrType.Reference(All[source]), new IrExpression.Parameter(0))], 0);
            if (runtime[target].IsAssignableFrom(runtime[source])) ReferenceIrValidator.Validate(Flow(method));
            else Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(Flow(method)));
        }
    }

    [Fact]
    public void Array_identity_survives_json_without_inventing_a_TypeDef_or_merging_images()
    {
        var catalog = Catalog(); ObjectModelValidator.Validate(catalog);
        var restored = JsonSerializer.Deserialize<ObjectTypeCatalog>(JsonSerializer.Serialize(catalog))!;
        ObjectModelValidator.Validate(restored); Assert.Equal(catalog.Arrays, restored.Arrays);
        Assert.NotEqual(Object, Integers); Assert.NotEqual(Integers, Vector(Object));
        var foreign = Other with { ContentHash = new string('b', 64) };
        var foreignArray = Vector(foreign);
        Assert.NotEqual(Vector(Other), foreignArray);
        var cross = catalog with { Roots = catalog.Roots.Add(foreignArray), Arrays = catalog.Arrays.Add(foreignArray),
            Types = catalog.Types.Add(Layout(foreign, Object)) };
        var method = Method([IrType.Reference(foreignArray)], IrType.Reference(Vector(Other)),
            [Define(0, IrType.Reference(foreignArray), new IrExpression.Parameter(0))], 0);
        ObjectModelValidator.Validate(cross);
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(Flow(method, cross)));
        ReferenceIrValidator.Validate(Flow(method with { ReturnType = IrType.Reference(Vector(Object)) }, cross));
        Assert.Throws<ArgumentNullException>(() => ObjectTypeId.Vector(Object, null!));
        Assert.Throws<ArgumentNullException>(() => ObjectTypeId.Vector(null!, ObjectArrayElement.Int32));
        Assert.Throws<ArgumentException>(() => ObjectTypeId.Vector(Integers, ObjectArrayElement.Int32));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Array_operations_have_typed_results_fault_edges_and_allocation_roots(bool references)
    {
        var method = Operations(references); var program = Program(method);
        ObjectIrValidator.Validate(program);
        var roots = Assert.Single(ObjectRootAssignment.Assign(program));
        Assert.Equal(ObjectSafepointKind.Allocation, Assert.Single(roots.Safepoints).Kind);
        Assert.Contains(roots.Slots, s => s.Kind == ObjectRootKind.Value && s.Index == 1);
        if (references) Assert.Contains(roots.Slots, s => s.Kind == ObjectRootKind.Value && s.Index == 4);
        Assert.Equal(roots.Slots.Length, roots.Slots.Select(s => s.Slot).Distinct().Count());
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(program.Flow));
        Assert.Throws<ArgumentException>(() => CppEmitter.EmitProgram(new(program.Flow.Entry, program.Flow.Methods)));
        Assert.Contains("allocate_array", ObjectCppEmitter.EmitProgram(program).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Negative_lengths_indices_and_null_receivers_are_runtime_faults_not_invalid_IR()
    {
        var method = Method([], IrType.Int32,
            [Define(0, IrType.Int32, new IrExpression.Constant(-1)),
             Define(1, IrType.Reference(Integers), new IrExpression.NewArray(Integers, 0), true),
             Define(2, IrType.NullReference, new IrExpression.Null()),
             new IrStatement.StoreElement(0, Integers, 2, 0, 0),
             Define(3, IrType.Int32, new IrExpression.LoadElement(Integers, 2, 0), true),
             Define(4, IrType.Int32, new IrExpression.ArrayLength(2), true)], 4);
        ObjectIrValidator.Validate(Program(method));
    }

    [Fact]
    public void Array_allocation_requires_access_to_the_nominal_element()
    {
        var method = Operations() with { Blocks = [Operations().Blocks[0] with { Statements = [
            Define(0, IrType.Int32, new IrExpression.Constant(1)),
            Define(1, IrType.Reference(Vector(Other)), new IrExpression.NewArray(Vector(Other), 0), true)] , Terminator = new IrReturn(1, 0) }] };
        var catalog = Catalog() with { Types = Catalog().Types.SetItem(3, Catalog().Types[3] with { Attributes = TypeAttributes.NotPublic }) };
        var program = Program(method, catalog);
        Assert.Throws<ArgumentException>(() => ObjectIrValidator.Validate(program));
        ObjectIrValidator.Validate(program with { Bindings = [program.Bindings[0] with { Owner = Other, OwnerExternallyVisible = false }] });
    }

    [Fact]
    public void Array_references_flow_through_declared_joins_locals_calls_returns_and_identity()
    {
        var child = IrType.Reference(Vector(Child)); var parent = IrType.Reference(Vector(Parent));
        var contract = IrType.Reference(Vector(Contract));
        var callee = Method([parent], parent, [Define(0, parent, new IrExpression.Parameter(0))], 0)
            with { Source = new("fixture", Hash, 0x06000002, "Callee") };
        var method = Method([child], parent, [], 0) with { Blocks = [
            new(0, 0, [], [Define(0, child, new IrExpression.Parameter(0)), Define(1, IrType.NullReference, new IrExpression.Null())],
                new IrJump(1, new(1, [0, 1]))),
            new(1, 2, [parent, contract], [
                new IrStatement.Define(new(0, parent, 2, new IrExpression.StackInput(0))),
                new IrStatement.StoreLocal(2, Array.IndexOf(All, Vector(Parent)), 0),
                new IrStatement.Define(new(1, parent, 2, new IrExpression.Local(Array.IndexOf(All, Vector(Parent))))),
                new IrStatement.Define(new(2, IrType.Int32, 2, new IrExpression.Compare(Int32Comparison.Equal, 0, 1))),
                new IrStatement.Define(new(3, parent, 2, new IrExpression.Call(IrMethodId.From(callee.Source), [1])), IrExceptionTarget.MethodExit)],
                new IrReturn(3, 3))] };
        ReferenceIrValidator.Validate(new(IrMethodId.From(method.Source), [method, callee], Catalog()));
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(new(IrMethodId.From(method.Source),
            [method with { ReturnType = child }, callee], Catalog())));
    }

    [Fact]
    public void Array_fields_retain_reference_slots_and_include_the_nominal_element_closure()
    {
        var fields = new[] { Vector(Child), Integers }.Select((a, i) => new ObjectField(new(Driver, 0x04000001 + i),
            "Array" + i, FieldAttributes.Public, new ObjectStorage.Reference(a))).ToImmutableArray();
        var catalog = new ObjectTypeCatalog([Driver], Object,
            [Layout(Object, null), Layout(Parent, Object), Layout(Child, Parent), Layout(Driver, Object) with { Fields = fields }])
            { Arrays = [Vector(Child), Integers] };
        ObjectModelValidator.Validate(catalog);
        Assert.Equal([0, 1], catalog.Types[^1].ReferenceSlots);
        var method = Method([IrType.Reference(Driver)], IrType.Reference(Object),
            [Define(0, IrType.Reference(Driver), new IrExpression.Parameter(0))], 0) with { Locals = [] };
        ReferenceIrValidator.Validate(Flow(method, catalog));
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(Flow(method, catalog with
            { Roots = catalog.Roots.Add(Vector(Object)), Arrays = catalog.Arrays.Add(Vector(Object)) })));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Arrays_share_the_exact_256_type_limit_with_nominal_elements(bool excess)
    {
        var nominal = Enumerable.Range(2, 128).Select(Id).ToArray();
        var arrays = nominal.Select(Vector).ToImmutableArray();
        if (excess) arrays = arrays.Add(Integers);
        var catalog = new ObjectTypeCatalog(arrays, Object,
            [.. nominal.Select(id => Layout(id, id == Object ? null : Object))]) { Arrays = arrays };
        if (excess) Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(catalog));
        else
        {
            ObjectModelValidator.Validate(catalog);
            var method = Method([.. arrays.Select(IrType.Reference)], IrType.Reference(Object),
                [Define(0, IrType.Reference(arrays[0]), new IrExpression.Parameter(0))], 0) with { Locals = [] };
            ReferenceIrValidator.Validate(Flow(method, catalog));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Array_stores_cannot_leak_an_uninitialized_constructor_receiver(bool initialized)
    {
        const MethodAttributes flags = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
        var baseId = new IrMethodId(Hash, 0x06000003); var ctorId = new IrMethodId(Hash, 0x06000002);
        var baseBody = new IrMethod(new("fixture", Hash, baseId.Token, "Object::.ctor"), [IrType.Reference(Object)], IrType.Void,
            [new(0, 0, [], [], new IrVoidReturn(0))], []);
        var baseCall = new IrStatement.Invoke(0, baseId, [0]);
        var store = new IrStatement.StoreElement(0, Vector(Object), 2, 1, 0);
        var ctor = new IrMethod(new("fixture", Hash, ctorId.Token, "Driver::.ctor"), [IrType.Reference(Driver)], IrType.Void,
            [new(0, 0, [], [Define(0, IrType.Reference(Driver), new IrExpression.Parameter(0)),
                Define(1, IrType.Int32, new IrExpression.Constant(1)),
                Define(2, IrType.Reference(Vector(Object)), new IrExpression.NewArray(Vector(Object), 1), true),
                initialized ? baseCall : store, initialized ? store : baseCall], new IrVoidReturn(1))], []);
        var entry = Method([], IrType.Int32, [Define(0, IrType.Reference(Driver), new IrExpression.NewObject(ctorId, []), true),
            Define(1, IrType.Int32, new IrExpression.Constant(0))], 1);
        var program = Program(entry) with { Flow = new(IrMethodId.From(entry.Source), [entry, ctor, baseBody], Catalog()),
            Bindings = [Program(entry).Bindings[0], new(ctorId, Driver, ObjectMethodKind.Constructor, flags, true),
                new(baseId, Object, ObjectMethodKind.ObjectBaseConstructor, flags, true)] };
        if (initialized) ObjectIrValidator.Validate(program);
        else Assert.Contains("uninitialized", Assert.Throws<ArgumentException>(() => ObjectIrValidator.Validate(program)).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("local")]
    [InlineData("allocation")]
    [InlineData("cast")]
    [InlineData("field")]
    public void Unsupported_deep_array_shapes_are_rejected_before_recursive_hashing(string shape)
    {
        var nested = Object;
        for (int i = 0; i < 4096; i++) nested = Vector(nested);
        var method = Operations(); var statements = method.Blocks[0].Statements.ToBuilder();
        switch (shape)
        {
            case "local": method = method with { Locals = method.Locals.Add(IrType.Reference(nested)) }; break;
            case "allocation": statements[1] = Define(1, IrType.Reference(Integers), new IrExpression.NewArray(nested, 0), true); break;
            case "cast": statements[6] = Define(5, IrType.Int32, new IrExpression.CastClass(nested, 1), true); break;
            case "field": statements[6] = Define(5, IrType.Int32, new IrExpression.LoadField(new(nested, 0x04000001), 1), true); break;
        }
        method = method with { Blocks = [method.Blocks[0] with { Statements = statements.ToImmutable() }] };
        Assert.Throws<ArgumentException>(() => ObjectIrValidator.Validate(Program(method)));
    }

    [Fact]
    public void A_callee_signature_is_checked_before_assignment_and_dispatch_can_hash_it()
    {
        var nested = Object;
        for (int i = 0; i < 4096; i++) nested = Vector(nested);
        var callee = Method([IrType.Reference(nested)], IrType.Int32, [Define(0, IrType.Int32, new IrExpression.Constant(0))], 0)
            with { Source = new("fixture", Hash, 0x06000002, "Callee") };
        var entry = Method([], IrType.Int32, [Define(0, IrType.NullReference, new IrExpression.Null()),
            Define(1, IrType.Int32, new IrExpression.Call(IrMethodId.From(callee.Source), [0]), true)], 1);
        var flow = new ReferenceIrProgram(IrMethodId.From(entry.Source), [entry, callee], Catalog());
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(flow));
        var program = Program(entry) with { Flow = flow, Bindings = [Program(entry).Bindings[0],
            new(IrMethodId.From(callee.Source), Driver, ObjectMethodKind.Static, MethodAttributes.Public | MethodAttributes.Static, true)] };
        Assert.Throws<ArgumentException>(() => ObjectIrValidator.Validate(program));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cil_vectors_translate_and_emit_native_storage(bool signature)
    {
        var b = new ResolutionAssemblyBuilder(new("ArrayBoundary" + signature, new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object"); b.Type("Example", "Driver", baseType: obj);
        var entry = b.Method("Run", [0, 0, 8], locals: signature ? [7, 1, 0x1d, 8] : null, emit: il =>
        {
            if (!signature) { il.LoadConstantI4(1); il.OpCode(ILOpCode.Newarr); il.Token(obj); il.OpCode(ILOpCode.Pop); }
            il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        var result = ObjectControlFlowTests.Translate(new(b, entry));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Single(result.Program!.Flow.Types.Arrays);
        Assert.Contains("ArrayObject", ObjectCppEmitter.EmitProgram(result.Program).Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("default")]
    [InlineData("duplicate")]
    [InlineData("nominal")]
    [InlineData("anchor")]
    [InlineData("element_missing")]
    [InlineData("int_with_type")]
    [InlineData("reference_without_type")]
    [InlineData("unknown_kind")]
    [InlineData("nested")]
    [InlineData("null")]
    [InlineData("null_object")]
    [InlineData("array_base")]
    [InlineData("array_interface")]
    [InlineData("unused")]
    public void Malformed_or_unrelated_arrays_are_rejected_before_value_flow(string shape)
    {
        var catalog = Catalog(); var array = Integers;
        switch (shape)
        {
            case "default": catalog = catalog with { Arrays = default }; break;
            case "duplicate": catalog = catalog with { Arrays = catalog.Arrays.Add(array) }; break;
            case "nominal": array = Object; break;
            case "anchor": array = array with { Token = Driver.Token }; break;
            case "element_missing": array = Vector(Id(99)); break;
            case "int_with_type": array = array with { ArrayElement = new(ObjectArrayElementKind.Int32, Object) }; break;
            case "reference_without_type": array = array with { ArrayElement = new(ObjectArrayElementKind.Reference) }; break;
            case "unknown_kind": array = array with { ArrayElement = new((ObjectArrayElementKind)42) }; break;
            case "nested": array = Vector(Vector(Object)); break;
            case "null": array = null!; break;
            case "null_object": catalog = catalog with { ObjectBase = null! }; break;
            case "array_base": catalog = catalog with { Types = catalog.Types.SetItem(4, catalog.Types[4] with { BaseType = Vector(Object) }) }; break;
            case "array_interface": catalog = catalog with { Types = catalog.Types.SetItem(4, catalog.Types[4] with { Interfaces = [Vector(Contract)] }) }; break;
            case "unused": catalog = catalog with { Roots = [.. catalog.Roots.Where(r => r != Integers)] }; break;
        }
        if (shape is "nominal" or "anchor" or "element_missing" or "int_with_type" or "reference_without_type" or "unknown_kind" or "nested" or "null")
            catalog = catalog with { Arrays = catalog.Arrays.SetItem(0, array) };
        Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(catalog));
    }

    [Theory]
    [InlineData("allocation_kind")]
    [InlineData("allocation_length")]
    [InlineData("allocation_result")]
    [InlineData("allocation_edge")]
    [InlineData("length_receiver")]
    [InlineData("length_result")]
    [InlineData("length_edge")]
    [InlineData("load_index")]
    [InlineData("load_receiver")]
    [InlineData("load_type")]
    [InlineData("load_result")]
    [InlineData("load_edge")]
    [InlineData("store_index")]
    [InlineData("store_receiver")]
    [InlineData("store_value")]
    [InlineData("store_edge")]
    [InlineData("store_forward")]
    [InlineData("array_owner")]
    public void Forged_operations_do_not_bypass_array_type_or_fault_checks(string shape)
    {
        var method = Operations(); var statements = method.Blocks[0].Statements.ToBuilder();
        switch (shape)
        {
            case "allocation_kind": statements[1] = Define(1, IrType.Reference(Object), new IrExpression.NewArray(Object, 0), true); break;
            case "allocation_length": statements[1] = Define(1, IrType.Reference(Integers), new IrExpression.NewArray(Integers, -1), true); break;
            case "allocation_result": statements[1] = Define(1, IrType.Reference(Vector(Object)), new IrExpression.NewArray(Integers, 0), true); break;
            case "allocation_edge": statements[1] = Define(1, IrType.Reference(Integers), new IrExpression.NewArray(Integers, 0)); break;
            case "length_receiver": statements[6] = Define(5, IrType.Int32, new IrExpression.ArrayLength(0), true); break;
            case "length_result": statements[6] = Define(5, IrType.Reference(Integers), new IrExpression.ArrayLength(1), true); break;
            case "length_edge": statements[6] = Define(5, IrType.Int32, new IrExpression.ArrayLength(1)); break;
            case "load_index": statements[5] = Define(4, IrType.Int32, new IrExpression.LoadElement(Integers, 1, 1), true); break;
            case "load_receiver": statements[5] = Define(4, IrType.Int32, new IrExpression.LoadElement(Integers, 0, 2), true); break;
            case "load_type": statements[5] = Define(4, IrType.Reference(Object), new IrExpression.LoadElement(Vector(Object), 1, 2), true); break;
            case "load_result": statements[5] = Define(4, IrType.Reference(Object), new IrExpression.LoadElement(Integers, 1, 2), true); break;
            case "load_edge": statements[5] = Define(4, IrType.Int32, new IrExpression.LoadElement(Integers, 1, 2)); break;
            case "store_index": statements[4] = new IrStatement.StoreElement(0, Integers, 1, 1, 3); break;
            case "store_receiver": statements[4] = new IrStatement.StoreElement(0, Integers, 0, 2, 3); break;
            case "store_value": statements[4] = new IrStatement.StoreElement(0, Integers, 1, 2, 1); break;
            case "store_edge": statements[4] = new IrStatement.StoreElement(0, Integers, 1, 2, 3, (IrExceptionTarget)99); break;
            case "store_forward": statements[4] = new IrStatement.StoreElement(0, Integers, 1, 2, 4); break;
        }
        method = method with { Blocks = [method.Blocks[0] with { Statements = statements.ToImmutable() }] };
        var program = Program(method);
        if (shape == "array_owner") program = program with { Bindings = [program.Bindings[0] with { Owner = Integers }] };
        Assert.Throws<ArgumentException>(() => ObjectIrValidator.Validate(program));
        Assert.Throws<ArgumentException>(() => ObjectRootAssignment.Assign(program));
    }
}
