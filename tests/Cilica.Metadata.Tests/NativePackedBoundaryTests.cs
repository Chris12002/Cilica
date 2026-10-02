using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativePackedBoundaryTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Packed_exports_bound_native_stack_and_validate_inputs_before_touching_outputs(bool optimized)
    {
        var seed = NativeObjectExecutionTests.Translate(nameof(ObjectProbe.ArgumentBeforeNull));
        var original = seed.Flow.Methods.Single(m => IrMethodId.From(m.Source) == seed.Flow.Entry);
        var binding = seed.Bindings.Single(b => b.Method == seed.Flow.Entry);
        var source = new StringBuilder(); var checks = new StringBuilder();
        foreach (int count in new[] { 0, 1, 1024, 4096 })
        {
            var method = original with { Source = original.Source with { Token = 0x06001000 + count },
                Parameters = Enumerable.Repeat(IrType.Int32, count).ToImmutableArray(), Locals = [],
                Blocks = [count == 0
                    ? new IrBlock(0, 0, [], [new IrStatement.Define(new(0, IrType.Int32, 0, new IrExpression.Constant(42)))], new IrReturn(1, 0))
                    : new IrBlock(0, 0, [], [new IrStatement.Define(new(0, IrType.Int32, 0, new IrExpression.Parameter(0))),
                        new IrStatement.Define(new(1, IrType.Int32, 1, new IrExpression.Parameter(count - 1))),
                        new IrStatement.Define(new(2, IrType.Int32, 2, new IrExpression.Binary(Int32Operation.AddWrapping, 0, 1)))], new IrReturn(3, 2))] };
            var id = IrMethodId.From(method.Source);
            var program = seed with { Flow = seed.Flow with { Entry = id, Methods = [method], Types = new([seed.Flow.Types.ObjectBase], seed.Flow.Types.ObjectBase, [seed.Flow.Types.Types.Single(t => ObjectTypeId.From(t.Source) == seed.Flow.Types.ObjectBase)]) }, Bindings = [binding with { Method = id }] };
            var artifact = ObjectCppEmitter.EmitProgram(program); source.Append(artifact.Text);
            checks.Append(CultureInfo.InvariantCulture, $$"""
                { std::unique_ptr<std::int32_t[]> parameters(new std::int32_t[{{Math.Max(1, count)}}]{});
                  parameters[0] = 2147483647;
                  std::int32_t value = -17; std::uint32_t method = 18, il = 19; const char* image = "sentinel";
                  const char* sentinel = image;
                  assert({{artifact.EntryPoint}}(&value, &method, &il, &image, parameters.get(), {{count + 1}}) == 3u);
                  assert(value == -17 && method == 18 && il == 19 && image == sentinel);
                  assert({{artifact.EntryPoint}}(&value, &method, &il, &image, nullptr, std::numeric_limits<std::size_t>::max()) == 3u);
                  assert(value == -17 && method == 18 && il == 19 && image == sentinel);
                """);
            if (count != 0)
                checks.Append(CultureInfo.InvariantCulture, $$"""
                    assert({{artifact.EntryPoint}}(&value, &method, &il, &image, nullptr, {{count}}) == 3u);
                    assert(value == -17 && method == 18 && il == 19 && image == sentinel);
                    """);
            string pointer = count == 0 ? "nullptr" : "parameters.get()";
            int expected = count == 0 ? 42 : count == 1 ? -2 : int.MaxValue;
            checks.Append(CultureInfo.InvariantCulture, $$"""
                  assert({{artifact.EntryPoint}}(&value, &method, &il, &image, {{pointer}}, {{count}}) == 0u);
                  assert(value == {{expected}} && method == 0 && il == 0 && image == nullptr);
                }
                """);
        }
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("packed-boundary");
        string cpp = Path.Combine(directory, "packed.cpp"), executable = Path.Combine(directory, "packed");
        source.Append("#include <cassert>\n#include <memory>\n#include <limits>\nint main() {\n").Append(checks).Append("}\n");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        string[] common = ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", "-Wframe-larger-than=2048", optimized ? "-O2" : "-O0"];
        var build = await NativeProcess.Run(compiler, [.. common, "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr);
        var measure = await NativeProcess.Run(compiler, [.. common, "-fstack-usage", "-c", cpp, "-o", Path.Combine(directory, "measured.o")], directory);
        Assert.True(measure.ExitCode == 0, measure.Stderr);
        var exports = File.ReadAllLines(Path.Combine(directory, "measured.su")).Where(line => line.Contains("_objects_packed_try", StringComparison.Ordinal)).Select(line => line.Split('\t')).ToArray();
        Assert.Equal(4, exports.Length);
        Assert.All(exports, row => { Assert.Equal("static", row[^1]); Assert.InRange(int.Parse(row[^2], CultureInfo.InvariantCulture), 0, 2048); });
        output.WriteLine($"Packed export maximum O{(optimized ? 2 : 0)} stack: {exports.Max(row => int.Parse(row[^2], CultureInfo.InvariantCulture))} bytes, arities 0/1/1024/4096.");
    }
}
