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
public sealed class HiddenEnumGenericTests
{
    private static readonly string[] Names = ["InternalHolder", "NestedHolder", "Nullable", "PrivateNullable", "PrivateBox"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(HiddenEnumGenericProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    public async Task Authorized_hidden_enum_arguments_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(HiddenEnumGenericProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }
    [Fact]
    public void Hidden_struct_arguments_keep_their_existing_boundary()
    {
        var result = IntegerObjectTranslationTests.Translate("UnsupportedStruct", fixture: typeof(HiddenEnumGenericProbe));
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("externally visible", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData("opaque", true)] [InlineData("box", false)] [InlineData("token", false)] [InlineData("local", false)]
    public void Foreign_generic_code_carries_T_but_cannot_name_hidden_enum(string use, bool admitted)
    {
        var result = ForeignClosure(use);
        Assert.Equal(admitted, result.Success);
        if (admitted) ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program!));
        else Assert.Contains(result.Diagnostics, d => d.Message.Contains("calling assembly", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Foreign_opaque_boxing_matches_dotnet(bool optimized)
    {
        var result = ForeignClosure("opaque"); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("OpaqueBox", result.Program!, (int a, int b) => unchecked(a + b + 1))]);
    }
    private static ManagedExceptionTranslationResult ForeignClosure(string use)
    {
        using var files = new ResolverFiles();
        string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        using var bootstrap = AssemblyResolver.Create([runtime]).Value!;
        var core = bootstrap.OpenInput(typeof(object).Assembly.Location).Value!.Identity;
        var homeIdentity = new AssemblyIdentity("HiddenEnumOwner", new Version(1, 0, 0, 0));
        var bridgeIdentity = new AssemblyIdentity("OpaqueEnumBridge", new Version(1, 0, 0, 0));
        var bridge = new ResolutionAssemblyBuilder(bridgeIdentity);
        var hidden = bridge.TypeReference("Test", "Hidden", bridge.Reference(homeIdentity));
        var variable = bridge.Specification(0x1e, 0);
        bridge.Type("Test", "Bridge", baseType: bridge.TypeReference("System", "Object", bridge.Reference(core)));
        byte hiddenCode = checked((byte)((MetadataTokens.GetRowNumber(hidden) << 2) | 1));
        bridge.Method("Box", [0x10, 1, 1, 0x1c, 0x1e, 0], arity: 1,
            locals: use == "local" ? new byte[] {7, 1, 0x11, hiddenCode} : null,
            emit: il => {
                if (use == "token") { il.OpCode(ILOpCode.Ldtoken); il.Token(hidden); il.OpCode(ILOpCode.Pop); }
                il.LoadArgument(0); il.OpCode(ILOpCode.Box); il.Token(use == "box" ? hidden : variable); il.OpCode(ILOpCode.Ret);
            });
        files.Write("OpaqueEnumBridge.dll", bridge.Build());
        var home = new ResolutionAssemblyBuilder(homeIdentity);
        var parent = home.TypeReference("System", "Enum", home.Reference(core));
        var type = home.Type("Test", "Hidden", baseType: parent, attributes: TypeAttributes.NotPublic | TypeAttributes.Sealed);
        home.FieldWithAttributes("value__", FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName, 6, 8);
        var helper = home.TypeReference("Test", "Bridge", home.Reference(bridgeIdentity));
        var member = home.Member(helper, "Box", 0x10, 1, 1, 0x1c, 0x1e, 0);
        var call = home.Instantiate(member, 0x0a, 1, 0x11, checked((byte)(MetadataTokens.GetRowNumber(type) << 2)));
        home.Type("Test", "Entry");
        var root = home.Method("Run", [0, 2, 8, 8, 8], locals: [7, 1, 8], emit: il => {
            il.LoadArgument(0); il.Call(call); il.OpCode(ILOpCode.Dup);
            il.OpCode(ILOpCode.Isinst); il.Token(type); il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Cgt_un); il.StoreLocal(0);
            il.OpCode(ILOpCode.Unbox_any); il.Token(type);
            il.LoadArgument(1); il.OpCode(ILOpCode.Add); il.LoadLocal(0); il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Ret);
        });
        var path = files.Write("HiddenEnumOwner.dll", home.Build());
        using var resolver = AssemblyResolver.Create([files.DirectoryPath("refs"), runtime]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var input = resolver.OpenInput(path).Value!;
        return ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
    }
}
