using System;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using Cilica.Metadata.Output;
using Xunit;

namespace Cilica.Metadata.Tests;

/// <summary>Malformed input must produce stable diagnostics, never exceptions.</summary>
public sealed class MalformedInputTests
{
    [Fact]
    public void Missing_file_reports_CIL1001_without_absolute_path()
    {
        string path = Path.Combine(Path.GetTempPath(), "cilica-missing-" + Guid.NewGuid().ToString("N"), "Missing.dll");

        AssemblyReport report = AssemblyInspector.Inspect(path);

        Diagnostic diagnostic = Assert.Single(report.Diagnostics);
        Assert.Equal(DiagnosticIds.InputUnreadable, diagnostic.Id);
        Assert.Equal("Cannot read 'Missing.dll': the directory does not exist.", diagnostic.Message);
        Assert.True(report.HasErrors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("This is a text file, not an assembly.")]
    public void Non_PE_input_reports_CIL1002(string content)
    {
        AssemblyReport report = AssemblyInspector.Inspect(Encoding.UTF8.GetBytes(content), "input.dll");

        Assert.Equal(DiagnosticIds.NotPortableExecutable, Assert.Single(report.Diagnostics).Id);
    }

    [Theory]
    [InlineData("headers")]
    [InlineData("metadata")]
    public void Truncated_image_reports_CIL1002(string cutInside)
    {
        // Cutting inside the metadata also leaves the section table describing data past the end of the file.
        byte[] original = Fixture.Bytes(Fixture.Basic);
        int length = cutInside == "headers" ? 200 : MetadataStartOffset(original) + 32;
        byte[] image = original[..length];

        AssemblyReport report = AssemblyInspector.Inspect(image, "truncated.dll");

        Assert.Equal(DiagnosticIds.NotPortableExecutable, Assert.Single(report.Diagnostics).Id);
    }

    [Fact]
    public void Image_without_CLI_header_reports_CIL1003()
    {
        byte[] image = Fixture.Bytes(Fixture.Basic);
        using (var reader = new PEReader(ImmutableArray.Create(image)))
        {
            PEHeaders headers = reader.PEHeaders;
            // The CLI header is data directory 14; directories start 96 (PE32) or 112 (PE32+) bytes into the optional header.
            int directories = headers.PEHeaderStartOffset + (headers.PEHeader!.Magic == PEMagic.PE32 ? 96 : 112);
            Array.Clear(image, directories + 14 * 8, 8);
        }

        AssemblyReport report = AssemblyInspector.Inspect(image, "native.dll");

        Assert.Equal(DiagnosticIds.NoCliMetadata, Assert.Single(report.Diagnostics).Id);
    }

    [Fact]
    public void Corrupt_metadata_signature_reports_CIL1004()
    {
        byte[] image = Fixture.Bytes(Fixture.Basic);
        int metadata = MetadataStartOffset(image);
        Assert.Equal(0x424A5342u, BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(metadata)));
        image[metadata] = 0;

        AssemblyReport report = AssemblyInspector.Inspect(image, "corrupt.dll");

        Assert.Equal(DiagnosticIds.MalformedMetadata, Assert.Single(report.Diagnostics).Id);
    }

    [Fact]
    public void Corrupt_method_signature_reports_CIL1006_and_keeps_other_results()
    {
        byte[] image = Fixture.Bytes(Fixture.Basic);
        using (var reader = new PEReader(ImmutableArray.Create(image)))
        {
            MetadataReader metadata = reader.GetMetadataReader();
            MethodDefinition definition = metadata.MethodDefinitions
                .Select(metadata.GetMethodDefinition)
                .Single(m => metadata.GetString(m.Name) == "Add");
            int blob = reader.PEHeaders.MetadataStartOffset
                + metadata.GetHeapMetadataOffset(HeapIndex.Blob)
                + MetadataTokens.GetHeapOffset(definition.Signature);
            // Skip the one-byte compressed length and replace the signature header with an invalid kind.
            image[blob + 1] = 0xFF;
        }

        AssemblyReport report = AssemblyInspector.Inspect(image, "corrupt.dll");

        Diagnostic diagnostic = Assert.Single(report.Diagnostics);
        Assert.Equal(DiagnosticIds.MalformedEntity, diagnostic.Id);
        Assert.StartsWith("Cilica.Fixtures.Basic.Maths::Add ", diagnostic.Location, StringComparison.Ordinal);
        MethodReport add = Assert.Single(report.Types.Single(t => t.Name == "Cilica.Fixtures.Basic.Maths").Methods, m => m.Name == "Add");
        Assert.Equal("Add <malformed signature>", add.Signature);
        Assert.Contains(report.Types, t => t.Name == "Cilica.Fixtures.Basic.Square");
    }

    [Fact]
    public void Random_metadata_corruption_never_throws()
    {
        byte[] original = Fixture.Bytes(Fixture.Basic);
        int start = MetadataStartOffset(original);
        int length;
        using (var reader = new PEReader(ImmutableArray.Create(original)))
        {
            length = reader.PEHeaders.MetadataSize;
        }

        var random = new Random(20260928);
        for (int iteration = 0; iteration < 500; iteration++)
        {
            byte[] image = (byte[])original.Clone();
            int corruptions = random.Next(1, 8);
            for (int index = 0; index < corruptions; index++)
            {
                image[start + random.Next(length)] = (byte)random.Next(256);
            }

            AssemblyReport report = AssemblyInspector.Inspect(image, "fuzzed.dll");

            // Rendering must also succeed for any report.
            Assert.NotEmpty(TextReportWriter.Write(report));
            Assert.NotEmpty(JsonReportWriter.Write(report));
        }
    }

    private static int MetadataStartOffset(byte[] image)
    {
        using var reader = new PEReader(ImmutableArray.Create(image));
        return reader.PEHeaders.MetadataStartOffset;
    }
}
