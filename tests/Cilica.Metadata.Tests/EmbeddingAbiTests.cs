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

public sealed class EmbeddingAbiTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(EmbeddingProbe), nameof(EmbeddingProbe.Step), nameof(EmbeddingProbe.Catch)));
    private static readonly Lazy<int[]> Expected = new(() =>
    {
        EmbeddingState.Held = null; EmbeddingState.Number = 0;
        return Enumerable.Range(0, 100).Select(i =>
        {
            try { return EmbeddingProbe.Step(10, i % 7 == 0 ? -3 : 2); }
            catch (InvalidOperationException) { return 0; }
        }).ToArray();
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task C_consumer_links_shared_library_and_preserves_state_faults_and_lifetimes(bool optimized)
    {
        var artifact = EmbeddingCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions((await Programs.Value)[0]));
        var caught = EmbeddingCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions((await Programs.Value)[1]));
        string api = artifact.SymbolPrefix;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("embedding-c");
        File.WriteAllText(Path.Combine(directory, "engine.h"), artifact.Header);
        File.WriteAllText(Path.Combine(directory, "caught.h"), caught.Header);
        File.WriteAllText(Path.Combine(directory, "engine.cpp"), artifact.Text + caught.Text);
        File.WriteAllText(Path.Combine(directory, "consumer.c"), $$"""
            #include "engine.h"
            #include "engine.h"
            #include "caught.h"
            #include <assert.h>
            #include <stddef.h>
            #include <string.h>
            _Static_assert(sizeof(((cilica_v1_result*)0)->value) == 4, "Int32 result");
            _Static_assert(offsetof(cilica_v1_result, image_hash) == 16, "C layout");
            static const int32_t expected[100] = { {{string.Join(", ", Expected.Value.Select(n => n.ToString(CultureInfo.InvariantCulture)))}} };
            int main(void) {
                assert({{api}}_version() == 1);
                uint64_t invalid = 99;
                assert({{api}}_create(2, 0, &invalid) == CILICA_V1_UNSUPPORTED_VERSION && invalid == 99);
                assert({{api}}_create(1, 2, &invalid) == CILICA_V1_INVALID_ARGUMENT && invalid == 99);
                assert({{api}}_create(1, 0, NULL) == CILICA_V1_INVALID_ARGUMENT);
                assert({{api}}_destroy(0) == CILICA_V1_INVALID_RUNTIME);
                assert({{api}}_destroy(UINT64_MAX) == CILICA_V1_INVALID_RUNTIME);
                for (uint32_t flags = 0; flags <= 1; ++flags) {
                    for (int cycle = 0; cycle < 3; ++cycle) {
                        uint64_t runtime = 0, other = 0;
                        assert({{api}}_create(1, flags, &runtime) == CILICA_V1_OK && runtime != 0);
                        assert({{api}}_create(1, flags, &other) == CILICA_V1_OK && other != runtime);
                        cilica_v1_result result = {0}; result.value = 123;
                        int32_t args[2] = {10, 2};
                        assert({{api}}_invoke(runtime, args, 1, &result) == CILICA_V1_INVALID_ARGUMENT && result.value == 123);
                        assert({{api}}_invoke(runtime, NULL, 2, &result) == CILICA_V1_INVALID_ARGUMENT && result.value == 123);
                        assert({{api}}_invoke(runtime, args, 2, NULL) == CILICA_V1_INVALID_ARGUMENT);
                        assert({{api}}_invoke(0, args, 2, &result) == CILICA_V1_INVALID_RUNTIME && result.value == 123);
                        cilica_v1_result saved = {0};
                        for (int i = 0; i < 100; ++i) {
                            args[1] = i % 7 == 0 ? -3 : 2;
                            uint32_t status = {{api}}_invoke(runtime, args, 2, &result);
                            assert(result.value == expected[i]);
                            if (args[1] < 0) {
                                assert(status == CILICA_V1_EXECUTION_FAILED && result.fault_code == 13);
                                assert(result.method_token != 0 && result.il_offset != 0 && strlen(result.image_hash) == 64);
                                saved = result;
                            } else {
                                assert(status == CILICA_V1_OK && result.fault_code == 0);
                                assert(result.method_token == 0 && result.il_offset == 0 && result.image_hash[0] == 0);
                            }
                        }
                        args[0] = 20; args[1] = 3;
                        assert({{api}}_invoke(other, args, 2, &result) == CILICA_V1_OK && result.value == 26);
                        assert({{api}}_destroy(runtime) == CILICA_V1_OK);
                        assert({{api}}_destroy(runtime) == CILICA_V1_INVALID_RUNTIME);
                        assert({{api}}_invoke(runtime, args, 2, &result) == CILICA_V1_INVALID_RUNTIME && result.value == 26);
                        assert(strlen(saved.image_hash) == 64 && saved.fault_code == 13);
                        uint64_t replacement = 0;
                        assert({{api}}_create(1, flags, &replacement) == CILICA_V1_OK && replacement != runtime);
                        assert({{api}}_invoke(runtime, args, 2, &result) == CILICA_V1_INVALID_RUNTIME);
                        assert({{api}}_invoke(replacement, args, 2, &result) == CILICA_V1_OK && result.value == 26);
                        assert({{api}}_destroy(replacement) == CILICA_V1_OK);
                        assert({{api}}_destroy(other) == CILICA_V1_OK);
                    }
                }
                uint64_t runtimes[64];
                for (int i = 0; i < 64; ++i) assert({{api}}_create(1, 0, &runtimes[i]) == CILICA_V1_OK);
                assert({{api}}_create(1, 0, &invalid) == CILICA_V1_LIMIT && invalid == 99);
                for (int i = 0; i < 64; ++i) assert({{api}}_destroy(runtimes[i]) == CILICA_V1_OK);
                uint64_t caught = 0;
                assert({{caught.SymbolPrefix}}_create(1, 1, &caught) == CILICA_V1_OK);
                int32_t args[] = {10, -3}; cilica_v1_result result = {0};
                assert({{caught.SymbolPrefix}}_invoke(caught, args, 2, &result) == CILICA_V1_OK && result.value == 42);
                assert(result.fault_code == 0 && result.method_token == 0 && result.il_offset == 0 && result.image_hash[0] == 0);
                assert({{caught.SymbolPrefix}}_destroy(caught) == CILICA_V1_OK);
                return 0;
            }
            """);
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        string[] sanitize = ["-fsanitize=address,undefined", "-fno-sanitize-recover=all"];
        string[] warnings = ["-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0"];
        string library = Path.Combine(directory, OperatingSystem.IsMacOS() ? "libengine.dylib" : "libengine.so");
        await Check(compiler, ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-fvisibility=hidden", "-fPIC",
            OperatingSystem.IsMacOS() ? "-dynamiclib" : "-shared", .. warnings, .. sanitize, "engine.cpp", "-o", library], directory);
        await Check(compiler, ["-x", "c", "-std=c11", .. warnings, .. sanitize, "-c", "consumer.c", "-o", "consumer.o"], directory);
        string executable = Path.Combine(directory, "consumer");
        await Check(compiler, [.. sanitize, "consumer.o", library, "-o", executable], directory);
        await Check(executable, [], directory);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Concurrent_calls_and_destroy_races_do_not_expose_heap_pointers(bool optimized)
    {
        var artifact = EmbeddingCppEmitter.EmitProgram(NativeObjectExecutionTests.Translate(nameof(ObjectProbe.AcrossCalls)));
        var seed = NativeObjectExecutionTests.Translate(nameof(ObjectProbe.Division));
        var original = seed.Flow.Methods.Single(m => IrMethodId.From(m.Source) == seed.Flow.Entry);
        var method = original with { Parameters = [], Blocks = [new(0, 0, [],
            [new IrStatement.Define(new(0, IrType.Int32, 0, new IrExpression.Constant(42)))], new IrReturn(1, 0))], Locals = [] };
        var zero = EmbeddingCppEmitter.EmitProgram(seed with { Flow = seed.Flow with { Methods = [method],
            Types = new([seed.Flow.Types.ObjectBase], seed.Flow.Types.ObjectBase,
                [seed.Flow.Types.Types.Single(t => ObjectTypeId.From(t.Source) == seed.Flow.Types.ObjectBase)]) },
            Bindings = [seed.Bindings.Single(b => b.Method == seed.Flow.Entry)] });
        string api = artifact.SymbolPrefix;
        string source = artifact.Text + zero.Text + $$"""
            #include <cassert>
            #include <thread>
            #include <barrier>
            int main() {
                uint64_t empty = 0; cilica_v1_result constant{};
                assert({{zero.SymbolPrefix}}_create(1, 0, &empty) == CILICA_V1_OK);
                assert({{zero.SymbolPrefix}}_invoke(empty, nullptr, 0, &constant) == CILICA_V1_OK && constant.value == 42);
                assert({{zero.SymbolPrefix}}_destroy(empty) == CILICA_V1_OK);
                // Independent instances may be invoked on separate host threads.
                std::array<uint64_t, 4> independent{};
                std::array<std::thread, 4> separate;
                for (std::size_t i = 0; i < independent.size(); ++i) {
                    assert({{api}}_create(1, 1, &independent[i]) == CILICA_V1_OK);
                    separate[i] = std::thread([&, i] {
                        const int32_t args[] = {static_cast<int32_t>(i), 2}; cilica_v1_result result{};
                        for (int call = 0; call < 100; ++call)
                            assert({{api}}_invoke(independent[i], args, 2, &result) == CILICA_V1_OK && result.value == static_cast<int32_t>(i) + 4);
                    });
                }
                for (std::size_t i = 0; i < separate.size(); ++i) {
                    separate[i].join(); assert({{api}}_destroy(independent[i]) == CILICA_V1_OK);
                }
                // Same-instance contention must either complete or reject entry.
                uint64_t runtime = 0;
                assert({{api}}_create(1, 1, &runtime) == CILICA_V1_OK);
                std::array<std::thread, 4> workers;
                std::barrier start(4);
                for (auto& worker : workers) worker = std::thread([&] {
                    start.arrive_and_wait();
                    for (int i = 0; i < 100; ++i) {
                        const int32_t args[] = {i, 2}; cilica_v1_result result{}; result.value = -1;
                        auto status = {{api}}_invoke(runtime, args, 2, &result);
                        assert((status == CILICA_V1_OK && result.value == i + 4)
                            || (status == CILICA_V1_BUSY && result.value == -1));
                    }
                });
                for (auto& worker : workers) worker.join();
                assert({{api}}_destroy(runtime) == CILICA_V1_OK);
                // Invoke racing destruction sees either a live busy instance or a stale token.
                for (int i = 0; i < 100; ++i) {
                    assert({{api}}_create(1, 1, &runtime) == CILICA_V1_OK);
                    std::barrier race(2);
                    std::thread caller([&] {
                        const int32_t args[] = {3, 4}; cilica_v1_result result{};
                        race.arrive_and_wait();
                        auto status = {{api}}_invoke(runtime, args, 2, &result);
                        assert((status == CILICA_V1_OK && result.value == 11) || status == CILICA_V1_INVALID_RUNTIME);
                    });
                    race.arrive_and_wait();
                    auto status = {{api}}_destroy(runtime);
                    assert(status == CILICA_V1_OK || status == CILICA_V1_BUSY);
                    caller.join();
                    if (status == CILICA_V1_BUSY) assert({{api}}_destroy(runtime) == CILICA_V1_OK);
                    assert({{api}}_destroy(runtime) == CILICA_V1_INVALID_RUNTIME);
                }
                // Exhaustion must not wrap a runtime token back to a stale value.
                {{api}}_private::next_token = UINT64_MAX;
                uint64_t unchanged = 123;
                assert({{api}}_create(1, 0, &unchanged) == CILICA_V1_LIMIT && unchanged == 123);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("embedding-concurrency");
        File.WriteAllText(Path.Combine(directory, "concurrency.cpp"), source, new UTF8Encoding(false));
        string executable = Path.Combine(directory, "concurrency");
        await Check(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", "-pthread",
                optimized ? "-O2" : "-O0", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", "concurrency.cpp", "-o", executable], directory);
        await Check(executable, [], directory);
    }

    private static async Task Check(string executable, string[] arguments, string directory)
    {
        var result = await NativeProcess.Run(executable, arguments, directory);
        Assert.True(result.ExitCode == 0, result.Stdout + result.Stderr);
        Assert.Empty(result.Stderr);
    }
}
