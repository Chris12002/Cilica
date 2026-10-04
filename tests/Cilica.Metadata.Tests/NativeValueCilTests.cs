using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeValueCilTests
{
    [Theory]
    [InlineData(false, "returns")]
    [InlineData(true, "returns")]
    [InlineData(false, "borrows")]
    [InlineData(true, "borrows")]
    [InlineData(false, "branches")]
    [InlineData(true, "branches")]
    [InlineData(false, "copy_overlap")]
    [InlineData(true, "copy_overlap")]
    public async Task Translated_CIL_matches_dotnet_with_native_sanitizers(bool optimized, string kind)
    {
        ValueTranslationResult result;
        if (kind == "copy_overlap")
        {
            var (builder, method) = ValueTranslationTests.Fixture(kind);
            result = ValueTranslationTests.Read(builder, method);
        }
        else result = ValueTranslationTests.Authored(kind switch
        {
            "returns" => typeof(ValueReturnProbe), "borrows" => typeof(ValueBorrowProbe), _ => typeof(ValueCilProbe),
        });
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!;
        var artifact = ObjectCppEmitter.EmitLocations(ValueScalarLowering.LowerLocations(program, result.Bindings));
        var entry = program.Flow.Methods.Single(m => IrMethodId.From(m.Source) == program.Flow.Entry);
        var division = program.Flow.Methods.SelectMany(m => m.Blocks.SelectMany(b => b.Values)
            .Where(v => v.Expression is IrExpression.Binary { Operation: Int32Operation.DivideSigned }).Select(v => (m.Source.Token, v.ILOffset))).ToArray();
        var source = new StringBuilder(artifact.Text).Append("\n#include <cassert>\nint main() {\n");
        foreach (int number in new[] { 0, 1, -19, int.MinValue, int.MaxValue })
        {
            foreach (int second in kind == "returns" ? new[] { 1, 3, 0, -1 } : kind == "branches" ? new[] { -1, 0, 4 } : new[] { 0 })
            {
                int expected = 0; uint status = 0;
                try
                {
                    expected = kind switch
                    {
                        "returns" => ValueReturnProbe.Execute(number, second), "borrows" => ValueBorrowProbe.Execute(number),
                        "branches" => ValueCilProbe.Execute(number, second), _ => 37,
                    };
                }
                catch (DivideByZeroException) { status = 1; }
                catch (OverflowException) { status = 2; }
                string inputs = entry.Parameters.Length == 1 ? number.ToString(CultureInfo.InvariantCulture)
                    : number.ToString(CultureInfo.InvariantCulture) + ", " + second.ToString(CultureInfo.InvariantCulture);
                source.Append(CultureInfo.InvariantCulture, $$"""
                    {
                        std::int32_t arguments[] = { {{inputs}} }; std::int32_t output = 123;
                        std::uint32_t method = 0, offset = 0; const char* image = nullptr;
                        assert({{artifact.EntryPoint}}(&output, &method, &offset, &image, arguments, {{entry.Parameters.Length}}) == {{status}}u);
                        assert(output == {{(status == 0 ? expected : 123)}});
                    """);
                if (status != 0)
                {
                    var (token, offset) = Assert.Single(division);
                    source.Append(CultureInfo.InvariantCulture, $"assert(method == {token}u && offset == {offset}u);\n");
                }
                source.Append("}\n");
            }
        }
        source.Append("}\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("value-cil-" + kind);
        string cpp = Path.Combine(directory, "probe.cpp"), executable = Path.Combine(directory, "probe");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
             "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
