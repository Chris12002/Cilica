using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeExceptionRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Private_exception_storage_preserves_roots_identity_messages_and_failure_origins(bool optimized)
    {
        var source = ManagedExceptionIrTests.Model(); var flow = source.Flow.Flow.Flow;
        var textType = source.Exceptions.Base with { Token = 0x0200000f };
        var references = flow.Types.References with { String = textType, Roots = flow.Types.References.Roots.Add(textType) };
        var method = flow.Methods[0] with
        {
            ExceptionRegions = [], Locals = [.. source.Exceptions.Types.Select(IrType.Reference), IrType.Reference(textType)],
            Blocks = [new(0, 0, [], [new IrStatement.Define(new(0, IrType.Int32, 0, new IrExpression.Constant(0)))], new IrReturn(1, 0))],
        };
        var scalar = new ScalarLocationIrProgram(new(new(flow.Entry, [method], references), source.Flow.Bindings));
        var artifact = ObjectCppEmitter.EmitExceptionRuntime(scalar, source.Exceptions);
        string stringDescriptor = "t_" + textType.ContentHash + "_" + textType.Token.ToString("x8", CultureInfo.InvariantCulture) + "_descriptor";
        string customDescriptor = "t_" + source.Exceptions.Types[^1].ContentHash + "_" + source.Exceptions.Types[^1].Token.ToString("x8", CultureInfo.InvariantCulture) + "_descriptor";
        string message = new Exception("A\0\ud800Z").Message;
        var cpp = new StringBuilder(artifact.Text).Append(CultureInfo.InvariantCulture, $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            [[gnu::noinline]] static void roots_and_identity() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                assert(prepare_exceptions(c) == 0 && heap.count == 1);
                Object* roots[2]{}; Frame frame; RootScope scope(c, frame, roots, 2);
                const std::uint16_t units[] = { {{string.Join(", ", message.Select(ch => ((ushort)ch).ToString(CultureInfo.InvariantCulture)))}} };
                roots[0] = allocate_string(c, {{stringDescriptor}}, units, 4, nullptr, 0); assert(roots[0]);
                auto made = make_exception(c, &{{customDescriptor}}, roots[0]); roots[1] = made.value;
                assert(made.status == 0 && made.value);
                assert(raise_exception(c, made.value, "throw", 17, 23) == 13);
                roots[0] = roots[1] = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 3);
                auto read = exception_message(c, c.exception);
                assert(read.status == 0 && string_length(read.value) == 4);
                for (std::uint32_t i = 0; i < 4; ++i) assert(string_code_unit(read.value, i) == units[i]);
                ExceptionOrigin origin; Object* original = c.exception;
                assert(suspend_exception(c, roots[1], origin));
                assert(c.pending == 0 && c.exception == nullptr && origin.status == 13 && origin.method == 17 && origin.il == 23);
                for (int i = 0; i < 20; ++i) assert(make_exception(c, runtime_exception(2), nullptr).status == 0);
                assert(collect(c).status == MarkStatus::complete && heap.count == 3);
                assert(restore_exception(c, roots[1], origin) && c.exception == original && c.method == 17 && c.il == 23);
                assert(!restore_exception(c, roots[1], origin));
                c.exception = nullptr; c.pending = 0; roots[1] = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
            }
            [[gnu::noinline]] static void runtime_faults_and_emergency_capacity() {
                Instance heap; heap.collect_before_allocation = true; heap.object_limit = 1; Context c(heap);
                assert(prepare_exceptions(c) == 0 && prepare_exceptions(c) == 0 && heap.count == 1);
                c.pending = 1; c.image = "division"; c.method = 19; c.il = 31;
                assert(materialize_exception(c) == 5 && c.exception == heap.emergency_exception && c.method == 19 && c.il == 31);
                c.exception = nullptr; c.pending = 0; heap.object_limit = 2;
                for (std::uint32_t status : {1u, 2u, 4u, 5u, 7u, 10u, 11u, 12u}) {
                    c.pending = status; c.method = 23; c.il = 41;
                    assert(materialize_exception(c) == status && c.exception->type == runtime_exception(status));
                    assert(collect(c).status == MarkStatus::complete && c.method == 23 && c.il == 41);
                    c.exception = nullptr; c.pending = 0;
                    assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                }
                assert(raise_exception(c, nullptr, "null", 29, 43) == 4 && c.exception->type == runtime_exception(4));
                c.exception = nullptr; c.pending = 0; assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                heap.limit = sizeof(ExceptionObject) * 2 - 1;
                c.pending = 2; assert(materialize_exception(c) == 5 && c.exception == heap.emergency_exception);
                c.exception = nullptr; c.pending = 0; heap.limit = sizeof(ExceptionObject) * 2;
                c.pending = 2; assert(materialize_exception(c) == 2 && c.exception != heap.emergency_exception);
            }
            [[gnu::noinline]] static void invalid_state_is_failure_atomic() {
                Instance heap, foreign; Context c(heap), other(foreign);
                assert(prepare_exceptions(c) == 0 && prepare_exceptions(other) == 0);
                Object* roots[2]{}; Frame frame; RootScope scope(c, frame, roots, 2);
                roots[0] = make_exception(c, &{{customDescriptor}}, nullptr).value;
                const auto count = heap.count; c.pending = 2; c.image = "original"; c.method = 37; c.il = 53;
                auto* forged = reinterpret_cast<Object*>(std::uintptr_t{1});
                auto* unknown = reinterpret_cast<const Descriptor*>(std::uintptr_t{1});
                const Descriptor copied = {{customDescriptor}};
                for (const Descriptor* bad : {unknown, &copied, &{{stringDescriptor}}}) assert(make_exception(c, bad, nullptr).status == 6);
                for (Object* bad : {forged, foreign.emergency_exception, roots[0]}) assert(make_exception(c, runtime_exception(1), bad).status == 6);
                for (Object* bad : {forged, foreign.emergency_exception}) assert(exception_message(c, bad).status == 6);
                assert(exception_message(c, nullptr).status == 4);
                assert(heap.count == count && c.pending == 2 && c.method == 37 && c.il == 53 && c.exception == nullptr);
                heap.marking = true;
                assert(make_exception(c, runtime_exception(1), nullptr).status == 6);
                assert(prepare_exceptions(c) == 6 && exception_message(c, roots[0]).status == 6);
                assert(c.pending == 2 && c.method == 37 && c.il == 53 && heap.count == count);
                heap.marking = false; c.pending = 0;
                assert(!restore_exception(c, roots[0], {6, "bad", 1, 2}));
                assert(raise_exception(c, foreign.emergency_exception, "bad", 1, 2) == 6 && c.pending == 0);
                auto* payload = reinterpret_cast<ExceptionObject*>(roots[0]); payload->message = forged;
                assert(exception_message(c, roots[0]).status == 6);
                assert(collect(c).status == MarkStatus::invalid && heap.count == count);
                payload->message = nullptr; roots[0] = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                c.pending = 6; assert(materialize_exception(c) == 6 && c.exception == nullptr);
            }
            [[gnu::noinline]] static void bootstrap_failure_is_bounded() {
                Instance heap; heap.limit = sizeof(ExceptionObject) - 1; Context c(heap);
                assert(prepare_exceptions(c) == 5 && heap.count == 0 && heap.emergency_exception == nullptr && c.pending == 0);
                heap.limit = sizeof(ExceptionObject); assert(prepare_exceptions(c) == 0 && heap.count == 1);
            }
            int main() { roots_and_identity(); runtime_faults_and_emergency_capacity(); invalid_state_is_failure_atomic(); bootstrap_failure_is_bounded(); }
            """);
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("native-exceptions");
        string input = Path.Combine(directory, "exceptions.cpp"), executable = Path.Combine(directory, "exceptions");
        File.WriteAllText(input, cpp.ToString(), new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", input, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
