using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.Resolution;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class PrivateGenericTests
{
    private static readonly string[] Names = ["Copy", "Holder", "Delegate"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(PrivateGenericProbe), Names));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Private_reference_arguments_remain_opaque_in_generic_bodies(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(PrivateGenericProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).ToArray());
    }

    [Theory]
    [InlineData("array", true)] [InlineData("array-operand", false)] [InlineData("array-and-cast", true)]
    [InlineData("opaque", true)] [InlineData("operand", false)] [InlineData("local", false)]
    [InlineData("catch", false)] [InlineData("signature", false)] [InlineData("unrelated", false)]
    public void Foreign_generic_body_cannot_name_the_private_type_hidden_behind_its_argument(string use, bool admitted)
    {
        var result = ForeignClosure(use);
        Assert.Equal(admitted, result.Success);
        if (admitted) ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program!));
        else Assert.Contains(result.Diagnostics, d => d.Message.Contains(use == "catch" ? "Custom exceptions require public or same-assembly internal top-level" : "lexical declaring scope", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, "opaque")] [InlineData(true, "opaque")]
    [InlineData(false, "array")] [InlineData(true, "array")]
    [InlineData(false, "array-and-cast")] [InlineData(true, "array-and-cast")]
    public async Task Foreign_generic_reference_transport_matches_dotnet(bool optimized, string use)
    {
        var result = ForeignClosure(use);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("Foreign", result.Program!, (a, b) => unchecked(a + b))]);
    }

    [Fact]
    public void Array_access_evidence_does_not_authorize_type_tests()
    {
        var program = ForeignClosure("array-and-cast").Program!;
        var flow = program.Flow.Flow.Flow;
        var method = flow.Methods.Single(m => m.GenericTypeTests is not null);
        Assert.NotNull(method.GenericArrays);
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with {
            Flow = flow with { Methods = flow.Methods.Replace(method, method with { GenericTypeTests = null }) } } } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program);
        var scalarFlow = scalar.Flow.Flow.Flow.Flow.Flow;
        var scalarMethod = scalarFlow.Methods.Single(m => IrMethodId.From(m.Source) == IrMethodId.From(method.Source));
        var scalarInvalid = scalar with { Flow = scalar.Flow with { Flow = scalar.Flow.Flow with { Flow = scalar.Flow.Flow.Flow with {
            Flow = scalar.Flow.Flow.Flow.Flow with { Flow = scalarFlow with { Methods = scalarFlow.Methods.Replace(scalarMethod, scalarMethod with { GenericTypeTests = null }) } } } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(scalarInvalid));
    }

    private static ManagedExceptionTranslationResult ForeignClosure(string use)
    {
        using var files = new ResolverFiles();
        string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        using var bootstrap = AssemblyResolver.Create([runtime]).Value!;
        var core = bootstrap.OpenInput(typeof(object).Assembly.Location).Value!.Identity;
        var ownerIdentity = new AssemblyIdentity("PrivateOwner", new Version(1, 0, 0, 0));
        var bridgeIdentity = new AssemblyIdentity("ForeignBridge", new Version(1, 0, 0, 0));
        var bridge = new ResolutionAssemblyBuilder(bridgeIdentity);
        var objectRef = bridge.TypeReference("System", "Object", bridge.Reference(core));
        var ownerRef = bridge.TypeReference("Test", "Owner", bridge.Reference(ownerIdentity));
        var privateRef = bridge.TypeReference("", "Context", ownerRef);
        byte privateCode = checked((byte)((MetadataTokens.GetRowNumber(privateRef) << 2) | 1));
        bridge.Type("Test", "Bridge", baseType: objectRef);
        var variable = bridge.Specification(0x1e, 0);
        byte[] parameter = use == "signature" ? [0x12, privateCode] : [0x1e, 0];
        bridge.Method("Echo", [0x10, 1, 1, 0x1e, 0, .. parameter], arity: 1,
            locals: use == "local" ? [7, 1, 0x12, privateCode] : [7, 1, 0x1e, 0], emit: il => {
                if (use is "array" or "array-operand" or "array-and-cast") {
                    il.LoadConstantI4(2); il.OpCode(ILOpCode.Newarr); il.Token(use == "array-operand" ? privateRef : variable); il.OpCode(ILOpCode.Pop);
                    if (use == "array-and-cast") { il.LoadArgument(0); il.OpCode(ILOpCode.Isinst); il.Token(variable); il.OpCode(ILOpCode.Pop); }
                }
                if (use == "catch") {
                    var start = il.DefineLabel(); var handler = il.DefineLabel(); var done = il.DefineLabel();
                    il.MarkLabel(start); il.Branch(ILOpCode.Leave, done);
                    il.MarkLabel(handler); il.OpCode(ILOpCode.Pop); il.Branch(ILOpCode.Leave, done);
                    il.MarkLabel(done); il.ControlFlowBuilder!.AddCatchRegion(start, handler, handler, done, privateRef);
                }
                if (use == "operand") { il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Isinst); il.Token(privateRef); il.OpCode(ILOpCode.Pop); }
                il.LoadArgument(0);
                if (use != "local" && use != "signature") { il.StoreLocal(0); il.LoadLocal(0); }
                il.OpCode(ILOpCode.Ret);
            });
        files.Write("ForeignBridge.dll", bridge.Build());
        var b = new ResolutionAssemblyBuilder(ownerIdentity);
        var obj = b.TypeReference("System", "Object", b.Reference(core));
        var bridgeRef = b.TypeReference("Test", "Bridge", b.Reference(bridgeIdentity));
        var owner = b.Type("Test", "Owner", baseType: obj);
        byte hiddenCode = 3 << 2; // Context is the next TypeDef after Owner.
        var member = b.Member(bridgeRef, "Echo", [0x10, 1, 1, 0x1e, 0,
            .. (use == "signature" ? new byte[] { 0x12, hiddenCode } : new byte[] { 0x1e, 0 })]);
        var call = b.Instantiate(member, 0x0a, 1, 0x12, hiddenCode);
        MethodDefinitionHandle Root() => b.Method("Run", [0, 2, 8, 8, 8], emit: il => {
            il.OpCode(ILOpCode.Ldnull); il.Call(call); il.OpCode(ILOpCode.Pop);
            il.LoadArgument(0); il.LoadArgument(1); il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Ret);
        });
        MethodDefinitionHandle root = use == "unrelated" ? default : Root();
        b.Type("", "Context", parent: owner, baseType: use == "catch" ? b.TypeReference("System", "Exception", b.Reference(core)) : obj, attributes: TypeAttributes.NestedPrivate | TypeAttributes.Sealed);
        if (use == "unrelated") { b.Type("Test", "Other", baseType: obj); root = Root(); }
        var path = files.Write("PrivateOwner.dll", b.Build());
        using var resolver = AssemblyResolver.Create([files.DirectoryPath("refs"), runtime]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var input = resolver.OpenInput(path).Value!;
        return ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
    }
}
