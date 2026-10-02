using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ObjectTranslationTests
{
    private const MethodAttributes Constructor = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
    private static ObjectTranslationResult Translate(string damage = "", MethodReadLimits? limits = null)
    {
        var b = new ResolutionAssemblyBuilder(new("ObjectProbe", new Version(1, 0, 0, 0)));
        var objectBase = b.Type("System", "Object");
        var objectCtor = b.Method(".ctor", [0x20, 0, 1], attributes: Constructor, emit: il =>
        {
            if (damage == "nontrivial_base") il.LoadConstantI4(0);
            il.OpCode(ILOpCode.Ret);
        });
        var node = b.Type("Test", "Node", baseType: objectBase, arity: damage == "generic" ? 1 : 0);
        var field = b.FieldWithAttributes("Value", damage is "readonly" or "readonly_ctor" or "wrong_readonly_receiver" ? FieldAttributes.Public | FieldAttributes.InitOnly
            : damage is "private_field" or "cross_private_field" ? FieldAttributes.Private : damage == "static_field" ? FieldAttributes.Public | FieldAttributes.Static : FieldAttributes.Public, 0x06, 0x08);
        var ctor = b.Method(".ctor", damage == "bad_ctor_return" ? [0x20, 1, 8, 8] : [0x20, 1, 1, 8],
            attributes: damage == "bad_ctor_flags" ? MethodAttributes.Public : damage == "private_ctor" ? MethodAttributes.Private | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName : Constructor, emit: il =>
        {
            if (damage != "missing_base") { il.LoadArgument(0); il.Call(objectCtor); }
            if (damage == "repeated_base") { il.LoadArgument(0); il.Call(objectCtor); }
            il.LoadArgument(damage == "wrong_readonly_receiver" ? 1 : 0);
            il.LoadArgument(1); il.OpCode(ILOpCode.Stfld); il.Token(field); il.OpCode(ILOpCode.Ret);
        });
        var mutate = b.Method("Change", [0x20, 1, 8, 8], attributes: MethodAttributes.Public |
            (damage == "virtual" ? MethodAttributes.Virtual : 0), emit: il =>
        {
            if (damage != "readonly_ctor") { il.LoadArgument(0); il.LoadArgument(1); il.OpCode(ILOpCode.Stfld); il.Token(field); }
            il.LoadArgument(0); il.OpCode(ILOpCode.Ldfld); il.Token(field); il.OpCode(ILOpCode.Ret);
        });
        var driver = b.Type("Test", "Driver", baseType: objectBase);
        if (damage == "initializer") b.Method(".cctor", [0, 0, 1], emit: il => il.OpCode(ILOpCode.Ret));
        var root = b.Method("Run", [0, 1, 8, 8], emit: il =>
        {
            if (damage == "branch") { var label = il.DefineLabel(); il.Branch(ILOpCode.Br_s, label); il.MarkLabel(label); }
            il.LoadArgument(0); il.OpCode(ILOpCode.Newobj); il.Token(damage == "newobj_nonconstructor" ? mutate : ctor);
            if (damage == "wrong_receiver") { il.OpCode(ILOpCode.Pop); il.LoadConstantI4(0); }
            if (damage is "cross_private_field" or "public_field") { il.OpCode(ILOpCode.Ldfld); il.Token(field); }
            else { il.LoadConstantI4(7); il.OpCode(ILOpCode.Callvirt); il.Token(mutate); }
            il.OpCode(ILOpCode.Ret);
            if (damage == "trailing") il.OpCode(ILOpCode.Nop);
        }, locals: damage == "maxstack" ? [0x07, 0] : null, maxStack: damage == "maxstack" ? 1 : 8);
        if (damage == "reference") b.MarkReferenceAssembly();
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var assembly = resolver.OpenInput(files.Write("probe.dll", b.Build())).Value!;
        return ObjectTranslator.Translate(resolver, assembly, root, assembly, limits);
    }

    [Fact]
    public void Constructor_mutation_and_nonvirtual_callvirt_detach_with_exact_fields_and_fault_edges()
    {
        var result = Translate(); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!; ObjectIrValidator.Validate(program);
        Assert.Equal(4, program.Flow.Methods.Length);
        var entry = program.Flow.Methods.Single(m => IrMethodId.From(m.Source) == program.Flow.Entry);
        var created = Assert.Single(entry.Blocks[0].Values.Where(v => v.Expression is IrExpression.NewObject));
        Assert.Equal(IrTypeKind.Reference, created.Type.Kind);
        var call = Assert.IsType<IrExpression.Call>(entry.Blocks[0].Values[^1].Expression); Assert.True(call.NullCheckReceiver);
        var constructor = program.Flow.Methods.Single(m => program.Bindings.Single(b => b.Method == IrMethodId.From(m.Source)).Kind == ObjectMethodKind.Constructor);
        Assert.IsType<IrStatement.Invoke>(constructor.Blocks[0].Statements[1]);
        Assert.All(program.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Statements).OfType<IrStatement.Define>()
            .Where(d => d.Value.Expression is IrExpression.NewObject or IrExpression.LoadField or IrExpression.Call), d => Assert.Equal(IrExceptionTarget.MethodExit, d.Exceptional));
        Assert.Throws<ArgumentException>(() => CppEmitter.EmitProgram(new(program.Flow.Entry, program.Flow.Methods)));
    }

    [Theory]
    [InlineData("nontrivial_base")]
    [InlineData("generic")]
    [InlineData("bad_ctor_return")]
    [InlineData("bad_ctor_flags")]
    [InlineData("private_ctor")]
    [InlineData("cross_private_field")]
    [InlineData("missing_base")]
    [InlineData("repeated_base")]
    [InlineData("newobj_nonconstructor")]
    [InlineData("wrong_receiver")]
    [InlineData("static_field")]
    [InlineData("readonly")]
    [InlineData("wrong_readonly_receiver")]
    [InlineData("initializer")]
    [InlineData("reference")]
    [InlineData("trailing")]
    [InlineData("maxstack")]
    public void Unsupported_or_invalid_object_input_has_diagnostics_and_no_partial_program(string damage)
    {
        var result = Translate(damage); Assert.False(result.Success); Assert.Null(result.Program);
        Assert.NotEmpty(result.Diagnostics); Assert.All(result.Diagnostics, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
        Assert.Contains(result.Diagnostics, d => !string.IsNullOrWhiteSpace(d.Location));
    }

    [Fact]
    public void Public_fields_on_top_level_public_classes_allow_cross_owner_loads()
    {
        var result = Translate("public_field"); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
    }

    [Fact]
    public void Ordinary_object_method_branches_are_translated()
    {
        var result = Translate("branch"); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        ObjectIrValidator.Validate(result.Program!);
        Assert.NotEmpty(ObjectCppEmitter.EmitProgram(result.Program!).Text);
    }

    [Fact]
    public void Virtual_instance_calls_now_have_an_explicit_dispatch_map()
    {
        var result = Translate("virtual"); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        ObjectIrValidator.Validate(result.Program!);
        Assert.Single(result.Program!.Dispatch);
        Assert.Contains(result.Program.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values),
            v => v.Expression is IrExpression.Call { VirtualDispatch: true, NullCheckReceiver: true });
    }

    [Fact]
    public void Readonly_fields_can_be_initialized_by_the_declaring_constructor_receiver()
    {
        var result = Translate("readonly_ctor"); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
    }

    [Fact]
    public void Private_fields_remain_accessible_to_their_declaring_instance_methods()
    {
        var result = Translate("private_field"); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
    }

    [Fact]
    public void Caller_read_limits_apply_to_every_discovered_method()
    {
        var result = Translate(limits: new(MaximumBodyBytes: 5));
        Assert.False(result.Success); Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData("allocation_fault")]
    [InlineData("load_fault")]
    [InlineData("store_fault")]
    [InlineData("void_fault")]
    [InlineData("constructor_target")]
    [InlineData("allocation_type")]
    [InlineData("allocation_args")]
    [InlineData("field_owner")]
    [InlineData("store_value")]
    [InlineData("binding_owner")]
    [InlineData("binding_kind")]
    [InlineData("binding_flags")]
    [InlineData("source_hash")]
    [InlineData("default_bindings")]
    [InlineData("duplicate_binding")]
    public void Forged_object_ir_cannot_bypass_operation_or_binding_rules(string damage)
    {
        var program = Translate().Program!;
        var entry = program.Flow.Methods.Single(m => IrMethodId.From(m.Source) == program.Flow.Entry);
        var block = entry.Blocks[0];
        var allocation = (IrStatement.Define)block.Statements[1];
        var allocated = (IrExpression.NewObject)allocation.Value.Expression;
        var ctorBinding = program.Bindings.Single(b => b.Kind == ObjectMethodKind.Constructor);
        var constructor = program.Flow.Methods.Single(m => IrMethodId.From(m.Source) == ctorBinding.Method);
        var change = program.Flow.Methods.Single(m => m.Source.Name.EndsWith("::Change", StringComparison.Ordinal));
        IrMethod altered = entry;
        switch (damage)
        {
            case "allocation_fault": altered = entry with { Blocks = [block with { Statements = block.Statements.SetItem(1, allocation with { Exceptional = null }) }] }; break;
            case "allocation_type": altered = entry with { Blocks = [block with { Statements = block.Statements.SetItem(1, allocation with { Value = allocation.Value with { Type = IrType.Int32 } }) }] }; break;
            case "constructor_target": altered = entry with { Blocks = [block with { Statements = block.Statements.SetItem(1, allocation with { Value = allocation.Value with { Expression = allocated with { Constructor = IrMethodId.From(change.Source) } } }) }] }; break;
            case "allocation_args": altered = entry with { Blocks = [block with { Statements = block.Statements.SetItem(1, allocation with { Value = allocation.Value with { Expression = allocated with { Arguments = default } } }) }] }; break;
            case "void_fault": altered = constructor with { Blocks = [constructor.Blocks[0] with { Statements = constructor.Blocks[0].Statements.SetItem(1, ((IrStatement.Invoke)constructor.Blocks[0].Statements[1]) with { Exceptional = (IrExceptionTarget)99 }) }] }; break;
            case "load_fault":
                var load = change.Blocks[0].Statements.OfType<IrStatement.Define>().Single(d => d.Value.Expression is IrExpression.LoadField);
                altered = change with { Blocks = [change.Blocks[0] with { Statements = change.Blocks[0].Statements.Replace(load, load with { Exceptional = null }) }] }; break;
            case "store_fault" or "field_owner" or "store_value":
                var store = change.Blocks[0].Statements.OfType<IrStatement.StoreField>().Single();
                var damaged = damage switch { "field_owner" => store with { Field = store.Field with { Owner = program.Flow.Types.ObjectBase } },
                    "store_value" => store with { Value = store.Receiver }, _ => store with { Exceptional = (IrExceptionTarget)99 } };
                altered = change with { Blocks = [change.Blocks[0] with { Statements = change.Blocks[0].Statements.Replace(store, damaged) }] }; break;
            case "binding_owner": program = program with { Bindings = program.Bindings.Replace(ctorBinding, ctorBinding with { Owner = program.Flow.Types.ObjectBase }) }; break;
            case "binding_kind": program = program with { Bindings = program.Bindings.Replace(ctorBinding, ctorBinding with { Kind = (ObjectMethodKind)99 }) }; break;
            case "binding_flags": program = program with { Bindings = program.Bindings.Replace(ctorBinding, ctorBinding with { Attributes = ctorBinding.Attributes | MethodAttributes.Virtual }) }; break;
            case "source_hash": altered = entry with { Source = entry.Source with { ContentHash = null! } }; break;
            case "default_bindings": program = program with { Bindings = default }; break;
            case "duplicate_binding": program = program with { Bindings = program.Bindings.SetItem(0, program.Bindings[1]) }; break;
        }
        var original = damage == "source_hash" ? entry : program.Flow.Methods.Single(m => m.Source.Token == altered.Source.Token);
        program = program with { Flow = program.Flow with { Methods = program.Flow.Methods.Replace(original, altered) } };
        Assert.Throws<ArgumentException>(() => ObjectIrValidator.Validate(program));
    }

    [Theory]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void Allocation_and_void_call_copies_share_the_exact_program_argument_limit(int lastArity, bool accepted)
    {
        var original = Translate().Program!;
        var binding = original.Bindings.Single(b => b.Kind == ObjectMethodKind.Constructor);
        var constructor = original.Flow.Methods.Single(m => IrMethodId.From(m.Source) == binding.Method);
        var first = constructor with { Parameters = [constructor.Parameters[0], .. Enumerable.Repeat(IrType.Int32, 32)] };
        var second = constructor with { Source = constructor.Source with { Token = 0x06000005 },
            Parameters = [constructor.Parameters[0], .. Enumerable.Repeat(IrType.Int32, lastArity)] };
        var baseBinding = original.Bindings.Single(b => b.Kind == ObjectMethodKind.ObjectBaseConstructor);
        var baseMethod = original.Flow.Methods.Single(m => IrMethodId.From(m.Source) == baseBinding.Method);
        var entryBinding = original.Bindings.Single(b => b.Method == original.Flow.Entry);
        var entry = original.Flow.Methods.Single(m => IrMethodId.From(m.Source) == original.Flow.Entry);
        var statements = ImmutableArray.CreateBuilder<IrStatement>();
        statements.Add(new IrStatement.Define(new(0, IrType.Int32, 0, new IrExpression.Constant(0))));
        for (int id = 1; id <= 2047; id++) statements.Add(new IrStatement.Define(new(id, constructor.Parameters[0], 0,
            new IrExpression.NewObject(binding.Method, [.. Enumerable.Repeat(0, 32)])), IrExceptionTarget.MethodExit));
        statements.Add(new IrStatement.Define(new(2048, constructor.Parameters[0], 0,
            new IrExpression.NewObject(IrMethodId.From(second.Source), [.. Enumerable.Repeat(0, lastArity)])), IrExceptionTarget.MethodExit));
        entry = entry with { Blocks = [new(0, 0, [], statements.ToImmutable(), new IrReturn(1, 0))] };
        var program = new ObjectIrProgram(original.Flow with { Methods = [baseMethod, first, second, entry] },
            [baseBinding, binding, binding with { Method = IrMethodId.From(second.Source) }, entryBinding]);
        // 2047*32 + lastArity allocation arguments + two base receiver copies = 65536 or 65537.
        if (accepted) ObjectIrValidator.Validate(program);
        else Assert.Contains("aggregate program", Assert.Throws<ArgumentException>(() => ObjectIrValidator.Validate(program)).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(4095, true)]
    [InlineData(4096, false)]
    public void Cached_field_snapshots_cannot_bypass_the_aggregate_catalog_limit(int count, bool accepted)
    {
        var b = new ResolutionAssemblyBuilder(new("ManyFields", new Version(1, 0, 0, 0)));
        var objectBase = b.Type("System", "Object"); b.Type("Test", "First", baseType: objectBase);
        FieldDefinitionHandle first = default;
        for (int i = 0; i < count; i++) { var field = b.Field("Field" + i, 0x06, 0x08); if (i == 0) first = field; }
        b.Type("Test", "Second", baseType: objectBase); var second = b.Field("Value", 0x06, 0x08);
        b.Type("Test", "Driver", baseType: objectBase);
        var root = b.Method("Read", [0, 0, 8], emit: il =>
        {
            il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Ldfld); il.Token(first); il.OpCode(ILOpCode.Pop);
            il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Ldfld); il.Token(second); il.OpCode(ILOpCode.Ret);
        });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("probe.dll", b.Build())).Value!;
        var result = ObjectTranslator.Translate(resolver, image, root, image);
        if (accepted) Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        else { Assert.False(result.Success); Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(result.Diagnostics).Id); }
    }

    [Fact]
    public void Inherited_field_memberrefs_require_the_exact_declaring_owner()
    {
        var b = new ResolutionAssemblyBuilder(new("InheritedField", new Version(1, 0, 0, 0)));
        var parent = b.Type("Test", "Parent"); b.Field("Value", 0x06, 0x08);
        var child = b.Type("Test", "Child", baseType: parent);
        var inherited = b.Member(child, "Value", 0x06, 0x08); var declared = b.Member(parent, "Value", 0x06, 0x08);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("probe.dll", b.Build())).Value!;
        Assert.True(resolver.ResolveField(image, inherited).Success);
        var rejected = resolver.ResolveDeclaredField(image, inherited); Assert.False(rejected.Success);
        Assert.Equal(DiagnosticIds.ResolutionUnsupported, Assert.Single(rejected.Diagnostics).Id);
        Assert.True(resolver.ResolveDeclaredField(image, declared).Success);
    }

    [Fact]
    public void Generic_enclosing_method_owners_and_excess_owner_depth_are_rejected()
    {
        var b = new ResolutionAssemblyBuilder(new("GenericOwner", new Version(1, 0, 0, 0)));
        b.Type("System", "Object"); var outer = b.Type("Test", "Outer", arity: 1); b.Type("", "Inner", parent: outer);
        var method = b.Method("Run", [0, 0, 8], emit: il => { il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("probe.dll", b.Build())).Value!;
        Assert.Equal(1, resolver.ReadMethodInput(image, method).Value!.OwnerGenericArity);
        Assert.False(Int32Translator.Translate(resolver, image, method).Success);
        Assert.False(ObjectTranslator.Translate(resolver, image, method, image).Success);
        Assert.Throws<ArgumentOutOfRangeException>(() => resolver.ReadMethodInput(image, method, new(MaximumOwnerDepth: 65)));
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(4, true)]
    public void Memberref_candidate_scans_obey_the_exact_shared_work_budget(int maximum, bool accepted)
    {
        var b = new ResolutionAssemblyBuilder(new("BindingBudget", new Version(1, 0, 0, 0)));
        var type = b.Type("Test", "Owner"); b.Method("Other", [0, 0, 8]); b.Method("Run", [0, 0, 8]);
        var member = b.Member(type, "Run", 0, 0, 8);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("probe.dll", b.Build())).Value!;
        var budget = new MemberResolutionBudget(maximum);
        var result = resolver.ResolveDeclaredMethod(image, member, budget);
        Assert.Equal(accepted, result.Success); Assert.Equal(0, budget.Remaining);
        if (!accepted) Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(result.Diagnostics).Id);
        Assert.False(budget.TryConsume(1));
    }

    [Fact]
    public void Readonly_constructor_writes_cannot_use_a_local_alias_of_this()
    {
        var program = Translate("readonly_ctor").Program!;
        var binding = program.Bindings.Single(b => b.Kind == ObjectMethodKind.Constructor);
        var ctor = program.Flow.Methods.Single(m => IrMethodId.From(m.Source) == binding.Method);
        var block = ctor.Blocks[0]; var store = block.Statements.OfType<IrStatement.StoreField>().Single();
        int index = block.Statements.IndexOf(store);
        var statements = block.Statements.Insert(index, new IrStatement.StoreLocal(store.ILOffset, 0, store.Receiver))
            .Insert(index + 1, new IrStatement.Define(new(3, ctor.Parameters[0], store.ILOffset, new IrExpression.Local(0))))
            .SetItem(index + 2, store with { Receiver = 3 });
        ctor = ctor with { Locals = [ctor.Parameters[0]], Blocks = [block with { Statements = statements }] };
        program = program with { Flow = program.Flow with { Methods = program.Flow.Methods.Select(m => IrMethodId.From(m.Source) == binding.Method ? ctor : m).ToImmutableArray() } };
        var error = Assert.Throws<ArgumentException>(() => ObjectIrValidator.Validate(program));
        Assert.Contains("readonly receiver", error.Message, StringComparison.Ordinal);
        Assert.Contains("::.ctor", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void Budgeted_member_owners_are_bounded_before_recursive_display_formatting(int depth, bool accepted)
    {
        var b = new ResolutionAssemblyBuilder(new("MemberOwnerDepth", new Version(1, 0, 0, 0)));
        TypeDefinitionHandle owner = default;
        for (int i = 0; i < depth; i++) owner = b.Type(i == 0 ? "Test" : "", "T" + i, parent: owner);
        b.Method("Run", [0, 0, 8]); var member = b.Member(owner, "Run", 0, 0, 8);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("probe.dll", b.Build())).Value!;
        var result = resolver.ResolveDeclaredMethod(image, member, new MemberResolutionBudget());
        Assert.Equal(accepted, result.Success);
        if (!accepted) Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(result.Diagnostics).Id);
    }
}
