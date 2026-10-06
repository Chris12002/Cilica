using System;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class TypeClassificationBudgetTests
{
    [Theory]
    [InlineData(1)] [InlineData(257)]
    public void Enum_classification_limit_reports_metadata_counts_without_claiming_layout_counts(int count)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("ClassificationBudget", new Version(1, 0, 0, 0)));
        var parent = b.TypeReference("System", "Enum", b.Reference(library.Identity));
        var locals = new BlobBuilder(); locals.WriteByte(0x07); locals.WriteCompressedInteger(count);
        for (int i = 0; i < count; i++)
        {
            var type = b.Type("Test", "Status" + i, baseType: parent, attributes: TypeAttributes.Public | TypeAttributes.Sealed);
            b.FieldWithAttributes("value__", FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName, 6, 8);
            locals.WriteByte(0x11); locals.WriteCompressedInteger(MetadataTokens.GetRowNumber(type) << 2);
        }
        b.Type("Test", "Root");
        var root = b.Method("Run", [0, 0, 8], locals: locals.ToArray(), emit: il => { il.LoadConstantI4(7); il.OpCode(ILOpCode.Ret); });
        var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
        if (count == 1) { Assert.True(result.Success, string.Join("; ", result.Diagnostics)); return; }
        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticIds.TranslationLimit, diagnostic.Id);
        Assert.Contains("Cached 256/256 type snapshots", diagnostic.Message, StringComparison.Ordinal);
        // The exception profile has already admitted its fifteen runtime reference types.
        Assert.Contains("Explicit roots: 0 value, 15 reference", diagnostic.Message, StringComparison.Ordinal);
        Assert.True(diagnostic.Message.Contains("Validated Int32 enums: 241", StringComparison.Ordinal), diagnostic.Message);
        Assert.Contains("Snapshots outside explicit value/reference roots: 241", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("metadata work units remaining", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("not emitted runtime layouts", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("Attempted identity:", diagnostic.Message, StringComparison.Ordinal);
    }
}
