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

public sealed class EmbeddingExceptionTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram>> Program = new(async () =>
        (await CoreLibraryBuildTests.TranslateFixtures(typeof(EmbeddingExceptionProbe), nameof(EmbeddingExceptionProbe.Root)))[0]);
    private static EmbeddingObjectContract Contract(ManagedExceptionIrProgram program)
    {
        var methods = program.Flow.Flow.Flow.Methods;
        IrMethod Method(string name) => methods.Single(m => m.Source.Token == typeof(EmbeddingExceptionProbe).GetMethod(name)!.MetadataToken);
        var scalar = new EmbeddingValueShape(EmbeddingValueKind.Int32);
        return new("faults", 1, [new(1, "other", Method("Make").ReturnType.Class!)], [
            new(1, "run", IrMethodId.From(Method("Run").Source), [scalar, scalar], scalar),
            new(2, "make", IrMethodId.From(Method("Make").Source), [scalar], new(EmbeddingValueKind.Handle, 1)),
            new(3, "calls", IrMethodId.From(Method("Calls").Source), [], scalar),
            new(4, "churn", IrMethodId.From(Method("Churn").Source), [scalar], scalar)], CaptureExceptions: true);
    }

    [Fact]
    public async Task Capture_is_explicit_negotiated_and_requires_exception_emission()
    {
        var program = await Program.Value; var contract = Contract(program);
        var enabled = EmbeddingObjectModuleEmitter.EmitExceptions(program, contract);
        var disabled = EmbeddingObjectModuleEmitter.EmitExceptions(program, contract with { CaptureExceptions = false });
        Assert.NotEqual(enabled.ContractHash, disabled.ContractHash);
        Assert.Contains("captureExceptions", enabled.ManifestJson, StringComparison.Ordinal);
        Assert.DoesNotContain("captureExceptions", disabled.ManifestJson, StringComparison.Ordinal);
        Assert.DoesNotContain("_invoke_capture", disabled.Header, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => EmbeddingObjectModuleEmitter.EmitProgram(
            NativeObjectExecutionTests.Translate(nameof(ObjectProbe.AcrossCalls)), contract));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Captured_exceptions_preserve_type_utf16_origin_and_lifetime_without_reexecution(bool optimized)
    {
        var program = await Program.Value;
        var artifact = EmbeddingObjectModuleEmitter.EmitExceptions(program, Contract(program));
        string api = artifact.SymbolPrefix;
        var samples = new StringBuilder();
        for (int mode = 0; mode < 5; ++mode) {
            Exception exception;
            try { EmbeddingExceptionProbe.Run(mode, 0); throw new InvalidOperationException("Oracle did not throw."); }
            catch (Exception caught) { exception = caught; }
            string name = exception.GetType().FullName!;
            var declaration = program.Flow.Flow.Flow.Types.References.Types.Single(t => t.Source.Name == name);
            string Units(string value) => string.Join(", ", value.Select(c => ((ushort)c).ToString(CultureInfo.InvariantCulture)));
            samples.Append(CultureInfo.InvariantCulture, $$"""
                {
                    static const uint16_t expected_name[] = { {{Units(name)}} };
                    static const uint16_t expected_message[] = { {{Units(exception.Message)}} };
                    check(runtime, foreign, {{mode}}, expected_name, {{name.Length}}, expected_message, {{exception.Message.Length}}, {{declaration.Source.Token}}u, "{{declaration.Source.ContentHash.ToLowerInvariant()}}");
                }
                """);
        }
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("embedding-exceptions");
        File.WriteAllText(Path.Combine(directory, "engine.h"), artifact.Header);
        File.WriteAllText(Path.Combine(directory, "engine.cpp"), artifact.Text);
        File.WriteAllText(Path.Combine(directory, "consumer.c"), $$"""
            #include "engine.h"
            #include <assert.h>
            #include <string.h>
            static uint16_t name_buffer[256], message_buffer[256];
            static uint32_t calls(uint64_t runtime) {
                cilica_v3_result result = {0};
                assert({{api}}_invoke(runtime, 3, NULL, 0, &result) == 0); return (uint32_t)result.value.payload;
            }
            static void check(uint64_t runtime, uint64_t foreign, uint32_t mode, const uint16_t* expected_name, uint32_t name_length,
                const uint16_t* expected_message, uint32_t message_length, uint32_t type_token, const char* type_image) {
                cilica_v3_value args[2] = { {1, 0, mode}, {1, 0, 0} }; cilica_v3_result result = {0};
                uint64_t exception = 123; uint32_t before = calls(runtime);
                assert({{api}}_invoke_capture(runtime, 1, args, 2, &result, &exception) == CILICA_V3_EXECUTION_FAILED && exception != 0);
                assert(result.value.kind == 0 && result.value.payload == 0 && result.fault_code != 0 && strlen(result.image_hash) == 64 && result.method_token != 0);
                cilica_v3_result saved = result;
                cilica_v3_exception_info info = {0};
                assert({{api}}_exception_copy(runtime, exception, NULL, 0, NULL, 0, &info) == CILICA_V3_BUFFER_TOO_SMALL);
                assert(info.type_name_length == name_length && info.message_length == message_length);
                assert(info.type_token == type_token && strcmp(info.type_image_hash, type_image) == 0);
                name_buffer[0] = 777; message_buffer[0] = 777;
                assert({{api}}_exception_copy(runtime, exception, name_buffer, name_length - 1, message_buffer, 256, &info) == CILICA_V3_BUFFER_TOO_SMALL);
                assert(name_buffer[0] == 777 && message_buffer[0] == 777);
                assert({{api}}_exception_copy(runtime, exception, name_buffer, 256, message_buffer, message_length - 1, &info) == CILICA_V3_BUFFER_TOO_SMALL);
                assert(name_buffer[0] == 777 && message_buffer[0] == 777);
                uint64_t clone = 0;
                assert({{api}}_retain_handle(runtime, exception, &clone) == 0 && clone != exception);
                assert({{api}}_release_handle(runtime, exception) == 0);
                cilica_v3_value churn = {1, 0, 40};
                assert({{api}}_invoke(runtime, 4, &churn, 1, &result) == 0);
                name_buffer[name_length] = 777; message_buffer[message_length] = 777;
                assert({{api}}_exception_copy(runtime, clone, name_buffer, 256, message_buffer, 256, &info) == 0);
                assert(memcmp(name_buffer, expected_name, name_length * sizeof(uint16_t)) == 0);
                assert(memcmp(message_buffer, expected_message, message_length * sizeof(uint16_t)) == 0);
                assert(name_buffer[name_length] == 777 && message_buffer[message_length] == 777);
                assert(calls(runtime) == before + 1); /* Query/copy never invokes the export again. */
                cilica_v3_exception_info untouched; memset(&untouched, 0x5a, sizeof(untouched));
                memcpy(&info, &untouched, sizeof(info)); name_buffer[0] = 777;
                assert({{api}}_exception_copy(foreign, clone, name_buffer, 256, message_buffer, 256, &info) == CILICA_V3_INVALID_HANDLE);
                assert(memcmp(&info, &untouched, sizeof(info)) == 0 && name_buffer[0] == 777);
                assert({{api}}_exception_copy(runtime, exception, NULL, 0, NULL, 0, &info) == CILICA_V3_INVALID_HANDLE);
                assert({{api}}_exception_copy(runtime, clone, NULL, 1, NULL, 0, &info) == CILICA_V3_INVALID_ARGUMENT);
                assert({{api}}_exception_copy(runtime, clone, NULL, 0, NULL, 0, NULL) == CILICA_V3_INVALID_ARGUMENT);
                assert({{api}}_release_handle(runtime, clone) == 0);
                assert(message_buffer[0] == expected_message[0] && saved.fault_code != 0 && strlen(saved.image_hash) == 64);
            }
            int main(void) {
                assert({{api}}_capabilities() == (255 | CILICA_V3_CAP_EXCEPTION_CAPTURE));
                cilica_v3_config config = CILICA_V3_CONFIG_INIT; config.object_limit = 24; config.handle_limit = 8;
                uint64_t runtime = 0, foreign = 0;
                for (uint32_t stress = 0; stress < 2; ++stress) {
                    config.flags = stress;
                    assert({{api}}_create(3, CILICA_FAULTS_ABI_V3_CONTRACT_HASH, &config, &runtime) == 0);
                    assert({{api}}_create(3, CILICA_FAULTS_ABI_V3_CONTRACT_HASH, &config, &foreign) == 0);
                    {{samples}}
                    cilica_v3_value args[2] = { {1, 0, 5}, {1, 0, 12} }; cilica_v3_result result = {0}; uint64_t exception = 123;
                    assert({{api}}_invoke_capture(runtime, 1, args, 2, &result, &exception) == 0 && exception == 0 && result.value.payload == 17);
                    args[0].payload = 6;
                    assert({{api}}_invoke_capture(runtime, 2, args, 1, &result, &exception) == 0 && exception == 0);
                    uint64_t other = result.value.payload; cilica_v3_exception_info info = {0};
                    assert({{api}}_exception_copy(runtime, other, NULL, 0, NULL, 0, &info) == CILICA_V3_TYPE_MISMATCH);
                    assert({{api}}_exception_copy(runtime, 0, NULL, 0, NULL, 0, &info) == CILICA_V3_INVALID_HANDLE);
                    assert({{api}}_release_handle(runtime, other) == 0);
                    assert({{api}}_destroy(runtime) == 0); assert({{api}}_destroy(foreign) == 0);
                }
                config.handle_limit = 1;
                assert({{api}}_create(3, CILICA_FAULTS_ABI_V3_CONTRACT_HASH, &config, &runtime) == 0);
                cilica_v3_value args[2] = { {1, 0, 6}, {1, 0, 12} }; cilica_v3_result result = {0}; uint64_t exception = 123;
                /* A single reserved slot serves either the reference return or failure. */
                assert({{api}}_invoke_capture(runtime, 2, args, 1, &result, &exception) == 0 && exception == 0);
                uint64_t other = result.value.payload; uint32_t before = calls(runtime); exception = 123; result.value.payload = 456;
                assert({{api}}_invoke_capture(runtime, 1, args, 2, &result, &exception) == CILICA_V3_LIMIT && exception == 123 && result.value.payload == 456);
                assert(calls(runtime) == before);
                args[0].kind = 3;
                assert({{api}}_invoke_capture(runtime, 1, args, 2, &result, &exception) == CILICA_V3_INVALID_ARGUMENT && exception == 123);
                args[0].kind = 1;
                assert({{api}}_release_handle(runtime, other) == 0);
                args[0].payload = 0;
                assert({{api}}_invoke_capture(runtime, 2, args, 1, &result, &exception) == CILICA_V3_EXECUTION_FAILED && exception != 0);
                assert({{api}}_release_handle(runtime, exception) == 0);
                args[0].payload = 6;
                for (int i = 0; i < 20; ++i) assert({{api}}_invoke_capture(runtime, 1, args, 2, &result, &exception) == 0 && exception == 0);
                assert({{api}}_invoke_capture(runtime, 1, args, 2, &result, NULL) == CILICA_V3_INVALID_ARGUMENT);
                assert({{api}}_destroy(runtime) == 0);
                config.execution_bytes = 0;
                assert({{api}}_create(3, CILICA_FAULTS_ABI_V3_CONTRACT_HASH, &config, &runtime) == 0);
                assert({{api}}_invoke_capture(runtime, 1, args, 2, &result, &exception) == CILICA_V3_EXECUTION_FAILED && result.fault_code == 9 && exception == 0);
                assert({{api}}_destroy(runtime) == 0);
                config.execution_bytes = 1048576; config.handle_limit = 0;
                assert({{api}}_create(3, CILICA_FAULTS_ABI_V3_CONTRACT_HASH, &config, &runtime) == 0);
                assert({{api}}_invoke_capture(runtime, 1, args, 2, &result, &exception) == CILICA_V3_LIMIT);
                assert({{api}}_invoke(runtime, 1, args, 2, &result) == 0 && result.value.payload == 12);
                assert({{api}}_destroy(runtime) == 0);
                /* Emergency object + divide fault fit, lazily formatted message does not. */
                config.handle_limit = 1; config.object_limit = 2;
                assert({{api}}_create(3, CILICA_FAULTS_ABI_V3_CONTRACT_HASH, &config, &runtime) == 0);
                args[0].payload = 1; args[1].payload = 0;
                assert({{api}}_invoke_capture(runtime, 1, args, 2, &result, &exception) == CILICA_V3_EXECUTION_FAILED && result.fault_code == 1 && exception != 0);
                cilica_v3_result saved = result; cilica_v3_exception_info info, untouched;
                memset(&untouched, 0x5a, sizeof(untouched)); memcpy(&info, &untouched, sizeof(info));
                assert({{api}}_exception_copy(runtime, exception, NULL, 0, NULL, 0, &info) == CILICA_V3_ALLOCATION_FAILED);
                assert(memcmp(&info, &untouched, sizeof(info)) == 0 && saved.fault_code == 1);
                args[0].payload = 6;
                assert({{api}}_invoke(runtime, 1, args, 2, &result) == 0);
                assert({{api}}_release_handle(runtime, exception) == 0);
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
    public async Task Exception_copy_capture_and_release_share_instance_admission(bool optimized)
    {
        var program = await Program.Value;
        var artifact = EmbeddingObjectModuleEmitter.EmitExceptions(program, Contract(program));
        string api = artifact.SymbolPrefix;
        string expected = new ArgumentException("bad\0input", "p\ud800").Message;
        string units = string.Join(", ", expected.Select(c => ((ushort)c).ToString(CultureInfo.InvariantCulture)));
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("embedding-exception-races");
        File.WriteAllText(Path.Combine(directory, "races.cpp"), artifact.Text + $$"""
            #include <cassert>
            #include <thread>
            #include <barrier>
            static constexpr uint16_t expected[] = { {{units}} };
            int main() {
                cilica_v3_config config = CILICA_V3_CONFIG_INIT; config.flags = 1; config.object_limit = 16;
                uint64_t runtime = 0;
                assert({{api}}_create(3, CILICA_FAULTS_ABI_V3_CONTRACT_HASH, &config, &runtime) == 0);
                cilica_v3_value args[2]{ {1, 0, 2}, {1, 0, 0} };
                {
                    using namespace {{api}}_private;
                    Slot* slot; assert(enter(runtime, slot) == 0); Invocation invocation{*slot};
                    cilica_v3_result result{}; result.value.payload = 123; uint64_t exception = 123;
                    cilica_v3_exception_info info{}; info.message_length = 123;
                    assert({{api}}_invoke_capture(runtime, 1, args, 2, &result, &exception) == CILICA_V3_BUSY && exception == 123 && result.value.payload == 123);
                    assert({{api}}_exception_copy(runtime, 0, nullptr, 0, nullptr, 0, &info) == CILICA_V3_BUSY && info.message_length == 123);
                }
                for (int i = 0; i < 100; ++i) {
                    cilica_v3_result result{}; uint64_t exception = 0;
                    assert({{api}}_invoke_capture(runtime, 1, args, 2, &result, &exception) == CILICA_V3_EXECUTION_FAILED && exception != 0);
                    std::barrier start(3);
                    std::thread reader([&] {
                        uint16_t name[64], message[64];
                        for (auto& unit : name) unit = 777;
                        for (auto& unit : message) unit = 777;
                        cilica_v3_exception_info info{}; info.message_length = 123;
                        start.arrive_and_wait();
                        auto status = {{api}}_exception_copy(runtime, exception, name, 64, message, 64, &info);
                        if (status == 0) {
                            assert(info.message_length == {{expected.Length}});
                            for (uint32_t n = 0; n < info.message_length; ++n) assert(message[n] == expected[n]);
                        } else {
                            assert(status == CILICA_V3_BUSY || status == CILICA_V3_INVALID_HANDLE);
                            assert(info.message_length == 123);
                            for (auto unit : name) assert(unit == 777);
                            for (auto unit : message) assert(unit == 777);
                        }
                    });
                    std::thread releaser([&] {
                        start.arrive_and_wait(); auto status = {{api}}_release_handle(runtime, exception);
                        assert(status == 0 || status == CILICA_V3_BUSY);
                    });
                    std::thread collector([&] {
                        cilica_v3_value churn{1, 0, 40}; cilica_v3_result completed{};
                        start.arrive_and_wait(); auto status = {{api}}_invoke(runtime, 4, &churn, 1, &completed);
                        assert(status == 0 || status == CILICA_V3_BUSY);
                    });
                    reader.join(); releaser.join(); collector.join();
                    auto status = {{api}}_release_handle(runtime, exception);
                    assert(status == 0 || status == CILICA_V3_INVALID_HANDLE);
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
