using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ObjectControlFlowTests
{
    internal static ObjectTranslationResult Translate(ObjectFlowFixtures.Fixture fixture)
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("flow.dll", fixture.Builder.Build())).Value!;
        return ObjectTranslator.Translate(resolver, image, fixture.Root, image);
    }

    internal static ObjectIrProgram Authored(string name)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var image = resolver.OpenInput(typeof(ObjectFlowProbe).Assembly.Location).Value!; var runtime = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var root = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ObjectFlowProbe).GetMethod(name)!.MetadataToken);
        var result = ObjectTranslator.Translate(resolver, image, root, runtime);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics)); return result.Program!;
    }

    [Theory]
    [InlineData("join")]
    [InlineData("widen_loop")]
    [InlineData("widen_call")]
    [InlineData("swap")]
    [InlineData("siblings")]
    public void Typed_joins_and_backedges_converge_and_assign_distinct_input_and_value_roots(string kind)
    {
        var result = Translate(ObjectFlowFixtures.Create(kind)); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!; var entry = program.Flow.Methods.Single(m => IrMethodId.From(m.Source) == program.Flow.Entry);
        Assert.Contains(entry.Blocks, b => b.Inputs.Any(t => t.Kind == IrTypeKind.Reference));
        if (kind == "siblings") Assert.Contains(entry.Blocks, b => b.Inputs.Contains(IrType.Reference(program.Flow.Types.ObjectBase)));
        if (kind == "widen_loop") Assert.DoesNotContain(entry.Blocks.SelectMany(b => b.Inputs), t => t == IrType.NullReference);
        var plan = ObjectRootAssignment.Assign(program).Single(p => p.Method == program.Flow.Entry);
        Assert.Contains(plan.Slots, s => s.Kind == ObjectRootKind.BlockInput);
        Assert.Equal(plan.Slots.Length, plan.Slots.Select(s => s.Slot).Distinct().Count());
        Assert.Equal(plan.Slots.Length, plan.Slots.Select(s => (s.Kind, s.Block, s.Index)).Distinct().Count());
        Assert.NotEmpty(ObjectCppEmitter.EmitProgram(program).Text);
    }

    [Theory]
    [InlineData("mixed")]
    [InlineData("depth")]
    [InlineData("bad_sibling_use")]
    [InlineData("starg")]
    [InlineData("ctor_branch")]
    [InlineData("ordered")]
    public void Invalid_join_or_widened_receiver_use_returns_a_diagnostic_without_partial_IR(string kind)
    {
        var result = Translate(ObjectFlowFixtures.Create(kind)); Assert.False(result.Success); Assert.Null(result.Program);
        Assert.NotEmpty(result.Diagnostics); Assert.All(result.Diagnostics, d => Assert.False(string.IsNullOrWhiteSpace(d.Location)));
    }

    [Theory]
    [InlineData("budget", true)]
    [InlineData("budget_excess", false)]
    public void Stack_inference_work_is_bounded_even_when_final_IR_fits_existing_limits(string kind, bool accepted)
    {
        var result = Translate(ObjectFlowFixtures.Create(kind));
        if (accepted) { Assert.True(result.Success, string.Join("; ", result.Diagnostics)); ObjectIrValidator.Validate(result.Program!); }
        else
        {
            Assert.False(result.Success); var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(DiagnosticIds.TranslationLimit, diagnostic.Id); Assert.Contains("stack-inference work", diagnostic.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Revisited_call_sites_do_not_charge_the_call_copy_limit_twice()
    {
        var b = new ResolutionAssemblyBuilder(new("RevisitedCalls", new Version(1, 0, 0, 0)));
        var objectBase = b.Type("System", "Object"); var node = b.Type("Test", "Node", baseType: objectBase);
        byte encoded = checked((byte)(MetadataTokens.GetRowNumber(node) << 2));
        byte[] signature = [0, 32, 0x12, encoded, .. Enumerable.Range(0, 32).SelectMany(_ => new byte[] { 0x12, encoded })];
        var identity = b.Method("Identity", signature, emit: il => { il.LoadArgument(0); il.OpCode(ILOpCode.Ret); });
        b.Type("Test", "Driver", baseType: objectBase);
        var root = b.Method("Run", [0, 1, 8, 8], maxStack: 33, emit: il =>
        {
            var loop = il.DefineLabel(); il.OpCode(ILOpCode.Ldnull); il.Branch(ILOpCode.Br, loop);
            il.MarkLabel(loop);
            for (int call = 0; call < 2047; call++)
            {
                for (int argument = 1; argument < 32; argument++) il.OpCode(ILOpCode.Dup);
                il.Call(identity);
            }
            il.LoadArgument(0); il.Branch(ILOpCode.Brtrue, loop);
            il.OpCode(ILOpCode.Pop); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        var result = Translate(new(b, root)); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!; ObjectIrValidator.Validate(program);
        Assert.Equal(65504, program.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(block => block.Values)
            .Select(v => v.Expression).OfType<IrExpression.Call>().Sum(c => c.Arguments.Length));
    }

    [Theory]
    [InlineData(nameof(ObjectFlowProbe.Chain))]
    [InlineData(nameof(ObjectFlowProbe.Choose))]
    [InlineData(nameof(ObjectFlowProbe.Identity))]
    [InlineData(nameof(ObjectFlowProbe.FaultLoop))]
    [InlineData(nameof(ObjectFlowProbe.ChooseObject))]
    public void Authored_control_flow_translates_with_existing_constructor_and_access_rules(string name)
    {
        var program = Authored(name); ObjectIrValidator.Validate(program); Assert.NotEmpty(ObjectCppEmitter.EmitProgram(program).Text);
    }
}
