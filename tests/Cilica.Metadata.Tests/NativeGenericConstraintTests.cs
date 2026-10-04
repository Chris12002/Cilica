using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeGenericConstraintTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Scalar_only_and_constrained_specializations_match_dotnet(bool optimized)
    {
        var source = new StringBuilder(); var calls = new StringBuilder();
        Add("scalar", ClosedGenericTranslationTests.Translate("Primitive"), ClosedGenericProbe.Primitive);
        Add("constraints", GenericConstraintTests.Authored(), GenericConstraintProbe.Execute);
        source.Append("int main() {\n").Append(calls).Append("}\n");
        void Add(string name, BoxedValueTranslationResult translated, Func<int, int> oracle)
        {
            Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
            var program = translated.Program!;
            var artifact = ObjectCppEmitter.EmitBoxes(ValueScalarLowering.LowerBoxes(program));
            var id = program.Flow.Flow.Entry;
            string entry = "cilica_m_" + id.ContentHash + "_" + id.Token.ToString("x8", CultureInfo.InvariantCulture);
            source.Append(artifact.Text).Append(CultureInfo.InvariantCulture, $$"""
                #include <cassert>
                [[gnu::noinline]] static void {{name}}(std::int32_t number, std::int32_t expected) {
                    using namespace {{artifact.EntryPoint[..^4]}}_closure;
                    Instance heap; heap.collect_before_allocation = true; heap.object_limit = 16; Context c(heap);
                    LocationArgument arg; arg.bits = std::bit_cast<std::uint32_t>(number);
                    for (int repeat = 0; repeat < 3; ++repeat) {
                        assert({{entry}}(c, &arg) == expected && c.pending == 0);
                        assert(c.top == nullptr && c.depth == 0 && c.borrows == nullptr && c.used == 0);
                        assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                    }
                    std::int32_t output = 123; std::uint32_t method = 0, offset = 0; const char* image = nullptr;
                    assert({{artifact.EntryPoint}}(&output, &method, &offset, &image, &number, 1) == 0 && output == expected);
                }
                """);
            foreach (int number in new[] { 0, -19, int.MinValue, int.MaxValue })
                calls.Append(CultureInfo.InvariantCulture, $"{name}({number}, {oracle(number)});\n");
        }
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("native-generic-constraints");
        string cpp = Path.Combine(directory, "generic.cpp"), executable = Path.Combine(directory, "generic");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
