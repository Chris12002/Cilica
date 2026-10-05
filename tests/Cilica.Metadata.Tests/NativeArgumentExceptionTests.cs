using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeArgumentExceptionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Parameter_payload_is_traced_and_format_failures_preserve_origin(bool optimized)
    {
        var translated = IntegerObjectTranslationTests.Translate("NullArgument", fixture: typeof(ArgumentExceptionProbe));
        Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
        var program = translated.Program!;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string Symbol(ObjectTypeId type) => $"t_{type.ContentHash}_{type.Token:x8}_descriptor";
        var argument = program.Exceptions.Resources.Single(r => r.Kind == ExceptionResourceKind.ArgumentNull).Type;
        var text = program.Flow.Flow.Flow.Types.References.String!;
        string expected = new ArgumentNullException("x\ud800", "bad\0").Message;
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            [[gnu::noinline]] static void parameter_payload() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                assert(prepare_exceptions(c) == 0);
                Object* roots[3]{}; Frame frame; RootScope scope(c, frame, roots, 3);
                static constexpr std::uint16_t name[] = {120, 0xd800}, message[] = {98, 97, 100, 0};
                roots[0] = allocate_string(c, {{Symbol(text)}}, name, 2, nullptr, 0);
                roots[1] = allocate_string(c, {{Symbol(text)}}, message, 4, nullptr, 0);
                auto made = make_exception(c, &{{Symbol(argument)}}, roots[1], 0, roots[0]);
                assert(made.status == 0); roots[2] = made.value; roots[0] = roots[1] = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 4);
                auto* payload = reinterpret_cast<ExceptionObject*>(roots[2]);
                auto parameter = exception_parameter_name(c, roots[2]);
                assert(parameter.status == 0 && parameter.value == payload->parameter_name && string_code_unit(parameter.value, 1) == 0xd800);
                heap.limit = heap.bytes; c.pending = 2; c.image = "saved"; c.method = 17; c.il = 31;
                auto failed = exception_message(c, roots[2]);
                assert(failed.status == 5 && failed.value == nullptr && c.pending == 2 && c.method == 17 && c.il == 31);
                assert(exception_parameter_name(c, roots[2]).value == parameter.value && heap.count == 4);
                heap.limit = 1024 * 1024; c.pending = 0;
                auto formatted = exception_message(c, roots[2]);
                assert(formatted.status == 0); roots[0] = formatted.value;
                static constexpr std::uint16_t expected[] = { {{string.Join(", ", expected.Select(ch => ((ushort)ch).ToString(System.Globalization.CultureInfo.InvariantCulture)))}} };
                assert(string_length(formatted.value) == {{expected.Length}});
                for (std::uint32_t i = 0; i < {{expected.Length}}; ++i) assert(string_code_unit(formatted.value, i) == expected[i]);
                assert(exception_message(c, roots[2]).value != roots[0]);
                auto* saved = payload->parameter_name; payload->parameter_name = reinterpret_cast<Object*>(std::uintptr_t{1});
                assert(exception_parameter_name(c, roots[2]).status == 6 && exception_message(c, roots[2]).status == 6);
                assert(collect(c).status == MarkStatus::invalid); payload->parameter_name = saved;
                assert(exception_parameter_name(c, nullptr).status == 4);
                assert(make_exception(c, runtime_exception(1), nullptr, 0, saved).status == 6);
                roots[0] = roots[2] = nullptr; assert(collect(c).status == MarkStatus::complete && heap.count == 1);
            }
            [[gnu::noinline]] static void resources() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                assert(prepare_exceptions(c) == 0);
                Object* roots[3]{}; Frame frame; RootScope scope(c, frame, roots, 3);
                for (auto status : {1u, 2u, 4u, 5u, 7u, 10u, 11u, 12u}) {
                    roots[2] = make_exception(c, runtime_exception(status), nullptr, 2).value;
                    assert(exception_message(c, roots[2]).status == 0);
                }
                roots[2] = make_exception(c, runtime_exception(12), nullptr, 1).value;
                assert(exception_message(c, roots[2]).status == 0);
                for (auto* type : { {{string.Join(", ", program.Exceptions.Resources.Select(r => "&" + Symbol(r.Type)))}} }) {
                    roots[2] = make_exception(c, type, nullptr, 1).value;
                    assert(exception_message(c, roots[2]).status == 0);
                }
                // All selected cached resources survive repeated collection. System
                // and Arithmetic resources exceed the former fixed 13-entry cache.
                assert(exception_resource_capacity == 15);
                roots[2] = nullptr; assert(collect(c).status == MarkStatus::complete && heap.count == 16);
                auto* first = heap.exception_resources;
                auto* saved = first->literal_next; first->literal_next = first;
                assert(collect(c).status == MarkStatus::invalid);
                first->literal_next = saved;
                assert(collect(c).status == MarkStatus::complete && heap.count == 16);
            }
            int main() { parameter_payload(); resources(); }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("argument-runtime");
        string cpp = Path.Combine(directory, "arguments.cpp"), exe = Path.Combine(directory, "arguments");
        File.WriteAllText(cpp, source);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
