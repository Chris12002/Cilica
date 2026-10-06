using System;
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
public sealed class EnumStorageTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(EnumStorageProbe), "Storage", "GenericAndDispatch", "Box"));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Int32_enum_fields_copies_borrows_and_closed_dispatch_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("EnumStorage", programs[0], EnumStorageProbe.Storage), ("EnumDispatch", programs[1], EnumStorageProbe.GenericAndDispatch), ("EnumBox", programs[2], EnumStorageProbe.Box)]);
    }
    [Theory]
    [InlineData("Small")] [InlineData("Wide")] [InlineData("Array")]
    public void Unsupported_enum_representations_remain_diagnostic(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name,fixture:typeof(EnumStorageProbe));
        Assert.False(result.Success); Assert.Null(result.Program); Assert.NotEmpty(result.Diagnostics);
    }
    [Theory]
    [InlineData("name")] [InlineData("flags")] [InlineData("wide")] [InlineData("extra")]
    [InlineData("static")] [InlineData("initializer")]
    public void Malformed_or_out_of_profile_enum_shapes_fail_before_scalar_lowering(string shape)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("EnumShape",new Version(1,0,0,0)));
        var parent = b.TypeReference("System","Enum",b.Reference(library.Identity));
        var type = b.Type("Test","Status",baseType:parent,
            attributes: TypeAttributes.Sealed | TypeAttributes.Public);
        b.FieldWithAttributes(shape == "name" ? "bits" : "value__",
            shape == "flags" ? FieldAttributes.Public : FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName,
            6,shape == "wide" ? (byte)10 : (byte)8);
        if (shape == "extra") b.Field("Extra",6,8);
        if (shape == "static") b.FieldWithAttributes("Storage",FieldAttributes.Public | FieldAttributes.Static,6,8);
        if (shape == "initializer") b.Method(".cctor",[0,0,1], attributes:MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            emit: il => il.OpCode(ILOpCode.Ret));
        b.Type("Test","Root");
        var get = b.Method("Get",[0,0,0x11,checked((byte)(MetadataTokens.GetRowNumber(type) << 2))],emit:il => { il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret); });
        var root = b.Method("Run",[0,0,8],emit:il => { il.Call(get); il.OpCode(ILOpCode.Ret); });
        var input = resolver.OpenInput(files.Write("input.dll",b.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver,input,root,library);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics,d => d.Message.Contains("Enum scalar storage",StringComparison.Ordinal));
    }
}
