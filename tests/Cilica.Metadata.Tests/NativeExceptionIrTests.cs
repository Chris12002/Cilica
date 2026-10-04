using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeExceptionIrTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Authored_catches_filters_finally_and_rethrow_keep_identity_and_cleanup(bool optimized)
    {
        var source = new StringBuilder(); var calls = new StringBuilder();
        for (int kind = 0; kind < 5; ++kind)
        {
            var program = kind is 1 or 2 ? ManagedExceptionIrTests.FilterModel() : ManagedExceptionIrTests.Model();
            var flow = program.Flow.Flow.Flow; var method = flow.Methods[0];
            if (kind == 2)
            {
                var filter = method.Blocks[1]; var test = (IrStatement.Define)filter.Statements[1];
                method = method with { Blocks = method.Blocks.SetItem(1, filter with { Statements = filter.Statements.SetItem(1,
                    test with { Value = test.Value with { Type = IrType.Reference(program.Exceptions.Types[1]),
                        Expression = new IrExpression.IsInstance(program.Exceptions.Types[1], 0) } }) }) };
            }
            if (kind == 3) method = method with { Blocks = method.Blocks.SetItem(0, method.Blocks[0] with
                { Statements = [new IrStatement.Define(new(0, IrType.NullReference, 0, new IrExpression.Null()))] }) };
            if (kind == 4) method = method with { Blocks = method.Blocks.SetItem(1, method.Blocks[1] with { Terminator = new IrRethrow(3) }).RemoveAt(3) };
            method = method with { Source = method.Source with { Specialization = new string((char)('1' + kind), 64) } };
            var entry = IrMethodId.From(method.Source); flow = flow with { Entry = entry, Methods = [method] };
            program = program with { Flow = program.Flow with { Flow = new(flow), Bindings = [program.Flow.Bindings[0] with { Method = entry }] } };
            var scalar = ValueScalarLowering.LowerExceptions(program);
            var artifact = ObjectCppEmitter.EmitExceptions(scalar);
            string symbol = "cilica_m_" + entry.ContentHash + "_" + entry.Token.ToString("x8", CultureInfo.InvariantCulture) + "_g_" + entry.Specialization;
            uint status = kind is 2 or 4 ? 13u : 0u;
            source.Append(artifact.Text).Append(CultureInfo.InvariantCulture, $$"""
                #include <cassert>
                [[gnu::noinline]] static void run_{{kind}}() {
                    using namespace {{artifact.EntryPoint[..^4]}}_closure;
                    Instance heap; heap.collect_before_allocation = true; Context c(heap);
                    for (int repeat = 0; repeat < 3; ++repeat) {
                        auto result = {{symbol}}(c, nullptr);
                        assert(c.pending == {{status}}u && ({{status}}u != 0 || result == 5));
                        if ({{status}}u != 0) assert(c.exception != nullptr && c.method == {{entry.Token}}u && c.il == 1);
                        else assert(c.exception == nullptr);
                        assert(c.top == nullptr && c.depth == 0 && c.borrows == nullptr && c.used == 0);
                        c.exception = nullptr; c.pending = 0;
                        assert(collect(c).status == MarkStatus::complete && heap.count == 1 && heap.head == heap.emergency_exception);
                    }
                    std::int32_t result = 123; std::uint32_t token = 0, offset = 0; const char* image = nullptr;
                    assert({{artifact.EntryPoint}}(&result, &token, &offset, &image, nullptr, 0) == {{status}}u);
                    assert(result == ({{status}}u == 0 ? 5 : 123));
                    if ({{status}}u != 0) assert(token == {{entry.Token}}u && offset == 1);
                }
                """);
            calls.Append(CultureInfo.InvariantCulture, $"run_{kind}();\n");
        }
        source.Append("int main() {\n").Append(calls).Append("}\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("native-exception-ir");
        string cpp = Path.Combine(directory, "exceptions.cpp"), executable = Path.Combine(directory, "exceptions");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
