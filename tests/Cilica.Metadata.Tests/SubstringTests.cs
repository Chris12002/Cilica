using System;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.Emit;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class SubstringTests
{
    private static readonly string[] Names = ["Tail", "Range", "Empty", "Identity", "NullReceiver", "Composed"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(SubstringProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    public async Task Substrings_preserve_code_units_identity_and_range_faults(bool optimized, int group)
    {
        var programs = await Programs.Value;
        int[] bounds = [int.MinValue, -7, -1, 0, 1, 2, 3, 5, 6, 7, int.MaxValue];
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
                (name, programs[i], typeof(SubstringProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray(),
                pairs: (from a in bounds from b in bounds select (a,b)).ToArray());
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    [Theory]
    [InlineData("virtual")] [InlineData("constructor")] [InlineData("initializer")]
    public async Task Both_stages_reject_invalid_string_instance_bindings(string corruption)
    {
        var program = (await Programs.Value)[0];
        var owner = program.Flow.Flow.Flow.Types.References.String;
        ImmutableArray<ObjectMethodBinding> Alter(ImmutableArray<ObjectMethodBinding> bindings)
        {
            var target = bindings.First(b => b.Owner == owner && b.Kind == ObjectMethodKind.Instance);
            var replacement = corruption switch {
                "virtual" => target with { Attributes = target.Attributes | MethodAttributes.Virtual },
                "constructor" => target with { Kind = ObjectMethodKind.Constructor },
                _ => target with { TypeInitializerName = "System.String" } };
            return bindings.Replace(target, replacement);
        }
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program with { Flow = program.Flow with { Bindings = Alter(program.Flow.Bindings) } }));
        var scalar = ValueScalarLowering.LowerExceptions(program); var boxes = scalar.Flow.Flow;
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(scalar with { Flow = scalar.Flow with {
            Flow = boxes with { Flow = boxes.Flow with { Flow = boxes.Flow.Flow with { Bindings = Alter(boxes.Flow.Flow.Bindings) } } } } }));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Direct_call_preserves_null_receiver_shortcut(bool optimized)
    {
        // The oracle emits the same unusual CLI call form explicitly: C# uses
        // callvirt for a possibly-null string and cannot express this case.
        var oracleMethod = new DynamicMethod("DirectSubstring", typeof(int), [typeof(int), typeof(int)]);
        var oracleIl = oracleMethod.GetILGenerator();
        var result = oracleIl.DeclareLocal(typeof(int));
        var end = oracleIl.BeginExceptionBlock();
        oracleIl.Emit(OpCodes.Ldnull); oracleIl.Emit(OpCodes.Ldarg_0);
        oracleIl.Emit(OpCodes.Call, typeof(string).GetMethod("Substring", [typeof(int)])!);
        oracleIl.Emit(OpCodes.Ldnull); oracleIl.Emit(OpCodes.Ceq); oracleIl.Emit(OpCodes.Stloc, result); oracleIl.Emit(OpCodes.Leave, end);
        oracleIl.BeginCatchBlock(typeof(NullReferenceException)); oracleIl.Emit(OpCodes.Pop); oracleIl.Emit(OpCodes.Ldc_I4, -11);
        oracleIl.Emit(OpCodes.Stloc, result); oracleIl.Emit(OpCodes.Leave, end); oracleIl.EndExceptionBlock();
        oracleIl.Emit(OpCodes.Ldloc, result); oracleIl.Emit(OpCodes.Ret);
        var oracle = oracleMethod.CreateDelegate<Func<int,int,int>>();
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var b = new ResolutionAssemblyBuilder(new("DirectSubstring", new Version(1,0,0,0)));
        var core = b.Reference(implementation.Identity);
        var text = b.TypeReference("System", "String", core);
        var fault = b.TypeReference("System", "NullReferenceException", core);
        var call = b.Member(text, "Substring", 0x20, 1, 0x0e, 8);
        b.Type("Test", "Caller");
        var root = b.Method("Run", [0,2,8,8,8], locals: [7,1,8], emit: il => {
            var start = il.DefineLabel(); var handler = il.DefineLabel(); var done = il.DefineLabel();
            il.MarkLabel(start); il.OpCode(ILOpCode.Ldnull); il.LoadArgument(0); il.Call(call);
            il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Ceq); il.StoreLocal(0); il.Branch(ILOpCode.Leave, done);
            il.MarkLabel(handler); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(-11); il.StoreLocal(0); il.Branch(ILOpCode.Leave, done);
            il.MarkLabel(done); il.LoadLocal(0); il.OpCode(ILOpCode.Ret);
            il.ControlFlowBuilder!.AddCatchRegion(start, handler, handler, done, fault);
        });
        var input = scope.OpenInput(files.Write("caller.dll", b.Build())).Value!;
        var translated = ValueTranslator.TranslateIntegerObjects(scope, input, root, implementation);
        Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("DirectSubstring", translated.Program!, oracle)]);
    }
}
