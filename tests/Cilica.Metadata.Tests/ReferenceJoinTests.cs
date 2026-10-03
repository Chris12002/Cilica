using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ReferenceJoinTests
{
    [Fact]
    public void Same_named_equal_token_contracts_in_different_images_join_only_at_Object()
    {
        const TypeAttributes flags = TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract;
        var identity = new AssemblyIdentity("JoinCore", new Version(1, 0, 0, 0)); var core = new ResolutionAssemblyBuilder(identity);
        var coreObject = core.Type("System", "Object"); var first = core.Type("Example", "SameName", attributes: flags);
        core.Type("Example", "Provider", baseType: coreObject);
        core.Method("Get", [0, 0, .. ReferenceJoinFixtures.Ref(first)], emit: il => { il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Ret); });
        var app = new ResolutionAssemblyBuilder(new("JoinApp", new Version(1, 0, 0, 0)));
        var scope = app.Reference(identity); var obj = app.TypeReference("System", "Object", scope);
        app.Type("Example", "Padding", baseType: obj); var second = app.Type("Example", "SameName", attributes: flags);
        Assert.Equal(MetadataTokens.GetToken(first), MetadataTokens.GetToken(second));
        var provider = app.TypeReference("Example", "Provider", scope); var contract = app.TypeReference("Example", "SameName", scope);
        var firstSource = app.Member(provider, "Get", 0, 0, 0x12, (byte)((MetadataTokens.GetRowNumber(contract) << 2) | 1));
        app.Type("Example", "Driver", baseType: obj);
        var secondSource = app.Method("Get", [0, 0, .. ReferenceJoinFixtures.Ref(second)], emit: il => { il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Ret); });
        var entry = app.Method("Run", [0, 1, 8, 8], emit: il =>
        {
            var other = il.DefineLabel(); var join = il.DefineLabel();
            il.LoadArgument(0); il.Branch(ILOpCode.Brtrue, other);
            il.Call(firstSource); il.Branch(ILOpCode.Br, join);
            il.MarkLabel(other); il.Call(secondSource); il.Branch(ILOpCode.Br, join);
            il.MarkLabel(join); il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Ceq); il.OpCode(ILOpCode.Ret);
        });
        using var files = new ResolverFiles();
        var corePath = files.Write("core.dll", core.Build()); var appPath = files.Write("app.dll", app.Build());
        using var resolver = files.Open("refs");
        var runtime = resolver.OpenInput(corePath).Value!;
        var image = resolver.OpenInput(appPath).Value!;
        var result = ObjectTranslator.Translate(resolver, image, entry, runtime);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!;
        Assert.Equal(2, program.Flow.Types.Interfaces.Length);
        Assert.Contains(program.Flow.Methods.Single(m => IrMethodId.From(m.Source) == program.Flow.Entry).Blocks.SelectMany(b => b.Inputs),
            t => t == IrType.Reference(program.Flow.Types.ObjectBase));
    }

    [Theory]
    [InlineData("ABC")]
    [InlineData("ACB")]
    [InlineData("BAC")]
    [InlineData("BCA")]
    [InlineData("CAB")]
    [InlineData("CBA")]
    public void Three_way_joins_wait_for_all_predecessors_and_are_independent_of_visit_order(string order)
    {
        foreach (string shape in new[] { "three", "loop", "parents", "class_interface" })
        {
            var result = ObjectControlFlowTests.Translate(ReferenceJoinFixtures.Create(shape, order));
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));
            var program = result.Program!;
            var root = program.Flow.Types.Interfaces.Single(i => i.Source.Name.EndsWith("IRoot", StringComparison.Ordinal));
            var entry = program.Flow.Methods.Single(m => IrMethodId.From(m.Source) == program.Flow.Entry);
            Assert.Contains(entry.Blocks.SelectMany(b => b.Inputs), t => t == IrType.Reference(ObjectTypeId.From(root.Source)));
            ObjectIrValidator.Validate(program);
        }
    }

    [Theory]
    [InlineData("ambiguous")]
    [InlineData("narrowing")]
    public void Ambiguous_or_invalid_joins_fail_without_partial_IR(string shape)
    {
        var result = ObjectControlFlowTests.Translate(ReferenceJoinFixtures.Create(shape));
        Assert.Null(result.Program); var diagnostic = Assert.Single(result.Diagnostics);
        Assert.NotNull(diagnostic.Location);
        if (shape == "ambiguous") Assert.Contains("explicitly typed local", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_explicit_Object_source_prevents_interface_narrowing()
    {
        var result = ObjectControlFlowTests.Translate(ReferenceJoinFixtures.Create("object"));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var entry = result.Program!.Flow.Methods.Single(m => IrMethodId.From(m.Source) == result.Program.Flow.Entry);
        Assert.Contains(entry.Blocks.SelectMany(b => b.Inputs), t => t == IrType.Reference(result.Program.Flow.Types.ObjectBase));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reference_set_work_uses_the_shared_inference_budget(bool excess)
    {
        var result = ObjectControlFlowTests.Translate(ReferenceJoinFixtures.Budget(excess));
        if (!excess) Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        else
        {
            Assert.Null(result.Program); var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(DiagnosticIds.TranslationLimit, diagnostic.Id);
            Assert.Contains("stack-inference work", diagnostic.Message, StringComparison.Ordinal);
            Assert.Contains("Reachable via:", diagnostic.Message, StringComparison.Ordinal);
        }
    }

    internal static ObjectIrProgram Authored(string name)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var image = resolver.OpenInput(typeof(ReferenceJoinProbe).Assembly.Location).Value!;
        var runtime = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result = ObjectTranslator.Translate(resolver, image,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ReferenceJoinProbe).GetMethod(name)!.MetadataToken), runtime);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics)); return result.Program!;
    }

    [Theory]
    [InlineData(nameof(ReferenceJoinProbe.Class), typeof(JoinBase))]
    [InlineData(nameof(ReferenceJoinProbe.Interface), typeof(IJoinRead))]
    [InlineData(nameof(ReferenceJoinProbe.Parents), typeof(IJoinRead))]
    [InlineData(nameof(ReferenceJoinProbe.Nullable), typeof(IJoinRead))]
    [InlineData(nameof(ReferenceJoinProbe.Rooted), typeof(IJoinRead))]
    [InlineData(nameof(ReferenceJoinProbe.Field), typeof(JoinBase))]
    public void Branches_infer_the_common_class_or_interface_without_losing_identity(string name, Type expected)
    {
        var program = Authored(name);
        var entry = program.Flow.Methods.Single(m => IrMethodId.From(m.Source) == program.Flow.Entry);
        Assert.Contains(entry.Blocks.SelectMany(b => b.Inputs), t => t.Class?.Token == expected.MetadataToken);
        ObjectIrValidator.Validate(program); ObjectCppEmitter.EmitProgram(program);
        var roots = ObjectRootAssignment.Assign(program).Single(p => p.Method == program.Flow.Entry);
        Assert.Contains(roots.Slots, s => s.Kind == ObjectRootKind.BlockInput);
    }
}
