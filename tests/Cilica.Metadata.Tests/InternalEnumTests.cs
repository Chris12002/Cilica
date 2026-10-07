using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class InternalEnumTests
{
    private static readonly string[] Names = ["Locals", "Objects", "Values", "ArraysOfValues", "Statics"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(InternalEnumProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    public async Task Internal_enum_scalar_storage_matches_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(InternalEnumProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void Direct_internal_enum_operands_are_checked_per_calling_assembly(bool foreign, bool box)
    {
        using var files = new ResolverFiles();
        using var bootstrap = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var coreIdentity = bootstrap.OpenInput(typeof(object).Assembly.Location).Value!.Identity;
        var definition = new ResolutionAssemblyBuilder(new("HiddenEnum", new Version(1, 0, 0, 0)));
        var parent = definition.TypeReference("System", "Enum", definition.Reference(coreIdentity));
        var type = definition.Type("Test", "State", baseType: parent, attributes: TypeAttributes.NotPublic | TypeAttributes.Sealed);
        definition.FieldWithAttributes("value__", FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName, 6, 8);
        EntityHandle operand = type;
        var caller = definition;
        if (foreign)
        {
            files.Write("HiddenEnum.dll", definition.Build());
            caller = new ResolutionAssemblyBuilder(new("ForeignEnumCaller", new Version(1, 0, 0, 0)));
            operand = caller.TypeReference("Test", "State", caller.Reference(new("HiddenEnum", new Version(1, 0, 0, 0))));
        }
        caller.Type("Test", "Caller");
        byte coded = checked((byte)((MetadataTokens.GetRowNumber(operand) << 2) | (operand.Kind == HandleKind.TypeReference ? 1 : 0)));
        var root = caller.Method("Run", [0, 0, 8], locals: [0x07, 1, 0x11, coded], emit: il => {
            if (box) { il.LoadConstantI4(7); il.OpCode(ILOpCode.Box); il.Token(operand); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(7); }
            else { il.LoadLocalAddress(0); il.OpCode(ILOpCode.Initobj); il.Token(operand); il.LoadLocal(0); }
            il.OpCode(ILOpCode.Ret);
        });
        var path = files.Write(foreign ? "Caller.dll" : "HiddenEnum.dll", caller.Build());
        using var resolver = AssemblyResolver.Create([files.DirectoryPath("refs"), Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var input = resolver.OpenInput(path).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
        if (foreign) { Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("calling assembly", StringComparison.Ordinal)); }
        else Assert.True(result.Success, string.Join("; ", result.Diagnostics));
    }
}
