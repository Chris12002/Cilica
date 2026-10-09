using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ConstructorPrefixTests
{
    private static readonly string[] Names = ["Conditional", "Throw", "CachedDelegate", "Chain", "Reference", "Handlers"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ConstructorPrefixProbe), Names));
    [Theory] [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    public async Task Branched_initializers_preserve_fault_order_roots_and_body_handlers(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ConstructorPrefixProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).Where(p => p.name != "CachedDelegate").ToArray());
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Cached_prefix_delegates_retain_exactly_their_two_static_objects(bool optimized)
    {
        // The C# closure singleton and cached Func survive; constructed instances do not.
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("CachedDelegate", (await Programs.Value)[2], ConstructorPrefixProbe.CachedDelegate)], expectedStaticRoots: 2);
    }
    [Theory] [InlineData("missing")] [InlineData("repeat")] [InlineData("receiver-alias")] [InlineData("protected-prefix")]
    public async Task Detached_initialization_facts_remain_checked_in_both_ir_stages(string damage)
    {
        var original = (await Programs.Value)[damage == "protected-prefix" ? 5 : 0];
        var scalar = ValueScalarLowering.LowerExceptions(original);
        IrMethod Change(IrMethod method)
        {
            if (damage == "protected-prefix") return method with { ExceptionRegions = method.ExceptionRegions.SetItem(0, method.ExceptionRegions[0] with { TryStart = 0 }) };
            var block = method.Blocks.Single(b => b.Statements.OfType<IrStatement.Invoke>().Any());
            var call = block.Statements.OfType<IrStatement.Invoke>().Single(); var statements = block.Statements;
            if (damage == "missing") statements = statements.Remove(call);
            if (damage == "repeat") statements = statements.Insert(statements.IndexOf(call) + 1, call);
            if (damage == "receiver-alias")
            {
                var receiver = statements.OfType<IrStatement.Define>().Single(d => d.Value.Id == call.Arguments[0]);
                statements = statements.Replace(receiver, receiver with { Value = receiver.Value with { Expression = new IrExpression.Local(method.Locals.Length) } });
                method = method with { Locals = method.Locals.Add(receiver.Value.Type) };
            }
            return method with { Blocks = method.Blocks.Replace(block, block with { Statements = statements }) };
        }
        string owner = damage == "protected-prefix" ? "PrefixHandler::.ctor" : "PrefixConditional::.ctor";
        var flow = original.Flow.Flow.Flow; var method = flow.Methods.Single(m => m.Source.Name.EndsWith(owner, StringComparison.Ordinal));
        var invalid = original with { Flow = original.Flow with { Flow = original.Flow.Flow with { Flow = flow with { Methods = flow.Methods.Replace(method, Change(method)) } } } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid));
        var objects = scalar.Flow.Flow.Flow.Flow; method = objects.Flow.Methods.Single(m => m.Source.Name.EndsWith(owner, StringComparison.Ordinal));
        var changed = objects with { Flow = objects.Flow with { Methods = objects.Flow.Methods.Replace(method, Change(method)) } };
        var invalidScalar = scalar with { Flow = scalar.Flow with { Flow = scalar.Flow.Flow with { Flow = scalar.Flow.Flow.Flow with { Flow = changed } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar));
    }
    [Theory]
    [InlineData("good", true)] [InlineData("loop", true)] [InlineData("mixed", false)] [InlineData("alias", false)]
    [InlineData("read", false)] [InlineData("escape", false)] [InlineData("test", false)] [InlineData("repeat", false)]
    public void Uninitialized_receivers_cannot_escape_or_skip_initialization(string damage, bool success)
    {
        var result = Raw(damage);
        Assert.True(result.Success == success, string.Join("; ", result.Diagnostics));
        if (success) ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program!));
        else Assert.Contains(result.Diagnostics, d => d.Message.Contains("constructor", StringComparison.Ordinal));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Bounded_preinitialization_branches_and_loops_execute(bool optimized)
    {
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, new[] { "good", "loop" }.Select(name =>
            (name, Raw(name).Program!, (Func<int, int, int>)((a, b) => unchecked(a + b)))).ToArray());
    }
    private static ManagedExceptionTranslationResult Raw(string damage)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("ConstructorPrefix", new Version(1, 0, 0, 0)));
        var obj = b.TypeReference("System", "Object", b.Reference(library.Identity));
        var baseCtor = b.Member(obj, ".ctor", 0x20, 0, 1);
        b.Type("Test", "Cell", baseType: obj);
        var field = b.FieldWithAttributes("Value", FieldAttributes.Public, 6, 8);
        var escaped = b.FieldWithAttributes("Escaped", FieldAttributes.Public | FieldAttributes.Static, 6, 0x1c);
        var ctor = b.Method(".ctor", [0x20, 1, 1, 8], attributes: MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            locals: [7, 2, 0x1c, 8], emit: il => {
                if (damage == "loop")
                {
                    var body = il.DefineLabel(); var check = il.DefineLabel();
                    il.LoadArgument(1); il.LoadConstantI4(3); il.OpCode(ILOpCode.And); il.StoreLocal(1); il.Branch(ILOpCode.Br, check);
                    il.MarkLabel(body); il.LoadLocal(1); il.LoadConstantI4(1); il.OpCode(ILOpCode.Sub); il.StoreLocal(1);
                    il.MarkLabel(check); il.LoadLocal(1); il.Branch(ILOpCode.Brtrue, body);
                    il.LoadArgument(0); il.Call(baseCtor); il.OpCode(ILOpCode.Ret); return;
                }
                var initialize = il.DefineLabel(); var done = il.DefineLabel();
                if (damage == "alias") { il.LoadArgument(0); il.StoreLocal(0); }
                if (damage == "read") { il.LoadArgument(0); il.OpCode(ILOpCode.Ldfld); il.Token(field); il.OpCode(ILOpCode.Pop); }
                if (damage == "escape") { il.LoadArgument(0); il.OpCode(ILOpCode.Stsfld); il.Token(escaped); }
                if (damage == "repeat") { il.LoadArgument(0); il.Call(baseCtor); }
                il.LoadArgument(damage == "test" ? 0 : 1); il.Branch(ILOpCode.Brfalse, damage == "mixed" ? done : initialize);
                il.LoadConstantI4(7); il.OpCode(ILOpCode.Pop);
                il.MarkLabel(initialize); il.LoadArgument(0); il.Call(baseCtor);
                il.MarkLabel(done); il.OpCode(ILOpCode.Ret);
            });
        b.Type("Test", "Root"); var root = b.Method("Run", [0, 2, 8, 8, 8], emit: il => {
            il.LoadArgument(0); il.OpCode(ILOpCode.Newobj); il.Token(ctor); il.OpCode(ILOpCode.Pop);
            il.LoadArgument(0); il.LoadArgument(1); il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Ret);
        });
        var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        return ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
    }
}
