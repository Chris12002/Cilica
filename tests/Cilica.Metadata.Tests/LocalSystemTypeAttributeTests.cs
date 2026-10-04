using System;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using Cilica.Metadata.Output;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class LocalSystemTypeAttributeTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    public void System_Type_attribute_arguments_preserve_types_arrays_and_null(bool array, bool nullValue, bool local)
    {
        var builder = new ResolutionAssemblyBuilder(new("LocalTypes", new Version(1, 0, 0, 0)));
        builder.Type("System", "Type");
        if (!local) builder.TypeReference("System", "Type", builder.Reference(new("OtherCore", new Version(1, 0, 0, 0))));
        builder.Type("Example", "TypeArgumentAttribute");
        byte typeToken = local ? (byte)8 : (byte)5;
        var constructor = builder.Method(".ctor", array ? [0x20, 1, 1, 0x1d, 0x12, typeToken] : [0x20, 1, 1, 0x12, typeToken]);
        byte[] argument = nullValue ? [0xff] : [12, .. Encoding.UTF8.GetBytes("System.Int32")];
        builder.CustomAttribute(MetadataTokens.EntityHandle(0x20000001), constructor,
            [1, 0, .. array ? new byte[] { 1, 0, 0, 0 } : [], .. argument, 0, 0]);
        var report = AssemblyInspector.Inspect(builder.Build(), "types.dll");
        Assert.Empty(report.Diagnostics);
        Assert.Contains(nullValue ? "TypeArgumentAttribute(null)" : array ? "TypeArgumentAttribute([typeof(System.Int32)])"
            : "TypeArgumentAttribute(typeof(System.Int32))", TextReportWriter.Write(report), StringComparison.Ordinal);
    }

    [Fact]
    public void Nested_namesake_does_not_become_System_Type()
    {
        var builder = new ResolutionAssemblyBuilder(new("LocalTypes", new Version(1, 0, 0, 0)));
        var outer = builder.Type("System", "Outer");
        builder.Type("System", "Type", parent: outer);
        builder.Type("Example", "TypeArgumentAttribute");
        var constructor = builder.Method(".ctor", [0x20, 1, 1, 0x12, 12]); // TypeDef 3
        builder.CustomAttribute(MetadataTokens.EntityHandle(0x20000001), constructor, [1, 0, 0xff, 0, 0]);
        var report = AssemblyInspector.Inspect(builder.Build(), "types.dll");
        Assert.Contains(report.Diagnostics, d => d.Id == DiagnosticIds.MalformedEntity);
    }
}
