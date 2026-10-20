using System;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeProvenFieldTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(ProvenFieldProbe), ["Mixed", "Many", "Null"]));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_layout_guards_preserve_hidden_wide_reference_fields_and_fallback(bool optimized)
    {
        var p = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("Mixed", p[0], ProvenFieldProbe.Mixed), ("Many", p[1], ProvenFieldProbe.Many), ("Null", p[2], ProvenFieldProbe.Null)],
            nativeFlags: ["-Wframe-larger-than=16384"]);
    }

    [Fact]
    public async Task Optimized_member_addresses_keep_exact_descriptor_guards()
    {
        string text = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions((await Programs.Value)[0])).Text;
        Assert.Contains("gnu::always_inline]] static void* proven_field_", text, StringComparison.Ordinal);
        Assert.Contains("_descriptor) return &reinterpret_cast<", text, StringComparison.Ordinal);
        Assert.Contains("#else\n[[maybe_unused, gnu::noinline]] static void* proven_field_", text, StringComparison.Ordinal);
        Assert.DoesNotContain("reinterpret_cast<std::byte*>(value)", text, StringComparison.Ordinal);
    }
}
