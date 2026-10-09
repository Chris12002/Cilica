using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Metadata.Resolution;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ReadOnlyReceiverTests
{
    private static readonly string[] Names = ["StructReceiver", "MemberReceiver", "DefensiveCopy", "BoxedReceiver", "Primitive", "Nullable", "GuidFields", "ClosedGeneric", "NullReceiver"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ReadOnlyReceiverProbe), Names));
    [Theory] [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)] [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Readonly_receivers_keep_identity_borrows_faults_and_defensive_copies(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ReadOnlyReceiverProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void Declaration_and_body_readers_preserve_type_and_method_markers(bool onType)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(ReadOnlyValue).Assembly.Location)!]).Value!;
        var assembly = resolver.OpenInput(typeof(ReadOnlyValue).Assembly.Location).Value!;
        var method = (onType ? typeof(ReadOnlyValue) : typeof(MixedReadOnlyValue)).GetMethod("Read")!;
        var handle = (MethodDefinitionHandle)MetadataTokens.EntityHandle(method.MetadataToken);
        var body = resolver.ReadMethodInput(assembly, handle); var declaration = resolver.ReadMethodDeclarationInput(assembly, handle);
        Assert.True(body.Success, string.Join("; ", body.Diagnostics)); Assert.True(declaration.Success, string.Join("; ", declaration.Diagnostics));
        Assert.True(body.Value!.ReadOnlyReceiver); Assert.True(declaration.Value!.ReadOnlyReceiver);
        int used = declaration.Value.MetadataMembersRead;
        Assert.True(resolver.ReadMethodDeclarationInput(assembly, handle, new(MaximumMembers: used)).Success);
        Assert.False(resolver.ReadMethodDeclarationInput(assembly, handle, new(MaximumMembers: used - 1)).Success);
        used = body.Value.MetadataMembersRead;
        Assert.True(resolver.ReadMethodInput(assembly, handle, new(MaximumInitializerMembers: used)).Success);
        Assert.False(resolver.ReadMethodInput(assembly, handle, new(MaximumInitializerMembers: used - 1)).Success);
    }
    [Fact]
    public async Task Readonly_binding_cannot_be_detached_from_its_receiver_signature()
    {
        var program = (await Programs.Value)[0];
        var binding = program.Flow.Bindings.First(b => b.ReadOnlyReceiver && b.Kind == ObjectMethodKind.ValueInstance);
        var damaged = program with { Flow = program.Flow with { Bindings = program.Flow.Bindings.Replace(binding, binding with { ReadOnlyReceiver = false }) } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(damaged));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Both_ir_stages_reject_writes_through_readonly_receiver(bool scalarStage)
    {
        var program = (await Programs.Value)[1];
        var binding = program.Flow.Bindings.Single(b => b.ReadOnlyReceiver && program.Flow.Flow.Flow.Methods.Single(m => IrMethodId.From(m.Source) == b.Method).Source.Name.Contains("MixedReadOnlyValue::Read", StringComparison.Ordinal));
        if (!scalarStage)
        {
            var flow = program.Flow.Flow.Flow;
            var method = flow.Methods.Single(m => IrMethodId.From(m.Source) == binding.Method);
            var block = method.Blocks.First(b => b.Values.Any(v => v.Expression is IrExpression.Parameter { Index: 0 }));
            var receiver = block.Statements.OfType<IrStatement.Define>().First(d => d.Value.Expression is IrExpression.Parameter { Index: 0 });
            var changed = method with { Blocks = method.Blocks.Replace(block, block with { Statements = block.Statements.Insert(block.Statements.IndexOf(receiver) + 1, new IrStatement.InitializeLocation(0, receiver.Value.Id)) }) };
            var invalid = program with { Flow = program.Flow with { Flow = new(flow with { Methods = flow.Methods.Replace(method, changed) }) } };
            Assert.Contains("readonly", Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid)).Message, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            var scalar = ValueScalarLowering.LowerExceptions(program); var boxes = scalar.Flow.Flow; var objects = boxes.Flow.Flow; var flow = objects.Flow;
            var method = flow.Methods.Single(m => IrMethodId.From(m.Source) == binding.Method);
            var block = method.Blocks.First(b => b.Values.Any(v => v.Expression is IrExpression.Parameter { Index: 0 }));
            var receiver = block.Statements.OfType<IrStatement.Define>().First(d => d.Value.Expression is IrExpression.Parameter { Index: 0 });
            var changed = method with { Blocks = method.Blocks.Replace(block, block with { Statements = block.Statements.Insert(block.Statements.IndexOf(receiver) + 1, new IrStatement.InitializeLocation(0, receiver.Value.Id)) }) };
            var invalid = scalar with { Flow = scalar.Flow with { Flow = boxes with { Flow = boxes.Flow with { Flow = objects with { Flow = flow with { Methods = flow.Methods.Replace(method, changed) } } } } } };
            Assert.Contains("readonly", Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalid)).Message, StringComparison.OrdinalIgnoreCase);
        }
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void Readonly_metadata_never_authorizes_a_mutating_body(bool onType)
    {
        var runtime = typeof(object).Assembly.GetName();
        var b = new ResolutionAssemblyBuilder(new("ReadonlyMutation", new Version(1, 0, 0, 0)));
        var reference = b.Reference(new(runtime.Name!, runtime.Version!, publicKeyToken: Convert.ToHexStringLower(runtime.GetPublicKeyToken()!)));
        var valueBase = b.TypeReference("System", "ValueType", reference);
        var marker = b.TypeReference("System.Runtime.CompilerServices", "IsReadOnlyAttribute", reference);
        var attribute = b.Member(marker, ".ctor", 0x20, 0, 1);
        var type = b.Type("Test", "Value", baseType: valueBase, attributes: TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout);
        var field = b.Field("Number", 6, 8);
        var mutate = b.Method("Mutate", [0x20, 0, 8], attributes: MethodAttributes.Public, emit: il => {
            il.OpCode(ILOpCode.Ldarg_0); il.OpCode(ILOpCode.Ldc_i4_1); il.OpCode(ILOpCode.Stfld); il.Token(field);
            il.OpCode(ILOpCode.Ldc_i4_0); il.OpCode(ILOpCode.Ret);
        });
        b.CustomAttribute(onType ? type : mutate, attribute, 1, 0, 0, 0);
        b.Type("Test", "Caller");
        byte encoded = checked((byte)(MetadataTokens.GetRowNumber(type) << 2));
        var root = b.Method("Run", [0, 0, 8], locals: [7, 1, 0x11, encoded], emit: il => {
            il.OpCode(ILOpCode.Ldloca_s); il.CodeBuilder.WriteByte(0); il.OpCode(ILOpCode.Call); il.Token(mutate); il.OpCode(ILOpCode.Ret);
        });
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!; var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Readonly", StringComparison.OrdinalIgnoreCase));
    }
}
