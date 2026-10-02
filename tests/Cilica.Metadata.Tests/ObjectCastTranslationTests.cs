using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Metadata;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ObjectCastTranslationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Target_only_types_and_explicit_fault_edges_are_retained(bool cast)
    {
        var program = Raw(cast, "class").Program!;
        ObjectIrValidator.Validate(program);
        var method = Assert.Single(program.Flow.Methods);
        var operation = method.Blocks[0].Statements.OfType<IrStatement.Define>().Single(d => d.Value.Expression is IrExpression.CastClass or IrExpression.IsInstance);
        Assert.Equal(IrExceptionTarget.MethodExit, operation.Exceptional);
        Assert.Equal(0x02000003, operation.Value.Type.Class!.Token);
        Assert.Equal(2, program.Flow.Types.Types.Length);
        Assert.Contains(ObjectRootAssignment.Assign(program).Single().Slots, s => s.Kind == ObjectRootKind.Value && s.Index == operation.Value.Id);
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(program.Flow));
        Assert.Throws<ArgumentException>(() => CppEmitter.EmitProgram(new(program.Flow.Entry, program.Flow.Methods)));
    }

    [Theory]
    [InlineData("interface")]
    [InlineData("abstract")]
    [InlineData("generic")]
    [InlineData("value")]
    [InlineData("array")]
    [InlineData("constructed")]
    [InlineData("private")]
    [InlineData("missing")]
    [InlineData("nil")]
    [InlineData("integer")]
    public void Unsupported_or_malformed_targets_produce_diagnostics_without_partial_IR(string shape)
    {
        foreach (bool cast in new[] { false, true })
        {
            var result = Raw(cast, shape);
            Assert.Null(result.Program); Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Forged_type_tests_cannot_retag_values_or_skip_validation(bool cast)
    {
        var program = Raw(cast, "class").Program!;
        var method = program.Flow.Methods[0]; var block = method.Blocks[0];
        var original = block.Statements.OfType<IrStatement.Define>().Single(d => d.Value.Expression is IrExpression.CastClass or IrExpression.IsInstance);
        ObjectTypeId target = original.Value.Type.Class!;
        IrExpression Expression(ObjectTypeId type, int operand) => cast ? new IrExpression.CastClass(type, operand) : new IrExpression.IsInstance(type, operand);
        void Reject(IrStatement.Define changed)
        {
            var damaged = program with { Flow = program.Flow with { Methods = [method with { Blocks = [block with { Statements = block.Statements.Replace(original, changed) }] }] } };
            Assert.Throws<ArgumentException>(() => ObjectCppEmitter.EmitProgram(damaged));
        }
        Reject(original with { Exceptional = null });
        Reject(original with { Exceptional = (IrExceptionTarget)99 });
        Reject(original with { Value = original.Value with { Type = IrType.NullReference } });
        Reject(original with { Value = original.Value with { Type = IrType.Reference(program.Flow.Types.ObjectBase) } });
        Reject(original with { Value = original.Value with { Expression = Expression(target, -1) } });
        Reject(original with { Value = original.Value with { Expression = Expression(target, original.Value.Id) } });
        Reject(original with { Value = original.Value with { Expression = Expression(target with { ContentHash = new string('a', 64) }, 0) } });
        Reject(original with { Value = original.Value with { Expression = Expression(null!, 0) } });
        var integer = (IrStatement.Define)block.Statements[0];
        var badOperand = program with { Flow = program.Flow with { Methods = [method with { Blocks = [block with {
            Statements = block.Statements.Replace(integer, integer with { Value = integer.Value with { Type = IrType.Int32, Expression = new IrExpression.Constant(0) } }) }] }] } };
        Assert.Throws<ArgumentException>(() => ObjectIrValidator.Validate(badOperand));
    }

    private static ObjectTranslationResult Raw(bool cast, string shape)
    {
        var builder = new ResolutionAssemblyBuilder(new("CastTargets", new Version(1, 0, 0, 0)));
        var objectBase = builder.Type("System", "Object");
        EntityHandle parent = objectBase;
        if (shape == "value") parent = builder.Type("System", "ValueType", baseType: objectBase, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        EntityHandle target = builder.Type("Example", "Target", baseType: parent, arity: shape == "generic" ? 1 : 0,
            attributes: shape switch { "interface" => TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract,
                "abstract" => TypeAttributes.Public | TypeAttributes.Abstract, "private" => TypeAttributes.NotPublic, _ => TypeAttributes.Public });
        if (shape == "array") target = builder.Specification(0x1d, 0x08);
        if (shape == "constructed") target = builder.Specification(0x15, 0x12, (byte)(MetadataTokens.GetRowNumber(target) * 4), 1, 0x08);
        if (shape == "missing") target = MetadataTokens.TypeDefinitionHandle(999);
        if (shape == "nil") target = MetadataTokens.TypeDefinitionHandle(0);
        builder.Type("Example", "Driver", baseType: objectBase);
        var root = builder.Method("Run", [0, 0, 8], emit: il =>
        {
            if (shape == "integer") il.LoadConstantI4(0); else il.OpCode(ILOpCode.Ldnull);
            il.OpCode(cast ? ILOpCode.Castclass : ILOpCode.Isinst); il.Token(target);
            il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Ceq); il.OpCode(ILOpCode.Ret);
        });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("targets.dll", builder.Build())).Value!;
        return ObjectTranslator.Translate(resolver, image, root, image);
    }

    internal static ObjectIrProgram CrossImage(bool cast)
    {
        const MethodAttributes constructor = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
        var identity = new Cilica.Metadata.Resolution.AssemblyIdentity("CastCore", new Version(1, 0, 0, 0));
        var core = new ResolutionAssemblyBuilder(identity);
        var objectBase = core.Type("System", "Object");
        core.Method(".ctor", [0x20, 0, 1], attributes: constructor, emit: il => il.OpCode(ILOpCode.Ret));
        core.Type("Example", "SameName", baseType: objectBase);
        var app = new ResolutionAssemblyBuilder(new("CastApp", new Version(1, 0, 0, 0)));
        var coreRef = app.Reference(identity);
        var objectRef = app.TypeReference("System", "Object", coreRef);
        var target = app.TypeReference("Example", "SameName", coreRef);
        var baseConstructor = app.Member(objectRef, ".ctor", 0x20, 0, 1);
        app.Type("Example", "Padding", baseType: objectRef);
        app.Type("Example", "SameName", baseType: objectRef);
        var ownConstructor = app.Method(".ctor", [0x20, 0, 1], attributes: constructor, emit: il =>
        { il.LoadArgument(0); il.Call(baseConstructor); il.OpCode(ILOpCode.Ret); });
        app.Type("Example", "Driver", baseType: objectRef);
        var entry = app.Method("Run", [0, 0, 8], emit: il =>
        {
            il.OpCode(ILOpCode.Newobj); il.Token(ownConstructor);
            il.OpCode(cast ? ILOpCode.Castclass : ILOpCode.Isinst); il.Token(target);
            il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Ceq); il.OpCode(ILOpCode.Ret);
        });
        using var files = new ResolverFiles();
        string corePath = files.Write("core.dll", core.Build()), appPath = files.Write("app.dll", app.Build());
        using var resolver = files.Open("refs");
        var runtime = resolver.OpenInput(corePath).Value!;
        var image = resolver.OpenInput(appPath).Value!;
        var result = ObjectTranslator.Translate(resolver, image, entry, runtime);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var duplicates = result.Program!.Flow.Types.Types.Where(t => t.Source.Token == 0x02000003).ToArray();
        Assert.Equal(2, duplicates.Length); Assert.Equal(duplicates[0].Source.Name, duplicates[1].Source.Name);
        Assert.NotEqual(duplicates[0].Source.ContentHash, duplicates[1].Source.ContentHash);
        return result.Program;
    }
}
