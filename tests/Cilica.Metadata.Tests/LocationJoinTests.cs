using System;
using System.Linq;
using System.Collections.Immutable;
using System.IO;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class LocationJoinTests
{
    private static readonly string[] Names = ["Local", "Parameter", "BorrowedParameter", "ReferenceValue", "Faults", "UpdatedContents", "HeapAddress", "ArrayAddress", "SelectLocal", "SelectHeap", "SelectArray", "SelectMixed", "SelectValue", "Readonly", "Wide", "ReferenceSlot", "FieldFaults", "ArrayFaults", "Static", "Empty", "Constructor"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(LocationJoinProbe), Names));
    [Theory] [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    [InlineData(false,4)] [InlineData(true,4)]
    public async Task Selected_locations_keep_aliases_owners_and_exception_order(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(LocationJoinProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 5).Take(5).ToArray());
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Same_and_distinct_slot_joins_execute(bool optimized)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        foreach (string kind in new[] { "same", "different-local", "different-kind" })
        {
            var builder = new ResolutionAssemblyBuilder(new("LocationJoin" + kind, new Version(1, 0, 0, 0)));
            builder.Type("Test", "Root");
            var root = builder.Method("Run", [0, 2, 8, 8, 8], locals: [7, 2, 8, 8], emit: il => {
                var other = il.DefineLabel(); var join = il.DefineLabel();
                il.LoadArgument(0); il.StoreLocal(0); il.LoadArgument(0); il.Branch(ILOpCode.Brtrue, other);
                il.LoadLocalAddress(0); il.Branch(ILOpCode.Br, join); il.MarkLabel(other);
                if (kind == "different-kind") il.LoadArgumentAddress(0);
                else il.LoadLocalAddress(kind == "different-local" ? 1 : 0);
                il.MarkLabel(join); il.LoadArgument(1); il.OpCode(ILOpCode.Stind_i4); il.LoadLocal(0); il.OpCode(ILOpCode.Ret);
            });
            var input = scope.OpenInput(files.Write(kind + ".dll", builder.Build())).Value!;
            var result = ValueTranslator.TranslateIntegerObjects(scope, input, root, implementation);
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));
            await IntegerObjectTranslationTests.VerifyTranslated(optimized, [(kind.Replace('-', '_'), result.Program!, (int a, int b) => kind == "same" || a == 0 ? b : a)]);
            var legacy = ValueTranslator.TranslateExceptions(scope, input, root, implementation);
            Assert.False(legacy.Success); Assert.Contains(legacy.Diagnostics, d => d.Message.Contains("cannot cross block joins", StringComparison.Ordinal));
        }
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Self_edges_swap_two_locations_without_overwriting_sources(bool optimized)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var builder = new ResolutionAssemblyBuilder(new("SwapLocationInputs", new Version(1, 0, 0, 0)));
        builder.Type("Test", "Root");
        var root = builder.Method("Run", [0, 2, 8, 8, 8], locals: [7, 5, 8, 8, 8, 0x10, 8, 0x10, 8], emit: il => {
            var loop = il.DefineLabel();
            il.LoadArgument(0); il.StoreLocal(0); il.LoadArgument(1); il.StoreLocal(1);
            il.LoadConstantI4(3); il.StoreLocal(2); il.LoadLocalAddress(0); il.LoadLocalAddress(1);
            il.Branch(ILOpCode.Br, loop); il.MarkLabel(loop);
            il.StoreLocal(4); il.StoreLocal(3);
            il.LoadLocal(3); il.OpCode(ILOpCode.Dup); il.OpCode(ILOpCode.Ldind_i4); il.LoadConstantI4(1); il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Stind_i4);
            il.LoadLocal(4); il.OpCode(ILOpCode.Dup); il.OpCode(ILOpCode.Ldind_i4); il.LoadConstantI4(3); il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Stind_i4);
            il.LoadLocal(2); il.LoadConstantI4(1); il.OpCode(ILOpCode.Sub); il.StoreLocal(2);
            il.LoadLocal(4); il.LoadLocal(3); il.LoadLocal(2); il.Branch(ILOpCode.Brtrue, loop);
            il.OpCode(ILOpCode.Pop); il.OpCode(ILOpCode.Pop);
            il.LoadLocal(0); il.LoadLocal(1); il.LoadConstantI4(3); il.OpCode(ILOpCode.Mul); il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Ret);
        });
        var input = scope.OpenInput(files.Write("swap.dll", builder.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, root, implementation);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("Swap", result.Program!, (int a, int b) => unchecked(a + 3 * b + 26))]);
    }
    [Fact]
    public async Task Lowered_location_inputs_have_distinct_owner_roots()
    {
        int count = 0;
        foreach (var program in await Programs.Value)
        {
            var scalar = ValueScalarLowering.LowerExceptions(program);
            ScalarManagedExceptionIrValidator.Validate(scalar);
            var roots = ObjectRootAssignment.AssignExceptions(scalar);
            foreach (var method in scalar.Flow.Flow.Flow.Flow.Flow.Methods)
                foreach (var block in method.Blocks)
                    for (int i = 0; i < block.Inputs.Length; ++i)
                        if (block.Inputs[i].Kind == IrTypeKind.ManagedLocation)
                        {
                            ++count;
                            Assert.Contains(roots.Single(r => r.Method == IrMethodId.From(method.Source)).Slots,
                                slot => slot.Kind == ObjectRootKind.BlockInput && slot.Block == block.Id && slot.Index == i && slot.LocationOwner);
                        }
        }
        Assert.True(count > 20);
    }
    [Theory] [InlineData("readonly")] [InlineData("element")] [InlineData("nested")]
    public async Task Both_ir_stages_reject_invalid_location_join_types(string mutation)
    {
        var program = (await Programs.Value)[13]; // Readonly merges a readonly field with a mutable local.
        ImmutableArray<IrMethod> Alter(ImmutableArray<IrMethod> methods) => [.. methods.Select(m => m with {
            Blocks = [.. m.Blocks.Select(b => b with {
                Inputs = [.. b.Inputs.Select(Change)],
                Statements = [.. b.Statements.Select(s => s is IrStatement.Define {Value.Expression: IrExpression.StackInput} d
                    ? d with {Value = d.Value with {Type = Change(d.Value.Type)}} : s)]
            })]
        })];
        IrType Change(IrType type) => type.Kind != IrTypeKind.ManagedLocation ? type : mutation switch {
            "readonly" => type with {ReadOnly = false},
            "element" => type with {Element = IrType.Int64},
            _ => type with {Element = IrType.Location(IrType.Int32)}
        };
        Assert.Contains(program.Flow.Flow.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Inputs), t => t.Kind == IrTypeKind.ManagedLocation && t.ReadOnly);
        var flow = program.Flow.Flow.Flow;
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program with {Flow = program.Flow with {Flow = new(flow with {Methods = Alter(flow.Methods)})}}));
        var scalar = ValueScalarLowering.LowerExceptions(program); var boxes = scalar.Flow.Flow; var objects = boxes.Flow.Flow;
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(scalar with {Flow = scalar.Flow with {Flow = boxes with {Flow = boxes.Flow with {Flow = objects with {Flow = objects.Flow with {Methods = Alter(objects.Flow.Methods)}}}}}}));
    }
}
