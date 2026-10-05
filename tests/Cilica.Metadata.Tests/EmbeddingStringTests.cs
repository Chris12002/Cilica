using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class EmbeddingStringTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram>> Program = new(async () =>
        (await CoreLibraryBuildTests.TranslateFixtures(typeof(EmbeddingStringProbe), nameof(EmbeddingStringProbe.Root)))[0]);

    private static EmbeddingObjectContract Contract(ManagedExceptionIrProgram program)
    {
        var methods = program.Flow.Flow.Flow.Methods;
        IrMethod Method(string name) => methods.Single(m => m.Source.Token == typeof(EmbeddingStringProbe).GetMethod(name)!.MetadataToken);
        var text = new EmbeddingValueShape(EmbeddingValueKind.Handle, 1);
        var scalar = new EmbeddingValueShape(EmbeddingValueKind.Int32);
        return new("strings", 1, [new(1, "text", Method("Echo").ReturnType.Class!), new(2, "other", Method("Other").ReturnType.Class!)], [
            new(1, "echo", IrMethodId.From(Method("Echo").Source), [text], text),
            new(2, "append", IrMethodId.From(Method("Append").Source), [text], text),
            new(3, "length", IrMethodId.From(Method("Length").Source), [text], scalar),
            new(4, "unit", IrMethodId.From(Method("Unit").Source), [text, scalar], scalar),
            new(5, "other", IrMethodId.From(Method("Other").Source), [], new(EmbeddingValueKind.Handle, 2))], 1);
    }

    [Fact]
    public async Task String_capability_requires_exact_string_binding_and_changes_contract()
    {
        var program = await Program.Value; var contract = Contract(program);
        var enabled = EmbeddingObjectModuleEmitter.EmitExceptions(program, contract);
        var disabled = EmbeddingObjectModuleEmitter.EmitExceptions(program, contract with { Utf16StringTypeId = 0 });
        Assert.NotEqual(enabled.ContractHash, disabled.ContractHash);
        Assert.Contains("utf16StringTypeId", enabled.ManifestJson, StringComparison.Ordinal);
        Assert.DoesNotContain("utf16StringTypeId", disabled.ManifestJson, StringComparison.Ordinal);
        Assert.DoesNotContain("_string_create", disabled.Header, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => EmbeddingObjectModuleEmitter.EmitExceptions(program, contract with { Utf16StringTypeId = 2 }));
        Assert.Throws<ArgumentException>(() => EmbeddingObjectModuleEmitter.EmitExceptions(program, contract with { Utf16StringTypeId = 99 }));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task C_host_copies_exact_utf16_with_owned_buffers_and_bounded_handles(bool optimized)
    {
        var program = await Program.Value;
        var artifact = EmbeddingObjectModuleEmitter.EmitExceptions(program, Contract(program));
        string api = artifact.SymbolPrefix;
        // Authored oracle covers every UTF-16 unit, including embedded zero and lone surrogates.
        string oracle = new(Enumerable.Range(0, 65536).Select(i => (char)i).ToArray());
        string appended = EmbeddingStringProbe.Append(oracle);
        Assert.Equal(65539, appended.Length);
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("embedding-strings");
        File.WriteAllText(Path.Combine(directory, "engine.h"), artifact.Header);
        File.WriteAllText(Path.Combine(directory, "engine.cpp"), artifact.Text);
        File.WriteAllText(Path.Combine(directory, "consumer.c"), $$"""
            #include "engine.h"
            #include <assert.h>
            #include <string.h>
            static uint16_t input[65536], output[65540];
            static uint64_t make(uint64_t runtime, const uint16_t* units, uint32_t count) {
                uint64_t result = 0;
                assert({{api}}_string_create(runtime, units, count, &result) == CILICA_V3_OK && result != 0);
                return result;
            }
            int main(void) {
                assert({{api}}_capabilities() == 511);
                for (uint32_t i = 0; i < 65536; ++i) input[i] = (uint16_t)i;
                const char* hash = CILICA_STRINGS_ABI_V3_CONTRACT_HASH;
                cilica_v3_config config = CILICA_V3_CONFIG_INIT;
                config.handle_limit = 8; config.object_limit = 12;
                for (uint32_t stress = 0; stress < 2; ++stress) {
                    config.flags = stress;
                    uint64_t runtime = 0, other = 0, sentinel = 123;
                    assert({{api}}_create(3, hash, &config, &runtime) == 0);
                    assert({{api}}_create(3, hash, &config, &other) == 0);
                    uint64_t text = make(runtime, input, 65536);
                    input[65] = 999; /* Imports copy, never borrow the host buffer. */
                    uint32_t length = 123; output[0] = 777;
                    assert({{api}}_string_copy(runtime, text, NULL, 0, &length) == CILICA_V3_BUFFER_TOO_SMALL && length == 65536);
                    length = 123;
                    assert({{api}}_string_copy(runtime, text, output, 65535, &length) == CILICA_V3_BUFFER_TOO_SMALL && length == 65536 && output[0] == 777);
                    output[65536] = 777;
                    assert({{api}}_string_copy(runtime, text, output, 65537, &length) == 0 && length == 65536 && output[65536] == 777);
                    for (uint32_t i = 0; i < length; ++i) assert(output[i] == i);
                    input[65] = 65;
                    cilica_v3_value args[2] = { {2, 0, text}, {1, 0, 0xd800} }; cilica_v3_result result = {0};
                    assert({{api}}_invoke(runtime, 3, args, 1, &result) == 0 && result.value.payload == {{EmbeddingStringProbe.Length(oracle)}});
                    assert({{api}}_invoke(runtime, 4, args, 2, &result) == 0 && result.value.payload == {{EmbeddingStringProbe.Unit(oracle, 0xd800)}});
                    assert({{api}}_invoke(runtime, 1, args, 1, &result) == 0 && result.value.payload != text);
                    uint64_t alias = result.value.payload;
                    assert({{api}}_invoke(runtime, 2, args, 1, &result) == 0);
                    uint64_t joined = result.value.payload;
                    assert({{api}}_release_handle(runtime, text) == 0);
                    assert({{api}}_string_copy(runtime, alias, output, 65536, &length) == 0 && length == 65536);
                    assert({{api}}_string_copy(runtime, joined, output, 65539, &length) == 0 && length == {{appended.Length}});
                    for (uint32_t i = 0; i < 65536; ++i) assert(output[i] == i);
                    assert(output[65536] == {{(int)appended[65536]}} && output[65537] == {{(int)appended[65537]}} && output[65538] == {{(int)appended[65538]}});
                    assert({{api}}_release_handle(runtime, alias) == 0);
                    assert({{api}}_release_handle(runtime, joined) == 0);
                    /* Copied output survives lease release and future collection. */
                    for (int i = 0; i < 40; ++i) {
                        uint64_t empty = make(runtime, NULL, 0);
                        assert({{api}}_string_copy(runtime, empty, NULL, 0, &length) == 0 && length == 0);
                        assert({{api}}_release_handle(runtime, empty) == 0);
                    }
                    assert(output[65538] == {{(int)appended[65538]}});
                    length = 123; output[0] = 777;
                    assert({{api}}_string_copy(runtime, text, output, 65540, &length) == CILICA_V3_INVALID_HANDLE && length == 123 && output[0] == 777);
                    assert({{api}}_string_copy(runtime, 0, NULL, 0, &length) == CILICA_V3_INVALID_HANDLE && length == 123);
                    text = make(runtime, input, 1);
                    assert({{api}}_string_copy(other, text, output, 65540, &length) == CILICA_V3_INVALID_HANDLE && length == 123);
                    assert({{api}}_invoke(runtime, 5, NULL, 0, &result) == 0);
                    uint64_t unrelated = result.value.payload;
                    assert({{api}}_string_copy(runtime, unrelated, output, 65540, &length) == CILICA_V3_TYPE_MISMATCH && length == 123);
                    assert({{api}}_release_handle(runtime, unrelated) == 0);
                    assert({{api}}_string_copy(runtime, text, NULL, 1, &length) == CILICA_V3_INVALID_ARGUMENT);
                    assert({{api}}_string_copy(runtime, text, output, 1, NULL) == CILICA_V3_INVALID_ARGUMENT);
                    assert({{api}}_string_create(runtime, NULL, 1, &sentinel) == CILICA_V3_INVALID_ARGUMENT && sentinel == 123);
                    assert({{api}}_string_create(runtime, input, 1, NULL) == CILICA_V3_INVALID_ARGUMENT);
                    assert({{api}}_string_create(runtime, input, UINT32_MAX, &sentinel) == CILICA_V3_LIMIT && sentinel == 123);
                    assert({{api}}_string_create(0, input, 1, &sentinel) == CILICA_V3_INVALID_RUNTIME && sentinel == 123);
                    assert({{api}}_destroy(runtime) == 0);
                    assert({{api}}_string_copy(runtime, text, output, 1, &length) == CILICA_V3_INVALID_RUNTIME);
                    assert({{api}}_destroy(other) == 0);
                }
                uint64_t runtime = 0, sentinel = 123;
                config.handle_limit = 1;
                assert({{api}}_create(3, hash, &config, &runtime) == 0);
                uint64_t held = make(runtime, input, 1);
                assert({{api}}_string_create(runtime, input, 1, &sentinel) == CILICA_V3_LIMIT && sentinel == 123);
                assert({{api}}_release_handle(runtime, held) == 0);
                /* Oversized strings exhaust heap budget before reading the supplied buffer.
                   The reserved lease must be cancelled, permitting the next small import. */
                assert({{api}}_string_create(runtime, input, INT32_MAX, &sentinel) == CILICA_V3_ALLOCATION_FAILED && sentinel == 123);
                held = make(runtime, input, 1);
                assert({{api}}_destroy(runtime) == 0);
                config.heap_bytes = 0;
                assert({{api}}_create(3, hash, &config, &runtime) == 0);
                assert({{api}}_string_create(runtime, NULL, 0, &sentinel) == CILICA_V3_ALLOCATION_FAILED && sentinel == 123);
                assert({{api}}_destroy(runtime) == 0);
                config.heap_bytes = 1048576; config.handle_limit = 0;
                assert({{api}}_create(3, hash, &config, &runtime) == 0);
                assert({{api}}_string_create(runtime, NULL, 0, &sentinel) == CILICA_V3_LIMIT && sentinel == 123);
                assert({{api}}_destroy(runtime) == 0);
            }
            """);
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        string[] flags = ["-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-fsanitize=address,undefined", "-fno-sanitize-recover=all"];
        string library = Path.Combine(directory, OperatingSystem.IsMacOS() ? "libengine.dylib" : "libengine.so");
        await Check(compiler, ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wframe-larger-than=2048", "-fvisibility=hidden", "-fPIC",
            OperatingSystem.IsMacOS() ? "-dynamiclib" : "-shared", .. flags, "engine.cpp", "-o", library], directory);
        await Check(compiler, ["-x", "c", "-std=c11", .. flags, "-c", "consumer.c", "-o", "consumer.o"], directory);
        string executable = Path.Combine(directory, "consumer");
        await Check(compiler, [.. flags, "consumer.o", library, "-o", executable], directory);
        await Check(executable, [], directory);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task String_copy_and_import_share_admission_with_release(bool optimized)
    {
        var program = await Program.Value;
        var artifact = EmbeddingObjectModuleEmitter.EmitExceptions(program, Contract(program));
        string api = artifact.SymbolPrefix;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("embedding-string-races");
        File.WriteAllText(Path.Combine(directory, "races.cpp"), artifact.Text + $$"""
            #include <cassert>
            #include <thread>
            #include <barrier>
            int main() {
                cilica_v3_config config = CILICA_V3_CONFIG_INIT; config.flags = 1; config.object_limit = 8;
                uint64_t runtime = 0; const uint16_t input[] = {65, 0, 0xd800, 0xdfff};
                assert({{api}}_create(3, CILICA_STRINGS_ABI_V3_CONTRACT_HASH, &config, &runtime) == 0);
                // Deterministic busy rejection, in addition to scheduler-dependent races.
                {
                    using namespace {{api}}_private;
                    Slot* slot; assert(enter(runtime, slot) == 0); Invocation invocation{*slot};
                    uint64_t token = 123; uint32_t length = 123; uint16_t output = 777;
                    assert({{api}}_string_create(runtime, input, 4, &token) == CILICA_V3_BUSY && token == 123);
                    assert({{api}}_string_copy(runtime, 0, &output, 1, &length) == CILICA_V3_BUSY && output == 777 && length == 123);
                }
                for (int i = 0; i < 100; ++i) {
                    uint64_t original = 0, imported = 0;
                    assert({{api}}_string_create(runtime, input, 4, &original) == 0);
                    std::barrier start(3);
                    std::thread reader([&] {
                        uint16_t output[4] = {777, 777, 777, 777}; uint32_t length = 123;
                        start.arrive_and_wait();
                        auto status = {{api}}_string_copy(runtime, original, output, 4, &length);
                        if (status == CILICA_V3_OK) {
                            assert(length == 4);
                            for (int n = 0; n < 4; ++n) assert(output[n] == input[n]);
                        } else {
                            assert(status == CILICA_V3_BUSY || status == CILICA_V3_INVALID_HANDLE);
                            assert(length == 123);
                            for (auto unit : output) assert(unit == 777);
                        }
                    });
                    std::thread releaser([&] {
                        start.arrive_and_wait(); auto status = {{api}}_release_handle(runtime, original);
                        assert(status == CILICA_V3_OK || status == CILICA_V3_BUSY);
                    });
                    std::thread creator([&] {
                        start.arrive_and_wait(); auto status = {{api}}_string_create(runtime, input, 4, &imported);
                        assert((status == CILICA_V3_OK && imported != 0) || (status == CILICA_V3_BUSY && imported == 0));
                    });
                    reader.join(); releaser.join(); creator.join();
                    auto status = {{api}}_release_handle(runtime, original);
                    assert(status == CILICA_V3_OK || status == CILICA_V3_INVALID_HANDLE);
                    if (imported != 0) assert({{api}}_release_handle(runtime, imported) == 0);
                }
                assert({{api}}_destroy(runtime) == 0);
            }
            """);
        string executable = Path.Combine(directory, "races");
        await Check(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", "-pthread", "-Wframe-larger-than=2048",
                optimized ? "-O2" : "-O0", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", "races.cpp", "-o", executable], directory);
        await Check(executable, [], directory);
    }

    private static async Task Check(string executable, string[] arguments, string directory)
    {
        var result = await NativeProcess.Run(executable, arguments, directory);
        Assert.True(result.ExitCode == 0, result.Stdout + result.Stderr); Assert.Empty(result.Stderr);
    }
}
