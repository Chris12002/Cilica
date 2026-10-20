using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeScalarLocalsTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(ScalarLocalsProbe), ["Run"]));

    // O0 keeps its default dispatcher; optimized builds cover the dispatcher, frame-resident
    // direct scalars (diagnostic) and native scalars with and without internal diagnostics.
    [Theory]
    [InlineData(false, 0, 0, 0)]
    [InlineData(true, 0, 0, 0)] [InlineData(true, 1, 0, 0)] [InlineData(true, 1, 1, 0)] [InlineData(true, 1, 1, 1)]
    public async Task Native_scalars_preserve_values_borrows_recursion_allocation_and_faults(bool optimized, int direct, int native, int diagnostics)
    {
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("Scalars", (await Programs.Value)[0], ScalarLocalsProbe.Run)],
            nativeFlags: ["-DCILICA_DIRECT_CONTROL_FLOW=" + direct, "-DCILICA_NATIVE_SCALARS=" + native,
                "-DCILICA_VERIFY_INTERNAL_REFERENCES=" + diagnostics, "-DCILICA_VERIFY_BORROW_LIFETIMES=" + diagnostics]);
    }

    private static string Symbol(CppArtifact artifact, string name) => "cilica_m_" + artifact.Source.ContentHash + "_"
        + typeof(ScalarLocalsProbe).GetMethod(name)!.MetadataToken.ToString("x8", CultureInfo.InvariantCulture);

    private static string Between(string text, string start, string end)
    {
        int first = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(first >= 0, start);
        int last = text.IndexOf(end, first, StringComparison.Ordinal);
        Assert.True(last > first, end);
        return text[first..last];
    }

    [Fact]
    public async Task Address_taken_scalars_stay_framed_and_other_scalars_become_direct_locals()
    {
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions((await Programs.Value)[0]));
        string neighbours = Symbol(artifact, "Neighbours"), chain = Symbol(artifact, "Chain");

        // Neighbours borrows two Int32 locals (framed, high) and one Int64 (low).
        string frame = Between(artifact.Text, "struct " + neighbours + "_storage {", "\n};");
        Assert.Matches(new Regex(@"#if CILICA_NATIVE_SCALARS\n    std::array<std::uint32_t, 2> integers\{\};\n    std::array<std::uint64_t, 1> wide_integers\{\};\n#else\n"), frame);
        string direct = Between(artifact.Text[artifact.Text.IndexOf(neighbours + "_steps {", StringComparison.Ordinal)..],
            "#if CILICA_DIRECT_CONTROL_FLOW\n    // Direct native edges", "#undef CILICA_STEP_RETURN");
        Assert.Contains("#if CILICA_NATIVE_SCALARS\n#define CILICA_I(slot) cilica_i_ ## slot", direct, StringComparison.Ordinal);
        Assert.Contains("[[maybe_unused]] std::uint32_t cilica_i_2 = 0", direct, StringComparison.Ordinal);
        Assert.Contains("LocationOrigin::frame_integer, 0", direct, StringComparison.Ordinal);
        Assert.Contains("LocationOrigin::frame_wide_integer, 0", direct, StringComparison.Ordinal);
        Assert.Contains("f->integers[0]", direct, StringComparison.Ordinal);
        Assert.DoesNotContain("f->integers[2]", direct, StringComparison.Ordinal);
        Assert.DoesNotContain("f->wide_integers[1]", direct, StringComparison.Ordinal);

        // A method without borrows keeps no scalar frame slots in the direct rendering.
        string chainFrame = Between(artifact.Text, "struct " + chain + "_storage {", "\n};");
        Assert.Contains("#if CILICA_NATIVE_SCALARS\n    std::array<std::uint32_t, 0> integers{};", chainFrame, StringComparison.Ordinal);
        string helpers = Between(artifact.Text, chain + "_steps {", "#undef CILICA_STEP_RETURN");
        Assert.Contains("#define CILICA_I(slot) f->integers[slot]", helpers, StringComparison.Ordinal);

        // Every scalar reference goes through a defined mapping.
        Assert.DoesNotContain("\u0001", artifact.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Native_scalars_link_across_module_units()
    {
        var module = ObjectCppEmitter.EmitExceptionModule(ValueScalarLowering.LowerExceptions((await Programs.Value)[0]), 1);
        using var files = new ResolverFiles(); string dir = files.DirectoryPath("scalar-locals-module");
        foreach (var file in module.Sources.Prepend(module.Header)) File.WriteAllText(Path.Combine(dir, file.Name), file.Text);
        string entry = "cilica_m_" + module.Source.ContentHash + "_" + module.Source.Token.ToString("x8", CultureInfo.InvariantCulture);
        string main = Path.Combine(dir, "main.cpp");
        var pairs = new[] { (0, 0), (1, 2), (-1, 3), (127, -1), (32768, int.MaxValue), (int.MinValue, 1), (6, 7) };
        File.WriteAllText(main, $$"""
            #include "{{module.Header.Name}}"
            #include <cassert>
            using namespace {{module.EntryPoint[..^4]}}_closure;
            static void check(std::int32_t a, std::int32_t b, std::int32_t expected) {
                Instance heap; heap.collect_before_allocation = true; Context c(heap); LocationArgument args[2]{};
                args[0].bits = std::bit_cast<std::uint32_t>(a); args[1].bits = std::bit_cast<std::uint32_t>(b);
                assert({{entry}}(c, args) == expected && c.pending == 0);
                assert(c.depth == 0 && c.used == 0 && c.top == nullptr && c.borrows == nullptr);
            }
            int main() {
            {{string.Concat(pairs.Select(p => $"    check({p.Item1}, {p.Item2}, {ScalarLocalsProbe.Run(p.Item1, p.Item2)});\n"))}}
            }
            """);
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        var objects = new System.Collections.Generic.List<string>();
        foreach (string cpp in module.Sources.Select(f => Path.Combine(dir, f.Name)).Append(main))
        {
            string obj = cpp + ".o";
            var build = await NativeProcess.Run(compiler, ["-std=c++20", "-O2", "-fno-exceptions", "-fno-rtti",
                "-Wall", "-Wextra", "-Werror", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", "-c", cpp, "-o", obj], dir);
            Assert.True(build.ExitCode == 0, build.Stderr); objects.Add(obj);
        }
        string exe = Path.Combine(dir, "probe");
        var link = await NativeProcess.Run(compiler, ["-fsanitize=address,undefined", .. objects, "-o", exe], dir);
        Assert.True(link.ExitCode == 0, link.Stderr);
        var run = await NativeProcess.Run(exe, [], dir); Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr);
    }
}
