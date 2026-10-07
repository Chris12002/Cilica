using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class EnumConstantMetadataTests
{
    [Theory]
    [InlineData("unique")] [InlineData("empty")] [InlineData("aliases")] [InlineData("attribute")] [InlineData("budget")]
    public void Formatting_admission_rejects_attributes_aliases_and_exhausted_work(string shape)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("EnumFormatConstants", new Version(1,0,0,0)));
        var reference = b.Reference(library.Identity);
        var parent = b.TypeReference("System", "Enum", reference);
        var owner = b.Type("Test", "Status", baseType: parent, attributes: TypeAttributes.Public | TypeAttributes.Sealed);
        b.FieldWithAttributes("value__", FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName, 6, 8);
        if (shape == "attribute")
        {
            var attribute = b.TypeReference("System", "FlagsAttribute", reference);
            var ctor = b.Member(attribute, ".ctor", 0x20, 0, 1);
            b.CustomAttribute(owner, ctor, 1, 0, 0, 0);
        }
        if (shape != "empty") for (int i = 0; i < 3; i++)
        {
            var field = b.FieldWithAttributes("Name" + i, FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault,
                6, 0x11, checked((byte)(MetadataTokens.GetRowNumber(owner) << 2)));
            b.Constant(field, shape == "aliases" ? 7 : i == 0 ? -7 : i == 1 ? int.MaxValue : 0);
        }
        var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        var type = resolver.ResolveType(input, owner).Value!;
        var result = resolver.ReadInt32EnumFormattingConstants(type, new(shape == "budget" ? 1 : 65536));
        if (shape is "unique" or "empty")
        {
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));
            Assert.Equal(shape == "empty" ? Array.Empty<int>() : new[] { -7, int.MaxValue, 0 }, result.Value!.Constants.Select(c => c.Value));
        }
        else
        {
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, d => shape == "budget" ? d.Id == DiagnosticIds.TranslationLimit
                : d.Message.Contains(shape == "aliases" ? "duplicate-value" : "custom attributes", StringComparison.Ordinal));
            // IsDefined's independent metadata contract still admits flags and aliases.
            Assert.True(resolver.ReadInt32EnumConstants(type, new(65536)).Success);
        }
    }
    [Theory]
    [InlineData("valid")] [InlineData("empty")] [InlineData("missing")] [InlineData("wide")]
    [InlineData("flags")] [InlineData("duplicate")] [InlineData("offset")] [InlineData("long-name")]
    [InlineData("total-names")] [InlineData("fields")] [InlineData("budget")]
    public void Constant_rows_are_exact_bounded_metadata(string shape)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("EnumConstants", new Version(1,0,0,0)));
        var parent = b.TypeReference("System", "Enum", b.Reference(library.Identity));
        var owner = b.Type("Test", "Status", baseType: parent, attributes: TypeAttributes.Public | TypeAttributes.Sealed);
        b.FieldWithAttributes("value__", FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName, 6, 8);
        int count = shape == "empty" ? 0 : shape == "fields" ? 4096 : shape == "total-names" ? 17 : 4;
        for (int i = 0; i < count; i++)
        {
            string name = shape == "long-name" ? new string('x', 4097) : shape == "total-names" ? new string('x', 4000) + i
                : shape == "duplicate" ? "Same" : i == 3 ? "ΔName" : "Name" + i;
            var field = b.FieldWithAttributes(name, FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal
                | (shape == "flags" ? 0 : FieldAttributes.HasDefault), 6, 0x11, checked((byte)(MetadataTokens.GetRowNumber(owner) << 2)));
            if (shape != "missing") b.Constant(field, shape == "wide" ? (object)7L : i < 2 ? 0 : i == 2 ? -7 : int.MaxValue);
            if (shape == "offset") b.FieldOffset(field, 4);
        }
        var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        var result = resolver.ReadInt32EnumConstants(resolver.ResolveType(input, owner).Value!, new(shape == "budget" ? 1 : 65536));
        if (shape is "valid" or "empty")
        {
            Assert.True(result.Success, string.Join("; ", result.Diagnostics)); Assert.Equal(count, result.Value!.Constants.Length);
            if (shape == "valid") Assert.Equal(new[] {0, 0, -7, int.MaxValue}, result.Value.Constants.Select(c => c.Value));
        }
        else
        {
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, d => shape is "long-name" or "total-names" or "fields" or "budget"
                ? d.Id == DiagnosticIds.TranslationLimit : d.Message.Contains("Enum constants", StringComparison.Ordinal));
        }
    }
}
