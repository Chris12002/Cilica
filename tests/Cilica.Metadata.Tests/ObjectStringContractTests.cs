using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text.Json;
using Cilica.Compiler;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ObjectStringContractTests
{
    private static readonly string Hash = new('b', 64);
    private static ObjectTypeId Id(int row) => new(Hash, 0x02000000 | row);
    private static readonly ObjectTypeId Object = Id(2), Text = Id(3), Driver = Id(4);
    private static readonly IrType StringType = IrType.Reference(Text);
    private static TypeSource Source(ObjectTypeId id) => new("fixture", id.ContentHash, id.Token, "Example.Type" + id.Token);
    private static ObjectTypeCatalog Catalog() => new([Text], Object, [new(Source(Object), null, TypeAttributes.Public, [])]) { String = Text };
    private static IrStatement.Define Define(int id, IrType type, IrExpression expression, bool fault = false) =>
        new(new(id, type, id, expression), fault ? IrExceptionTarget.MethodExit : null);
    private static IrMethod Method(ImmutableArray<IrStatement> statements, int returned = 0) =>
        new(new("fixture", Hash, 0x06000001, "Example.Driver::Run"), [], IrType.Int32,
            [new(0, 0, [], statements, new IrReturn(statements.Length + 1, returned))], []);
    private static ObjectIrProgram Program(IrMethod method, ObjectTypeCatalog? catalog = null) =>
        new(new(IrMethodId.From(method.Source), [method], catalog ?? Catalog()),
            [new(IrMethodId.From(method.Source), Driver, ObjectMethodKind.Static, MethodAttributes.Public | MethodAttributes.Static, true)]);
    private static ImmutableArray<ushort> Units(string value) => [.. value.Select(c => (ushort)c)];
    private static IrMethod Operations() => Method([
        Define(0, StringType, new IrExpression.StringLiteral(Units("A\0\ud800\udc00\udfff")), true),
        Define(1, IrType.NullReference, new IrExpression.Null()),
        Define(2, IrType.Int32, new IrExpression.Constant(2)),
        Define(3, IrType.Int32, new IrExpression.StringLength(0), true),
        Define(4, IrType.Int32, new IrExpression.StringCodeUnit(0, 2), true),
        Define(5, IrType.Int32, new IrExpression.StringEquals(0, 1), true),
        Define(6, StringType, new IrExpression.StringConcat(0, 1), true)], 4);

    [Fact]
    public void String_operations_validate_and_publish_literal_and_concat_roots()
    {
        var program = Program(Operations()); ObjectIrValidator.Validate(program);
        var roots = Assert.Single(ObjectRootAssignment.Assign(program));
        Assert.Equal(new[] { 0, 6 }, roots.Safepoints.Select(s => s.Statement));
        Assert.All(roots.Safepoints, s => Assert.Equal(ObjectSafepointKind.Allocation, s.Kind));
        Assert.Equal(new[] { 0, 1, 6 }, roots.Slots.Select(s => s.Index));
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(program.Flow));
        Assert.Throws<ArgumentException>(() => CppEmitter.EmitProgram(new(program.Flow.Entry, program.Flow.Methods)));
        Assert.NotEmpty(ObjectCppEmitter.EmitProgram(program).Text);
    }

    [Fact]
    public void Numeric_UTF16_payload_round_trips_embedded_zero_and_unpaired_surrogates_exactly()
    {
        var literal = new IrExpression.StringLiteral(Units("\0\ud800X\udc00\uffff"));
        var restored = JsonSerializer.Deserialize<IrExpression.StringLiteral>(JsonSerializer.Serialize(literal))!;
        Assert.Equal(new ushort[] { 0, 0xd800, 88, 0xdc00, 0xffff }, restored.CodeUnits);
        var catalog = JsonSerializer.Deserialize<ObjectTypeCatalog>(JsonSerializer.Serialize(Catalog()))!;
        ObjectModelValidator.Validate(catalog); Assert.Equal(Text, catalog.String);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Null_operands_and_negative_indices_are_runtime_faults_not_bad_IR(bool negative)
    {
        var method = Method([Define(0, IrType.NullReference, new IrExpression.Null()),
            Define(1, IrType.Int32, new IrExpression.Constant(negative ? -1 : 0)),
            Define(2, IrType.Int32, new IrExpression.StringLength(0), true),
            Define(3, IrType.Int32, new IrExpression.StringCodeUnit(0, 1), true),
            Define(4, IrType.Int32, new IrExpression.StringEquals(0, 0), true),
            Define(5, StringType, new IrExpression.StringConcat(0, 0), true)], 4);
        ObjectIrValidator.Validate(Program(method));
        // No reference-typed values are needed to make the selected intrinsic type used.
        ObjectIrValidator.Validate(Program(Method(method.Blocks[0].Statements[..5], 4)));
    }

    [Fact]
    public void Detached_string_assignability_matches_dotnet_for_strings_objects_and_vectors()
    {
        ObjectTypeId[] ids = [Object, Text, ObjectTypeId.Vector(Object, ObjectArrayElement.Reference(Object)), ObjectTypeId.Vector(Object, ObjectArrayElement.Reference(Text))];
        Type[] oracle = [typeof(object), typeof(string), typeof(object[]), typeof(string[])];
        var catalog = Catalog() with { Roots = [.. ids], Arrays = [.. ids.Skip(2)] };
        for (int from = 0; from < ids.Length; from++) for (int to = 0; to < ids.Length; to++)
        {
            var method = Method([Define(0, IrType.Reference(ids[from]), new IrExpression.Parameter(0))]) with
            { Parameters = [IrType.Reference(ids[from])], ReturnType = IrType.Reference(ids[to]), Locals = [.. ids.Select(IrType.Reference)] };
            var flow = Program(method, catalog).Flow;
            if (oracle[to].IsAssignableFrom(oracle[from])) ReferenceIrValidator.Validate(flow);
            else Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(flow));
        }
    }

    [Fact]
    public void String_fields_and_array_elements_close_the_catalog_without_mutable_string_layouts()
    {
        var vector = ObjectTypeId.Vector(Object, ObjectArrayElement.Reference(Text));
        var catalog = Catalog() with { Roots = [Driver], Arrays = [vector], Types = Catalog().Types.Add(
            new(Source(Driver), Object, TypeAttributes.Public, [new(new(Driver, 0x04000001), "Values", FieldAttributes.Public, new ObjectStorage.Reference(vector))])) };
        ObjectModelValidator.Validate(catalog);
        ObjectModelValidator.Validate(new ObjectModel(Driver, Object, catalog.Types) { Arrays = [vector], String = Text });
        var method = Method([Define(0, IrType.Int32, new IrExpression.Constant(0))]) with { Locals = [IrType.Reference(Driver)] };
        ObjectIrValidator.Validate(Program(method, catalog));
        Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(catalog with { String = null }));
    }

    [Theory]
    [InlineData("object")]
    [InlineData("module")]
    [InlineData("uppercase")]
    [InlineData("array")]
    [InlineData("class")]
    [InlineData("interface")]
    [InlineData("derived")]
    [InlineData("unrelated")]
    public void Forged_string_catalog_shapes_are_rejected(string shape)
    {
        var c = Catalog();
        c = shape switch
        {
            "object" => c with { String = Object }, "module" => c with { String = Id(1) },
            "uppercase" => c with { String = Text with { ContentHash = Hash.ToUpperInvariant() } },
            "array" => c with { String = ObjectTypeId.Vector(Object, ObjectArrayElement.Reference(Text)) },
            "class" => c with { Types = c.Types.Add(new(Source(Text), Object, TypeAttributes.Public | TypeAttributes.Sealed, [])) },
            "interface" => c with { Interfaces = [new(Source(Text), TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Interface, [])] },
            "derived" => c with { Roots = [Driver], Types = c.Types.Add(new(Source(Driver), Text, TypeAttributes.Public, [])) },
            _ => c with { Roots = [Object] },
        };
        Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(c));
    }

    [Theory]
    [InlineData("wrong_result")]
    [InlineData("missing_fault")]
    [InlineData("forward")]
    [InlineData("index_reference")]
    [InlineData("object_operand")]
    [InlineData("default_literal")]
    [InlineData("binding")]
    [InlineData("array_access")]
    public void Forged_string_operations_cannot_bypass_storage_and_fault_contracts(string shape)
    {
        var method = Operations(); var statements = method.Blocks[0].Statements;
        var operation = (IrStatement.Define)statements[4];
        switch (shape)
        {
            case "wrong_result": operation = operation with { Value = operation.Value with { Type = StringType } }; break;
            case "missing_fault": operation = operation with { Exceptional = null }; break;
            case "forward": operation = operation with { Value = operation.Value with { Expression = new IrExpression.StringLength(6) } }; break;
            case "index_reference": operation = operation with { Value = operation.Value with { Expression = new IrExpression.StringCodeUnit(0, 0) } }; break;
            case "object_operand": statements = statements.SetItem(0, Define(0, IrType.Reference(Object), new IrExpression.Local(0))); method = method with { Locals = [IrType.Reference(Object)] }; break;
            case "default_literal": statements = statements.SetItem(0, Define(0, StringType, new IrExpression.StringLiteral(default), true)); break;
            case "array_access": operation = operation with { Value = operation.Value with { Expression = new IrExpression.ArrayLength(0) } }; break;
        }
        statements = statements.SetItem(4, operation);
        var program = Program(method with { Blocks = [method.Blocks[0] with { Statements = statements }] });
        if (shape == "binding") program = program with { Bindings = [program.Bindings[0] with { Owner = Text }] };
        Assert.Throws<ArgumentException>(() => ObjectIrValidator.Validate(program));
    }

    [Theory]
    [InlineData(65536, 1, true)]
    [InlineData(65537, 1, false)]
    [InlineData(65536, 4, true)]
    [InlineData(65536, 5, false)]
    public void Literal_unit_limits_include_repeated_contents(int length, int copies, bool valid)
    {
        var units = Enumerable.Repeat((ushort)0xd800, length).ToImmutableArray();
        var statements = Enumerable.Range(0, copies).Select(i => (IrStatement)Define(i, StringType, new IrExpression.StringLiteral(units), true))
            .Append(Define(copies, IrType.Int32, new IrExpression.Constant(0))).ToImmutableArray();
        var program = Program(Method(statements, copies));
        if (valid) ObjectIrValidator.Validate(program); else Assert.Throws<ArgumentException>(() => ObjectIrValidator.Validate(program));
    }

    [Theory]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void Distinct_literal_budget_is_ordinal_in_UTF16(int count, bool valid)
    {
        var statements = Enumerable.Range(0, count).Select(i => (IrStatement)Define(i, StringType, new IrExpression.StringLiteral([(ushort)i]), true))
            .Append(Define(count, IrType.Int32, new IrExpression.Constant(0))).ToImmutableArray();
        var program = Program(Method(statements, count));
        if (valid) ObjectIrValidator.Validate(program); else Assert.Throws<ArgumentException>(() => ObjectIrValidator.Validate(program));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Literal_unit_budget_is_shared_across_reachable_methods(bool excess)
    {
        var units = Enumerable.Repeat((ushort)65, ObjectStringLimits.MaximumLiteralCodeUnits).ToImmutableArray();
        IrMethod Helper(int token, int copies) => Method([.. Enumerable.Range(0, copies).Select(i =>
            (IrStatement)Define(i, StringType, new IrExpression.StringLiteral(units), true)),
            Define(copies, IrType.Int32, new IrExpression.Constant(0))], copies) with
            { Source = new("fixture", Hash, token, "Example.Driver::Helper" + token) };
        var first = Helper(0x06000002, 2); var second = Helper(0x06000003, excess ? 3 : 2);
        var program = Program(Method([Define(0, IrType.Int32, new IrExpression.Call(IrMethodId.From(first.Source), []), true),
            Define(1, IrType.Int32, new IrExpression.Call(IrMethodId.From(second.Source), []), true)], 1));
        program = program with { Flow = program.Flow with { Methods = [.. program.Flow.Methods, first, second] },
            Bindings = [.. program.Bindings, program.Bindings[0] with { Method = IrMethodId.From(first.Source) },
                program.Bindings[0] with { Method = IrMethodId.From(second.Source) }] };
        if (excess) Assert.Throws<ArgumentException>(() => ObjectIrValidator.Validate(program));
        else ObjectIrValidator.Validate(program);
    }

    [Fact]
    public void Deep_forged_selected_string_is_rejected_before_recursive_equality_or_hashing()
    {
        var forged = Text;
        for (int i = 0; i < 4096; i++) forged = ObjectTypeId.Vector(Object, ObjectArrayElement.Reference(forged));
        Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(Catalog() with { String = forged }));
    }

    [Fact]
    public void Interface_string_signatures_require_the_same_selected_identity_as_executable_flow()
    {
        var contract = Id(5); var declared = new IrMethodId(Hash, 0x06000002);
        var catalog = Catalog() with { Roots = [Text, contract], Interfaces = [new(Source(contract),
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Interface, [])] };
        var declaration = new ObjectInterfaceDeclaration(new("fixture", Hash, declared.Token, "Example.IText::Get"), contract, "Get",
            MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.NewSlot,
            true, [IrType.Reference(contract)], StringType);
        var plan = new ObjectInterfaceDispatchPlan(catalog, [declaration], [], [new(declared, [])]);
        var method = Method([Define(0, IrType.NullReference, new IrExpression.Null()),
            Define(1, StringType, new IrExpression.Call(declared, [0], NullCheckReceiver: true, VirtualDispatch: true), true),
            Define(2, IrType.Int32, new IrExpression.Constant(0))], 2);
        var program = Program(method, catalog) with { InterfaceDispatch = plan };
        ObjectIrValidator.Validate(program);
        var other = Id(6);
        var foreignPlan = plan with { Types = catalog with { String = other, Roots = [other, contract] },
            Declarations = [declaration with { ReturnType = IrType.Reference(other) }] };
        ObjectInterfacePlanValidator.Validate(foreignPlan);
        Assert.Contains("catalog disagree", Assert.Throws<ArgumentException>(() =>
            ObjectIrValidator.Validate(program with { InterfaceDispatch = foreignPlan })).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Literal_field_initialization_before_base_call_keeps_unfinished_receiver_and_string_rooted()
    {
        var field = new ObjectField(new(Driver, 0x04000001), "Value", FieldAttributes.Public, new ObjectStorage.Reference(Text));
        var catalog = Catalog() with { Roots = [Driver], Types = Catalog().Types.Add(new(Source(Driver), Object, TypeAttributes.Public, [field])) };
        var ctorId = new IrMethodId(Hash, 0x06000002); var baseId = new IrMethodId(Hash, 0x06000003);
        var ctor = new IrMethod(new("fixture", Hash, ctorId.Token, "Example.Driver::.ctor"), [IrType.Reference(Driver)], IrType.Void,
            [new(0, 0, [], [Define(0, IrType.Reference(Driver), new IrExpression.Parameter(0)),
                Define(1, StringType, new IrExpression.StringLiteral([]), true), new IrStatement.StoreField(2, field.Id, 0, 1),
                new IrStatement.Invoke(3, baseId, [0])], new IrVoidReturn(4))], []);
        var baseCtor = new IrMethod(new("fixture", Hash, baseId.Token, "System.Object::.ctor"), [IrType.Reference(Object)], IrType.Void,
            [new(0, 0, [], [], new IrVoidReturn(0))], []);
        var program = Program(Method([Define(0, IrType.Reference(Driver), new IrExpression.NewObject(ctorId, []), true),
            Define(1, IrType.Int32, new IrExpression.Constant(0))], 1), catalog);
        const MethodAttributes flags = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
        program = program with { Flow = program.Flow with { Methods = [.. program.Flow.Methods, ctor, baseCtor] },
            Bindings = [.. program.Bindings, new(ctorId, Driver, ObjectMethodKind.Constructor, flags, true),
                new(baseId, Object, ObjectMethodKind.ObjectBaseConstructor, flags, true)] };
        var roots = ObjectRootAssignment.Assign(program).Single(r => r.Method == ctorId);
        Assert.Contains(roots.Slots, s => s.Kind == ObjectRootKind.Parameter && s.Index == 0);
        Assert.Contains(roots.Slots, s => s.Kind == ObjectRootKind.Value && s.Index == 1);
        Assert.Contains(roots.Safepoints, s => s.Kind == ObjectSafepointKind.Allocation && s.ILOffset == 1);
    }

    [Fact]
    public void Unused_selected_string_is_rejected_but_storage_only_programs_emit()
    {
        var method = Method([Define(0, IrType.Int32, new IrExpression.Constant(0))]);
        Assert.Throws<ArgumentException>(() => ObjectIrValidator.Validate(Program(method)));
        var storage = Program(method with { Locals = [StringType] });
        ObjectIrValidator.Validate(storage);
        Assert.NotEmpty(ObjectCppEmitter.EmitProgram(storage).Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Selected_string_shares_the_catalog_type_budget(bool excess)
    {
        var nominal = Enumerable.Range(4, excess ? 255 : 254).Select(Id).ToArray();
        var catalog = Catalog() with { Roots = [Text, .. nominal], Types = [.. Catalog().Types,
            .. nominal.Select(id => new ObjectTypeLayout(Source(id), Object, TypeAttributes.Public, []))] };
        if (excess) Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(catalog));
        else ObjectModelValidator.Validate(catalog);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void String_CIL_and_signatures_remain_located_diagnostic_boundaries(bool signature)
    {
        var b = new ResolutionAssemblyBuilder(new("StringBoundary" + signature, new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object"); b.Type("Example", "Driver", baseType: obj);
        var entry = b.Method("Run", [0, 0, 8], locals: signature ? [7, 1, 0x0e] : null, emit: il =>
        {
            if (!signature) { il.LoadString(b.String("A\0B")); il.OpCode(ILOpCode.Pop); }
            il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        var result = ObjectControlFlowTests.Translate(new(b, entry));
        Assert.False(result.Success); Assert.NotNull(Assert.Single(result.Diagnostics).Location);
    }
}
