using System;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class StaticArrayDataSafetyTests
{
    [Theory]
    [InlineData("rva", "readonly RVA")]
    [InlineData("section", "file-backed PE")]
    [InlineData("readonly", "readonly RVA")]
    [InlineData("size", "too small")]
    [InlineData("count", "1 through 256")]
    [InlineData("branch", "uninterrupted")]
    public async Task Corrupted_metadata_and_unproven_control_flow_are_rejected(string mutation, string message)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var bytes = await File.ReadAllBytesAsync(typeof(StaticArrayDataProbe).Assembly.Location, TestContext.Current.CancellationToken);
        string name = mutation == "branch" ? "Multiple" : "Integers";
        var root = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(StaticArrayDataProbe).GetMethod(name)!.MetadataToken);
        var instructions = Assert.Single(MethodBodyInspector.Inspect(bytes, "original.dll",
            n => n.StartsWith("Cilica.Fixtures.Execution.StaticArrayDataProbe::" + name + "(", StringComparison.Ordinal)).Methods).Instructions;
        int tokenOffset = instructions.First(i => i.OpCode == "ldtoken").Offset;
        using (var pe = new PEReader(ImmutableArray.Create(bytes)))
        {
            var reader = pe.GetMetadataReader();
            int methodRva = reader.GetMethodDefinition(root).RelativeVirtualAddress;
            var section = pe.PEHeaders.SectionHeaders.Single(s => methodRva >= s.VirtualAddress && methodRva < (long)s.VirtualAddress + s.VirtualSize);
            int header = methodRva - section.VirtualAddress + section.PointerToRawData;
            int code = header + ((bytes[header] & 3) == 2 ? 1 : (bytes[header + 1] >> 4) * 4);
            var fieldHandle = (FieldDefinitionHandle)MetadataTokens.EntityHandle(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(code + tokenOffset + 1)));
            int row = MetadataTokens.GetRowNumber(fieldHandle);
            int table = pe.PEHeaders.MetadataStartOffset + reader.GetTableMetadataOffset(TableIndex.FieldRva);
            int rowSize = reader.GetTableRowSize(TableIndex.FieldRva);
            int rvaRow = Enumerable.Range(0, reader.GetTableRowCount(TableIndex.FieldRva)).Single(i =>
                (rowSize == 6 ? BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(table + i * rowSize + 4))
                    : BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(table + i * rowSize + 4))) == row);
            if (mutation == "rva") BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(table + rvaRow * rowSize), 0);
            if (mutation == "section") BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(table + rvaRow * rowSize),
                section.VirtualAddress + section.SizeOfRawData - 1);
            if (mutation == "readonly")
            {
                int flags = pe.PEHeaders.MetadataStartOffset + reader.GetTableMetadataOffset(TableIndex.Field) + (row - 1) * reader.GetTableRowSize(TableIndex.Field);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(flags), (ushort)(reader.GetFieldDefinition(fieldHandle).Attributes & ~FieldAttributes.InitOnly));
            }
            if (mutation == "size")
            {
                int layout = pe.PEHeaders.MetadataStartOffset + reader.GetTableMetadataOffset(TableIndex.ClassLayout);
                int size = reader.GetTableRowSize(TableIndex.ClassLayout);
                for (int i = 0; i < reader.GetTableRowCount(TableIndex.ClassLayout); ++i)
                    if (BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(layout + i * size + 2)) == 32)
                        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(layout + i * size + 2), 1);
            }
            if (mutation == "count") bytes[code] = 0x15; // ldc.i4.m1, same one-byte width as ldc.i4.8
            if (mutation == "branch")
            {
                var branch = instructions.First(i => i.OpCode.EndsWith(".s", StringComparison.Ordinal) && i.OpCode.StartsWith("b", StringComparison.Ordinal));
                bytes[code + branch.Offset] = 0x2b; // br.s leaves two values: valid stack depth, unproven array provenance
                bytes[code + branch.Offset + 1] = checked((byte)(tokenOffset - branch.Offset - 2));
            }
        }
        var input = scope.OpenInput(files.Write("patched.dll", bytes)).Value!;
        var rejected = ValueTranslator.TranslateIntegerObjects(scope, input, root, implementation);
        Assert.False(rejected.Success); Assert.Null(rejected.Program);
        Assert.Contains(rejected.Diagnostics, d => d.Message.Contains(message, StringComparison.Ordinal));
    }
    [Fact]
    public async Task Helper_grant_is_required_even_for_constant_data()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions {
            Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        Assert.Single(artifact.Grants.Where(g => g.Capability == LibraryIntrinsicCapability.StaticArrayInitialization));
        var changed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != LibraryIntrinsicCapability.StaticArrayInitialization)] }] };
        var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(changed);
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
        var input = scope.OpenInput(typeof(StaticArrayDataProbe).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(StaticArrayDataProbe).GetMethod("Integers")!.MetadataToken), implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
    }
}
