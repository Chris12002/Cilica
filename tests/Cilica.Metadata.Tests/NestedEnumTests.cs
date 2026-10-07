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

public sealed class NestedEnumTests
{
    private static readonly string[] Names = ["Objects", "Values", "Arrays"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(NestedEnumProbe), Names));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Private_nested_enum_payloads_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(NestedEnumProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).ToArray());
    }
    [Theory]
    [InlineData("UnsupportedGeneric")]
    public void Nested_enum_storage_does_not_admit_nonpublic_generic_arguments(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(NestedEnumOwner));
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("externally visible", StringComparison.Ordinal));
    }
    [Fact]
    public void Nested_private_enum_borrows_use_containing_type_method_access()
    {
        var result = IntegerObjectTranslationTests.Translate("PrivateCall", fixture: typeof(NestedEnumProbe));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program!));
    }
    [Theory]
    [InlineData("owner", true, "memory")] [InlineData("nested-helper", true, "memory")]
    [InlineData("unrelated", false, "memory")] [InlineData("foreign", false, "memory")] [InlineData("protected", false, "memory")]
    [InlineData("owner", true, "box")] [InlineData("nested-helper", true, "box")]
    [InlineData("unrelated", false, "box")] [InlineData("foreign", false, "box")] [InlineData("protected", false, "box")]
    [InlineData("unrelated", false, "token")] [InlineData("foreign", false, "token")] [InlineData("protected", false, "token")]
    public void Direct_enum_operands_require_lexical_access(string callerKind, bool admitted, string operation)
    {
        using var files = new ResolverFiles();
        using var bootstrap = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var core = bootstrap.OpenInput(typeof(object).Assembly.Location).Value!.Identity;
        var identity = new AssemblyIdentity("NestedEnum", new Version(1, 0, 0, 0));
        var b = new ResolutionAssemblyBuilder(identity);
        var parent = b.TypeReference("System", "Enum", b.Reference(core));
        var owner = b.Type("Test", "Owner");
        MethodDefinitionHandle AddRoot(ResolutionAssemblyBuilder target, EntityHandle operand)
        {
            byte coded = checked((byte)((MetadataTokens.GetRowNumber(operand) << 2) | (operand.Kind == HandleKind.TypeReference ? 1 : 0)));
            return target.Method("Run", [0, 0, 8], locals: [7, 1, 0x11, coded], emit: il => {
                if (operation == "memory") { il.LoadLocalAddress(0); il.OpCode(ILOpCode.Initobj); il.Token(operand); il.LoadLocal(0); }
                else
                {
                    if (operation == "box") il.LoadConstantI4(7);
                    il.OpCode(operation == "box" ? ILOpCode.Box : ILOpCode.Ldtoken); il.Token(operand);
                    il.OpCode(ILOpCode.Pop); il.LoadConstantI4(7);
                }
                il.OpCode(ILOpCode.Ret);
            });
        }
        MethodDefinitionHandle root = default;
        // TypeDef row 3 follows the owner; the owner's method references it forward.
        if (callerKind == "owner") root = AddRoot(b, MetadataTokens.TypeDefinitionHandle(3));
        var type = b.Type("", "Phase", parent: owner, baseType: parent,
            attributes: TypeAttributes.Sealed | (callerKind == "protected" ? TypeAttributes.NestedFamily : TypeAttributes.NestedPrivate));
        b.FieldWithAttributes("value__", FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName, 6, 8);
        if (callerKind == "foreign")
        {
            files.Write("NestedEnum.dll", b.Build());
            b = new ResolutionAssemblyBuilder(new("ForeignEnum", new Version(1, 0, 0, 0)));
            var foreignOwner = b.TypeReference("Test", "Owner", b.Reference(identity));
            var foreignEnum = b.TypeReference("", "Phase", foreignOwner);
            b.Type("Test", "Caller"); root = AddRoot(b, foreignEnum);
        }
        else if (callerKind != "owner")
        {
            b.Type("Test", "Caller", parent: callerKind == "nested-helper" ? owner : default);
            root = AddRoot(b, type);
        }
        var path = files.Write(callerKind == "foreign" ? "ForeignEnum.dll" : "NestedEnum.dll", b.Build());
        using var resolver = AssemblyResolver.Create([files.DirectoryPath("refs"), Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var input = resolver.OpenInput(path).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
        Assert.Equal(admitted, result.Success);
        if (!admitted) Assert.Contains(result.Diagnostics, d => d.Message.Contains("lexical declaring scope", StringComparison.Ordinal));
    }
}
