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

public sealed class ObjectHierarchyTranslationTests
{
    private const MethodAttributes Constructor = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;

    [Theory]
    [InlineData("skip")]
    [InlineData("repeat")]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("alias")]
    [InlineData("read")]
    [InlineData("escape")]
    [InlineData("compare")]
    [InlineData("inherited_write_before_base")]
    [InlineData("inherited_readonly_write")]
    [InlineData("branch")]
    public void Invalid_constructor_sequences_fail_without_partial_IR(string damage)
    {
        var result = ConstructorSequence(damage);
        Assert.Null(result.Program);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticIds.ExecutionUnsupported, diagnostic.Id);
        Assert.Contains("token 0x", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Declared_readonly_initializers_and_computed_base_arguments_are_accepted()
    {
        var result = ConstructorSequence("");
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!;
        ObjectIrValidator.Validate(program);
        var child = program.Bindings.Single(b => b.Owner.Token == 0x02000004 && b.Kind == ObjectMethodKind.Constructor);
        var method = program.Flow.Methods.Single(m => IrMethodId.From(m.Source) == child.Method);
        int store = method.Blocks[0].Statements.IndexOf(method.Blocks[0].Statements.OfType<IrStatement.StoreField>().Single());
        int call = method.Blocks[0].Statements.IndexOf(method.Blocks[0].Statements.OfType<IrStatement.Invoke>().Single());
        Assert.True(store < call);
        Assert.NotEmpty(ObjectCppEmitter.EmitProgram(program).Text);
    }

    private static ObjectTranslationResult ConstructorSequence(string damage)
    {
        var builder = new ResolutionAssemblyBuilder(new("ConstructorSequence", new Version(1, 0, 0, 0)));
        var objectBase = builder.Type("System", "Object");
        var objectCtor = builder.Method(".ctor", [0x20, 0, 1], attributes: Constructor, emit: il => il.OpCode(ILOpCode.Ret));
        var parent = builder.Type("Example", "Parent", baseType: objectBase);
        var parentField = builder.FieldWithAttributes("Value", FieldAttributes.Public | FieldAttributes.InitOnly, 6, 8);
        var parentCtor = builder.Method(".ctor", [0x20, 1, 1, 8], attributes: Constructor, emit: il =>
        {
            il.LoadArgument(0); il.Call(objectCtor); il.LoadArgument(0); il.LoadArgument(1);
            il.OpCode(ILOpCode.Stfld); il.Token(parentField); il.OpCode(ILOpCode.Ret);
        });
        var child = builder.Type("Example", "Child", baseType: parent);
        var childField = builder.FieldWithAttributes("Value", FieldAttributes.Public | FieldAttributes.InitOnly, 6, 8);
        var escape = builder.Method("Escape", [0, 1, 1, 0x12, (byte)(MetadataTokens.GetRowNumber(child) * 4)], emit: il => il.OpCode(ILOpCode.Ret));
        var childCtor = builder.Method(".ctor", [0x20, 1, 1, 8], attributes: Constructor,
            locals: damage == "alias" ? [7, 1, 0x12, (byte)(MetadataTokens.GetRowNumber(child) * 4)] : null, emit: il =>
        {
            il.LoadArgument(0); il.LoadConstantI4(19); il.OpCode(ILOpCode.Stfld); il.Token(childField);
            if (damage == "branch") { var label = il.DefineLabel(); il.Branch(ILOpCode.Br_s, label); il.MarkLabel(label); }
            if (damage == "alias") { il.LoadArgument(0); il.StoreLocal(0); }
            if (damage == "read") { il.LoadArgument(0); il.OpCode(ILOpCode.Ldfld); il.Token(childField); il.OpCode(ILOpCode.Pop); }
            if (damage == "escape") { il.LoadArgument(0); il.Call(escape); }
            if (damage == "compare") { il.LoadArgument(0); il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Ceq); il.OpCode(ILOpCode.Pop); }
            if (damage == "inherited_write_before_base") { il.LoadArgument(0); il.LoadConstantI4(1); il.OpCode(ILOpCode.Stfld); il.Token(parentField); }
            if (damage != "missing")
            {
                if (damage == "null") il.OpCode(ILOpCode.Ldnull);
                else if (damage == "alias") il.LoadLocal(0);
                else il.LoadArgument(0);
                if (damage != "skip") { il.LoadArgument(1); il.LoadConstantI4(1); il.OpCode(ILOpCode.Add); }
                il.Call(damage == "skip" ? objectCtor : parentCtor);
            }
            if (damage == "repeat") { il.LoadArgument(0); il.LoadArgument(1); il.Call(parentCtor); }
            if (damage == "inherited_readonly_write") { il.LoadArgument(0); il.LoadConstantI4(1); il.OpCode(ILOpCode.Stfld); il.Token(parentField); }
            il.OpCode(ILOpCode.Ret);
        });
        builder.Type("Example", "Driver", baseType: objectBase);
        var root = builder.Method("Run", [0, 1, 8, 8], emit: il =>
        {
            il.LoadArgument(0); il.OpCode(ILOpCode.Newobj); il.Token(childCtor);
            il.OpCode(ILOpCode.Ldfld); il.Token(childField); il.OpCode(ILOpCode.Ret);
        });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("sequence.dll", builder.Build())).Value!;
        return ObjectTranslator.Translate(resolver, image, root, image);
    }

    [Fact]
    public void Valid_constructor_prefixes_do_not_allocate_diagnostic_names_per_statement()
    {
        var program = ConstructorSequence("").Program!;
        var binding = program.Bindings.Single(b => b.Owner.Token == 0x02000004 && b.Kind == ObjectMethodKind.Constructor);
        var original = program.Flow.Methods.Single(m => IrMethodId.From(m.Source) == binding.Method);
        var statements = ImmutableArray.CreateBuilder<IrStatement>();
        const int count = 8192;
        for (int i = 0; i < count; i++) statements.Add(new IrStatement.Define(new(i, IrType.Int32, 0, new IrExpression.Constant(i))));
        foreach (var statement in original.Blocks[0].Statements)
            statements.Add(statement switch
            {
                IrStatement.Define d => d with { Value = d.Value with { Id = d.Value.Id + count,
                    Expression = d.Value.Expression is IrExpression.Binary b ? b with { Left = b.Left + count, Right = b.Right + count } : d.Value.Expression } },
                IrStatement.StoreField s => s with { Receiver = s.Receiver + count, Value = s.Value + count },
                IrStatement.Invoke c => c with { Arguments = [.. c.Arguments.Select(a => a + count)] },
                _ => throw new InvalidOperationException(),
            });
        var expanded = original with { Source = original.Source with { Name = new string('x', 2048) },
            Blocks = [original.Blocks[0] with { Statements = statements.ToImmutable() }] };
        program = program with { Flow = program.Flow with { Methods = program.Flow.Methods.Replace(original, expanded) } };
        ObjectIrValidator.Validate(ConstructorSequence("").Program!);
        long before = GC.GetAllocatedBytesForCurrentThread();
        ObjectIrValidator.Validate(program);
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0, 16 * 1024 * 1024);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Composed_native_field_slots_have_an_exact_and_excess_budget(bool excess)
    {
        string hash = new('c', 64);
        var objectBase = new ObjectTypeId(hash, 0x02000002);
        var parent = new ObjectTypeId(hash, 0x02000003);
        var fields = Enumerable.Range(1, ObjectModelLimits.MaximumFields).Select(i =>
            new ObjectField(new(parent, 0x04000000 | i), "Value", FieldAttributes.Public, new ObjectStorage.Int32())).ToImmutableArray();
        ObjectTypeLayout Layout(ObjectTypeId id, ObjectTypeId? ancestor, ImmutableArray<ObjectField> declared) =>
            new(new("fixture", hash, id.Token, "SameName"), ancestor, TypeAttributes.Public, declared);
        var types = ImmutableArray.CreateBuilder<ObjectTypeLayout>();
        types.Add(Layout(objectBase, null, [])); types.Add(Layout(parent, objectBase, fields));
        var leaf = parent;
        for (int i = 0; i < 15 + (excess ? 1 : 0); i++)
        {
            var next = new ObjectTypeId(hash, 0x02000004 + i); types.Add(Layout(next, leaf, [])); leaf = next;
        }
        var entry = new IrMethod(new("fixture", hash, 0x06000001, "Run"), [], IrType.Int32,
            [new(0, 0, [], [new IrStatement.Define(new(0, IrType.Int32, 0, new IrExpression.Constant(1)))], new IrReturn(1, 0))], [IrType.Reference(leaf)]);
        var program = new ObjectIrProgram(new(IrMethodId.From(entry.Source), [entry], new([leaf], objectBase, types.ToImmutable())),
            [new(IrMethodId.From(entry.Source), leaf, ObjectMethodKind.Static, MethodAttributes.Public | MethodAttributes.Static, true)]);
        ReferenceIrValidator.Validate(program.Flow); // Detached declarations still fit their own budget.
        if (excess) Assert.Contains("native field-slot limit", Assert.Throws<ArgumentException>(() => ObjectIrValidator.Validate(program)).Message, StringComparison.Ordinal);
        else ObjectIrValidator.Validate(program);
        Assert.Equal(65536, ObjectNativeLimits.MaximumFieldSlots);
    }

    internal static ObjectIrProgram CrossImage()
    {
        var identity = new AssemblyIdentity("HierarchyCore", new Version(1, 0, 0, 0));
        var core = new ResolutionAssemblyBuilder(identity);
        var objectBase = core.Type("System", "Object");
        var objectCtor = core.Method(".ctor", [0x20, 0, 1], attributes: Constructor, emit: il => il.OpCode(ILOpCode.Ret));
        core.Type("Example", "SameName", baseType: objectBase);
        var parentField = core.Field("Value", 6, 8);
        core.Method(".ctor", [0x20, 0, 1], attributes: Constructor, emit: il =>
        {
            il.LoadArgument(0); il.Call(objectCtor); il.LoadArgument(0); il.LoadConstantI4(3);
            il.OpCode(ILOpCode.Stfld); il.Token(parentField); il.OpCode(ILOpCode.Ret);
        });
        var app = new ResolutionAssemblyBuilder(new("HierarchyApp", new Version(1, 0, 0, 0)));
        var coreRef = app.Reference(identity);
        var parent = app.TypeReference("Example", "SameName", coreRef);
        var parentCtor = app.Member(parent, ".ctor", 0x20, 0, 1);
        var inheritedField = app.Member(parent, "Value", 6, 8);
        app.Type("Example", "SameName", baseType: parent);
        var ownField = app.Field("Value", 6, 8);
        var ownCtor = app.Method(".ctor", [0x20, 0, 1], attributes: Constructor, emit: il =>
        {
            il.LoadArgument(0); il.Call(parentCtor); il.LoadArgument(0); il.LoadConstantI4(7);
            il.OpCode(ILOpCode.Stfld); il.Token(ownField); il.OpCode(ILOpCode.Ret);
        });
        app.Type("Example", "Driver", baseType: app.TypeReference("System", "Object", coreRef));
        var root = app.Method("Run", [0, 0, 8], emit: il =>
        {
            il.OpCode(ILOpCode.Newobj); il.Token(ownCtor); il.OpCode(ILOpCode.Dup);
            il.OpCode(ILOpCode.Ldfld); il.Token(inheritedField); il.LoadConstantI4(10); il.OpCode(ILOpCode.Mul);
            il.StoreLocal(0); il.OpCode(ILOpCode.Ldfld); il.Token(ownField); il.LoadLocal(0); il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Ret);
        }, locals: [7, 1, 8]);
        using var files = new ResolverFiles();
        string corePath = files.Write("core.dll", core.Build()), appPath = files.Write("app.dll", app.Build());
        using var resolver = files.Open("refs");
        var runtime = resolver.OpenInput(corePath).Value!; var image = resolver.OpenInput(appPath).Value!;
        var result = ObjectTranslator.Translate(resolver, image, root, runtime);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics)); return result.Program!;
    }
}
