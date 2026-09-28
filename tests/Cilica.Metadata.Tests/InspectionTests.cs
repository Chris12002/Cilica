using System;
using System.IO;
using System.Linq;
using Cilica.Metadata.Output;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class InspectionTests
{
    [Theory]
    [InlineData(Fixture.Basic)]
    [InlineData(Fixture.Forwarding)]
    public void Text_output_matches_golden_file(string fixture)
    {
        AssemblyReport report = AssemblyInspector.Inspect(Fixture.PathOf(fixture));

        Golden.Verify(fixture + ".txt", TextReportWriter.Write(report));
    }

    [Theory]
    [InlineData(Fixture.Basic)]
    [InlineData(Fixture.Forwarding)]
    public void Json_output_matches_golden_file(string fixture)
    {
        AssemblyReport report = AssemblyInspector.Inspect(Fixture.PathOf(fixture));

        Golden.Verify(fixture + ".json", JsonReportWriter.Write(report));
    }

    [Fact]
    public void Output_does_not_depend_on_run_or_input_location()
    {
        string original = Fixture.PathOf(Fixture.Basic);
        string directory = Path.Combine(Path.GetTempPath(), "cilica-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string copy = Path.Combine(directory, Path.GetFileName(original));
            File.Copy(original, copy);

            AssemblyReport first = AssemblyInspector.Inspect(original);
            AssemblyReport second = AssemblyInspector.Inspect(original);
            AssemblyReport moved = AssemblyInspector.Inspect(copy);

            Assert.Equal(TextReportWriter.Write(first), TextReportWriter.Write(second));
            Assert.Equal(TextReportWriter.Write(first), TextReportWriter.Write(moved));
            Assert.Equal(JsonReportWriter.Write(first), JsonReportWriter.Write(moved));
            Assert.DoesNotContain(directory, JsonReportWriter.Write(moved), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Reports_identity_and_references()
    {
        AssemblyReport report = AssemblyInspector.Inspect(Fixture.PathOf(Fixture.Basic));

        Assert.Empty(report.Diagnostics);
        Assert.Equal("Cilica.Fixtures.Basic", report.Identity?.Name);
        Assert.Equal(".NETStandard,Version=v2.1", report.TargetFramework);
        Assert.Contains("il-only", report.ImageFlags);
        AssemblyIdentityReport netstandard = Assert.Single(report.References, r => r.Name == "netstandard");
        Assert.Equal("cc7b13ffcd2ddd51", netstandard.PublicKeyToken);
    }

    [Fact]
    public void Fields_keep_declaration_order_and_explicit_offsets()
    {
        TypeReport overlay = Type(Fixture.Basic, "Cilica.Fixtures.Basic.Overlay");

        Assert.Equal("struct", overlay.Kind);
        Assert.Equal("explicit, size 8", overlay.Layout);
        Assert.Equal(["Whole", "Low", "High"], overlay.Fields.Select(f => f.Name));
        Assert.Equal([0, 0, 4], overlay.Fields.Select(f => f.Offset ?? -1));
    }

    [Fact]
    public void Reports_literal_values_and_parameter_defaults()
    {
        TypeReport square = Type(Fixture.Basic, "Cilica.Fixtures.Basic.Square");
        MethodReport add = Assert.Single(Type(Fixture.Basic, "Cilica.Fixtures.Basic.Maths").Methods, m => m.Name == "Add");

        Assert.Equal("4", Assert.Single(square.Fields, f => f.Name == "Sides").Value);
        Assert.Equal("\"square\\n\\\"quoted\\\"\"", Assert.Single(square.Fields, f => f.Name == "Label").Value);
        Assert.Equal("-9000000000", Assert.Single(square.Fields, f => f.Name == "Large").Value);
        Assert.Equal("int32 Add(int32 a, [opt] int32 b = 2)", add.Signature);
    }

    [Fact]
    public void Reports_generic_parameters_with_variance_and_constraints()
    {
        TypeReport repository = Type(Fixture.Basic, "Cilica.Fixtures.Basic.Repository`2");
        TypeReport transformer = Type(Fixture.Basic, "Cilica.Fixtures.Basic.Transformer`2");

        Assert.Equal(["[netstandard]System.IComparable`1<!TKey>"], repository.GenericParameters[0].Constraints);
        Assert.Equal(["class", "new()"], repository.GenericParameters[1].Constraints);
        Assert.Equal(["in", "out"], transformer.GenericParameters.Select(p => p.Variance));
        Assert.Equal("delegate", transformer.Kind);
    }

    [Fact]
    public void Decodes_attributes_with_local_enums_and_keeps_blobs_for_external_enums()
    {
        TypeReport annotated = Type(Fixture.Basic, "Cilica.Fixtures.Basic.Annotated");
        TypeReport note = Type(Fixture.Basic, "Cilica.Fixtures.Basic.NoteAttribute");

        Assert.Contains(
            "[Cilica.Fixtures.Basic.NoteAttribute(\"sample\", Colour = Cilica.Fixtures.Basic.Colour(2), " +
            "Related = typeof(Cilica.Fixtures.Basic.Square), Values = [1, 2])]",
            annotated.Attributes);
        Assert.Contains(note.Attributes, a => a.StartsWith("[[netstandard]System.AttributeUsageAttribute(undecoded blob ", StringComparison.Ordinal));
    }

    [Fact]
    public void Reports_method_semantics_relevant_to_compilation()
    {
        TypeReport locking = Type(Fixture.Basic, "Cilica.Fixtures.Basic.Locking");
        TypeReport native = Type(Fixture.Basic, "Cilica.Fixtures.Basic.Native");
        TypeReport explicitImplementation = Type(Fixture.Basic, "Cilica.Fixtures.Basic.ExplicitImplementation");

        Assert.Contains("synchronized", Assert.Single(locking.Methods, m => m.Name == "Synchronised").Flags);
        Assert.Equal("int32 modreq([netstandard]System.Runtime.CompilerServices.IsVolatile)", Assert.Single(locking.Fields).Type);
        MethodReport import = Assert.Single(native.Methods, m => m.Name == "GetProcessId");
        Assert.Equal("\"libc\" getpid", import.Import);
        Assert.False(import.HasBody);
        Assert.Equal("method unmanaged cdecl int32 *(int32)", Assert.Single(native.Fields, f => f.Name == "Callback").Type);
        Assert.Equal(
            ["Cilica.Fixtures.Basic.ExplicitImplementation::System.IDisposable.Dispose() overrides [netstandard]System.IDisposable::Dispose()"],
            explicitImplementation.Overrides);
    }

    [Fact]
    public void Reports_type_forwarders_including_nested_types()
    {
        AssemblyReport report = AssemblyInspector.Inspect(Fixture.PathOf(Fixture.Forwarding));

        Assert.Equal(
            ["Cilica.Fixtures.Basic.Outer", "Cilica.Fixtures.Basic.Outer/Inner", "Cilica.Fixtures.Basic.Point"],
            report.ExportedTypes.Select(e => e.Name));
        Assert.All(report.ExportedTypes, e =>
        {
            Assert.True(e.IsForwarder);
            Assert.Equal("Cilica.Fixtures.Basic", e.Target);
        });
    }

    private static TypeReport Type(string fixture, string name) =>
        Assert.Single(AssemblyInspector.Inspect(Fixture.PathOf(fixture)).Types, t => t.Name == name);
}
