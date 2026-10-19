using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeLeafGetterTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Getters_preserve_bits_inheritance_return_roots_and_caught_faults(bool optimized)
    {
        await IntegerObjectTranslationTests.Verify(optimized,
            [(nameof(LeafGetterProbe.Run), LeafGetterProbe.Run), (nameof(LeafGetterProbe.Fault), LeafGetterProbe.Fault)], typeof(LeafGetterProbe));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task Nested_getters_skip_storage_but_faults_cold_entry_and_diagnostics_keep_fallback(bool optimized, bool verify)
    {
        var translated = IntegerObjectTranslationTests.Translate(nameof(LeafGetterProbe.Run), fixture: typeof(LeafGetterProbe));
        Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
        var scalar = ValueScalarLowering.LowerExceptions(translated.Program!);
        var flow = scalar.Flow.Flow.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(scalar);
        string Method(string name) => "cilica_m_" + flow.Methods.Single(m => m.Source.Token == typeof(LeafGetterBase).GetMethod(name)!.MetadataToken && m.Source.ContentHash == flow.Entry.ContentHash).Source.ContentHash
            + "_" + flow.Methods.Single(m => m.Source.Token == typeof(LeafGetterBase).GetMethod(name)!.MetadataToken && m.Source.ContentHash == flow.Entry.ContentHash).Source.Token.ToString("x8", CultureInfo.InvariantCulture);
        string getter = Method("GetNumber");
        Assert.Contains(getter + "_framed(", artifact.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Method("AddOne") + "_framed(", artifact.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Method("Mutate") + "_framed(", artifact.Text, StringComparison.Ordinal);
        var node = flow.Types.Types.Single(t => t.Source.Name.EndsWith("LeafGetterDerived", StringComparison.Ordinal));
        string type = "t_" + node.Source.ContentHash + "_" + node.Source.Token.ToString("x8", CultureInfo.InvariantCulture);
        int number = flow.Types.Types.Single(t => t.Source.Name.EndsWith("LeafGetterBase", StringComparison.Ordinal)).Fields.Select((f, i) => (f, i)).Single(p => p.f.Name == "Number").i;
        string source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap, foreign; Context cold(heap), other(foreign);
                auto* value = allocate<{{type}}>(cold, {{type}}_descriptor); assert(value);
                *static_cast<std::uint32_t*>(field_address(value, {{number}})) = 0x80000000u;
                LocationArgument args[1]{}; args[0].reference = value;
                assert({{getter}}(cold, args) == (-2147483647 - 1) && cold.pending == 0);
                assert(cold.peak > 0 && cold.depth == 0 && cold.used == 0);
                Context c(heap, 0); Object* roots[] = {value}; Frame frame; RootScope root(c, frame, roots, 1);
                auto* top = c.top;
                assert({{getter}}(c, args) == (-2147483647 - 1) && c.pending == 0);
                assert(c.used == 0 && c.peak == 0 && c.depth == 1 && c.top == top && c.borrows == nullptr);
                c.depth = 64; (void){{getter}}(c, args); assert(c.pending == 8);
                c.depth = 1; c.pending = 0;
                args[0].reference = nullptr;
                (void){{getter}}(c, args); assert(c.pending == 9); // Cold/fault path still reserves its old frame.
                Context faults(heap); Frame f; RootScope r(faults, f, roots, 1);
                (void){{getter}}(faults, args); assert(faults.pending != 0 && faults.method == {{flow.Methods.Single(m => m.Source.Token == typeof(LeafGetterBase).GetMethod("GetNumber")!.MetadataToken && m.Source.ContentHash == flow.Entry.ContentHash).Source.Token}}u && faults.il == 1u);
                if (CILICA_VERIFY_INTERNAL_REFERENCES) {
                    auto* alien = allocate<{{type}}>(other, {{type}}_descriptor);
                    for (auto* bad : {alien, reinterpret_cast<Object*>(std::uintptr_t{1})}) {
                        Context invalid(heap); Frame fi; RootScope ri(invalid, fi, roots, 1); args[0].reference = bad;
                        (void){{getter}}(invalid, args); assert(invalid.pending == 6 && invalid.used == 0 && invalid.depth == 1);
                    }
                }
                args[0].reference = value;
                args[0].reference = heap.emergency_exception;
                Context wrong(heap); Frame fw; RootScope rw(wrong, fw, roots, 1);
                (void){{getter}}(wrong, args); assert(wrong.pending == 6 && wrong.il == 0);
                args[0].reference = value;
                c.pending = 2; c.method = 123; c.il = 456;
                assert({{getter}}(c, args) == 0 && c.pending == 2 && c.method == 123 && c.il == 456);
            }
            """;
        using var files = new ResolverFiles(); string dir = files.DirectoryPath("leaf-getters");
        string cpp = Path.Combine(dir, "probe.cpp"), exe = Path.Combine(dir, "probe");
        File.WriteAllText(cpp, source, new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", optimized ? "-O2" : "-O0", "-DCILICA_VERIFY_INTERNAL_REFERENCES=" + (verify ? "1" : "0"),
             "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], dir);
        Assert.True(build.ExitCode == 0, build.Stdout + build.Stderr);
        var run = await NativeProcess.Run(exe, [], dir);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }

    [Fact]
    public async Task Getter_fast_paths_link_across_separate_translation_units()
    {
        var translated = IntegerObjectTranslationTests.Translate(nameof(LeafGetterProbe.Run), fixture: typeof(LeafGetterProbe));
        Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
        var module = ObjectCppEmitter.EmitExceptionModule(ValueScalarLowering.LowerExceptions(translated.Program!), methodsPerUnit: 1);
        Assert.Contains(module.Sources, file => file.Text.Contains("// Proven leaf getter:", StringComparison.Ordinal));
        using var files = new ResolverFiles(); string dir = files.DirectoryPath("leaf-module");
        foreach (var file in module.Sources.Prepend(module.Header)) File.WriteAllText(Path.Combine(dir, file.Name), file.Text);
        string entry = "cilica_m_" + module.Source.ContentHash + "_" + module.Source.Token.ToString("x8", CultureInfo.InvariantCulture);
        string main = Path.Combine(dir, "main.cpp");
        File.WriteAllText(main, $$"""
            #include "{{module.Header.Name}}"
            #include <cassert>
            using namespace {{module.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                LocationArgument args[2]{}; args[0].bits = 17; args[1].bits = std::bit_cast<std::uint32_t>(-3);
                assert({{entry}}(c, args) == {{LeafGetterProbe.Run(17, -3)}} && c.pending == 0);
                assert(c.used == 0 && c.depth == 0 && c.top == nullptr);
                assert(collect(c).status == MarkStatus::complete);
            }
            """);
        var objects = new List<string>(); string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        foreach (string cpp in module.Sources.Select(s => Path.Combine(dir, s.Name)).Append(main))
        {
            string obj = cpp + ".o";
            var build = await NativeProcess.Run(compiler,
                ["-std=c++20", "-O2", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", "-Wframe-larger-than=2048",
                 "-fsanitize=address,undefined", "-fno-sanitize-recover=all", "-c", cpp, "-o", obj], dir);
            Assert.True(build.ExitCode == 0, build.Stderr); objects.Add(obj);
        }
        string exe = Path.Combine(dir, "probe");
        var link = await NativeProcess.Run(compiler, ["-fsanitize=address,undefined", .. objects, "-o", exe], dir);
        Assert.True(link.ExitCode == 0, link.Stderr);
        var run = await NativeProcess.Run(exe, [], dir);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
