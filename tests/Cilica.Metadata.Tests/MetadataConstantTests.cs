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

public sealed class MetadataConstantTests
{
    private static readonly string[] Names = ["Classes", "Values", "Arrays", "EmptyValues", "ClosedGeneric"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(MetadataConstantProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    public async Task Literal_declarations_do_not_occupy_instance_storage(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(MetadataConstantProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }

    [Theory]
    [InlineData("valid", true)] [InlineData("string", true)] [InlineData("null", true)]
    [InlineData("missing", false)] [InlineData("instance", false)] [InlineData("initonly", false)]
    [InlineData("offset", false)] [InlineData("rva", false)] [InlineData("marshal", false)]
    [InlineData("wrong-type", false)] [InlineData("private-scope", false)] [InlineData("direct-load", false)]
    public void Only_valid_literal_metadata_is_omitted_and_direct_loads_still_fail(string shape, bool admitted)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("LiteralLayout", new Version(1, 0, 0, 0)));
        var parent = b.TypeReference("System", "Object", b.Reference(library.Identity));
        var type = b.Type("Test", "Holder", baseType: parent);
        var flags = FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault;
        if (shape == "instance") flags &= ~FieldAttributes.Static;
        if (shape == "initonly") flags |= FieldAttributes.InitOnly;
        if (shape == "rva") flags |= FieldAttributes.HasFieldRVA;
        if (shape == "marshal") flags |= FieldAttributes.HasFieldMarshal;
        if (shape == "private-scope") flags &= ~FieldAttributes.FieldAccessMask;
        var field = b.FieldWithAttributes("Literal", flags, 6, shape is "string" or "null" ? (byte)0x0e : (byte)8);
        if (shape != "missing") b.Constant(field, shape == "null" ? null : shape is "string" or "wrong-type" ? "literal" : (object)7);
        if (shape == "offset") b.FieldOffset(field, 0);
        byte coded = checked((byte)(MetadataTokens.GetRowNumber(type) << 2));
        var root = b.Method("Run", [0, 0, 8], locals: [7, 1, 0x12, coded], emit: il => {
            if (shape == "direct-load") { il.OpCode(ILOpCode.Ldsfld); il.Token(field); }
            else il.LoadConstantI4(0);
            il.OpCode(ILOpCode.Ret);
        });
        var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
        Assert.Equal(admitted, result.Success);
        if (!admitted) Assert.NotEmpty(result.Diagnostics);
    }
}
