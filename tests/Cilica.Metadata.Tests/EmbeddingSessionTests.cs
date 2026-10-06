using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class EmbeddingSessionTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram>> Program = new(async () =>
        (await CoreLibraryBuildTests.TranslateFixtures(typeof(EmbeddingSessionProbe), nameof(EmbeddingSessionProbe.Root)))[0]);

    internal static async Task<EmbeddingModuleArtifact> Emit()
    {
        var program = await Program.Value; var flow = program.Flow.Flow.Flow;
        IrMethod Method(string name) => flow.Methods.Single(m => m.Source.ContentHash == flow.Entry.ContentHash && m.Source.Token == typeof(EmbeddingSessionProbe).GetMethod(name)!.MetadataToken);
        var session = new EmbeddingValueShape(EmbeddingValueKind.Handle, 1);
        var text = new EmbeddingValueShape(EmbeddingValueKind.Handle, 2);
        var view = new EmbeddingValueShape(EmbeddingValueKind.Record, RecordTypeId: 1);
        EmbeddingObjectExport Export(uint id, string name, string method, EmbeddingValueShape result, params EmbeddingValueShape[] inputs) =>
            new(id, name, IrMethodId.From(Method(method).Source), [.. inputs], result);
        var type = Method("Read").ReturnType.Class!;
        var slots = ValueLayoutPlanner.Build(flow.Types).Single(p => p.Type == type).Slots;
        var contract = new EmbeddingObjectContract("sessions", 1,
            [new(1, "session", Method("Open").ReturnType.Class!), new(2, "text", flow.Types.References.String!)],
            [Export(1, "open", "Open", session, text), Export(2, "apply", "Apply", view, session, new(EmbeddingValueKind.Int32)),
             Export(3, "read", "Read", view, session), Export(4, "snapshot", "Snapshot", text, session),
             Export(5, "echo", "Echo", view, view)],
            Utf16StringTypeId: 2, CaptureExceptions: true) {
            Records = [new(1, "view", type, [new(1, "steps", slots[0].Fields, EmbeddingValueKind.Int32),
                new(2, "total", slots[1].Fields, EmbeddingValueKind.Integer64), new(3, "text_length", slots[2].Fields, EmbeddingValueKind.Int32)])]
        };
        return EmbeddingObjectModuleEmitter.EmitExceptions(program, contract);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Persistent_sessions_compose_handles_records_strings_and_fault_recovery(bool optimized)
    {
        var artifact = await Emit(); string api = artifact.SymbolPrefix;
        const string label = "A\0\ud800";
        var oracle = EmbeddingSessionProbe.Open(label);
        var observations = Enumerable.Range(1, 12).Select(i => EmbeddingSessionProbe.Apply(oracle, i)).ToArray();
        string expectedText = EmbeddingSessionProbe.Snapshot(oracle);
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("embedding-sessions");
        File.WriteAllText(Path.Combine(directory, "engine.h"), artifact.Header);
        File.WriteAllText(Path.Combine(directory, "engine.cpp"), artifact.Text);
        File.WriteAllText(Path.Combine(directory, "consumer.c"), $$"""
            #include "engine.h"
            #include <assert.h>
            #include <string.h>
            static const uint16_t label[] = {65, 0, 0xd800};
            static const uint16_t expected_text[] = { {{string.Join(",", expectedText.Select(c => ((uint)c).ToString(CultureInfo.InvariantCulture)))}} };
            static const uint64_t expected[][3] = {
                {{string.Join(",\n", observations.Select(o => "{" + o.Steps.ToString(CultureInfo.InvariantCulture) + ", UINT64_C(" + o.Total.ToString(CultureInfo.InvariantCulture) + "), " + o.TextLength.ToString(CultureInfo.InvariantCulture) + "}"))}}
            };
            static uint64_t open_session(uint64_t runtime) {
                uint64_t label_handle = 0;
                assert({{api}}_string_create(runtime, label, 3, &label_handle) == 0);
                cilica_v3_value arg = {2, 0, label_handle}; cilica_v3_result result = {0};
                assert({{api}}_invoke(runtime, 1, &arg, 1, &result) == 0);
                assert({{api}}_release_handle(runtime, label_handle) == 0);
                return result.value.payload;
            }
            int main(void) {
                assert({{api}}_capabilities() == 4095);
                uint64_t old_runtime = 0, old_session = 0;
                for (uint32_t stress = 0; stress < 2; ++stress) for (uint32_t repeat = 0; repeat < 3; ++repeat) {
                    cilica_v3_config config = CILICA_V3_CONFIG_INIT; config.flags = stress;
                    config.object_limit = 256; config.handle_limit = 4;
                    uint64_t runtime = 0, independent = 0;
                    assert({{api}}_create(3, CILICA_SESSIONS_ABI_V3_CONTRACT_HASH, &config, &runtime) == 0);
                    assert({{api}}_create(3, CILICA_SESSIONS_ABI_V3_CONTRACT_HASH, &config, &independent) == 0);
                    uint64_t handle = open_session(runtime), other = open_session(independent), alias = 0;
                    assert({{api}}_retain_handle(runtime, handle, &alias) == 0);
                    assert({{api}}_release_handle(runtime, handle) == 0);
                    cilica_v3_value args[2] = { {2, 0, handle}, {1, 0, 1} }, fields[3]; cilica_v3_result result = {0};
                    assert({{api}}_invoke_records(runtime, 3, args, 1, fields, 3, &result, NULL) == CILICA_V3_INVALID_HANDLE);
                    args[0].payload = other;
                    assert({{api}}_invoke_records(runtime, 3, args, 1, fields, 3, &result, NULL) == CILICA_V3_INVALID_HANDLE);
                    if (old_runtime != 0) {
                        args[0].payload = old_session;
                        assert({{api}}_invoke_records(runtime, 3, args, 1, fields, 3, &result, NULL) == CILICA_V3_INVALID_HANDLE);
                        assert({{api}}_invoke_records(old_runtime, 3, args, 1, fields, 3, &result, NULL) == CILICA_V3_INVALID_RUNTIME);
                    }
                    args[0].payload = alias; uint64_t first_snapshot = 0, failure = 0;
                    for (uint32_t step = 0; step < 12; ++step) {
                        args[1].payload = step + 1;
                        assert({{api}}_invoke_records(runtime, 2, args, 2, fields, 2, &result, NULL) == CILICA_V3_BUFFER_TOO_SMALL);
                        uint64_t exception = 123;
                        assert({{api}}_invoke_records(runtime, 2, args, 2, fields, 3, &result, &exception) == 0 && exception == 0);
                        assert(result.value.kind == 4 && result.value.payload == 1);
                        for (uint32_t j = 0; j < 3; ++j) assert(fields[j].payload == expected[step][j] && fields[j].kind == (j == 1 ? 3u : 1u));
                        if (step == 0) {
                            assert({{api}}_invoke(runtime, 4, args, 1, &result) == 0); first_snapshot = result.value.payload;
                            args[1].payload = UINT32_MAX;
                            cilica_v3_value saved[3]; memcpy(saved, fields, sizeof(fields));
                            assert({{api}}_invoke_records(runtime, 2, args, 2, fields, 3, &result, &failure) == CILICA_V3_EXECUTION_FAILED && failure != 0);
                            assert(result.fault_code == 13 && memcmp(saved, fields, sizeof(fields)) == 0);
                        }
                    }
                    /* Retained snapshot and failure survive later allocations and calls. */
                    uint16_t units[64]; uint32_t length = 0;
                    assert({{api}}_string_copy(runtime, first_snapshot, units, 64, &length) == 0 && length == 5);
                    assert(memcmp(units, label, sizeof(label)) == 0 && units[3] == ':' && units[4] == '1');
                    assert({{api}}_release_handle(runtime, first_snapshot) == 0);
                    cilica_v3_exception_info info = {0}; uint16_t type_name[64];
                    assert({{api}}_exception_copy(runtime, failure, type_name, 64, units, 64, &info) == 0 && info.message_length == 14);
                    assert({{api}}_release_handle(runtime, failure) == 0);
                    assert({{api}}_invoke(runtime, 4, args, 1, &result) == 0); uint64_t snapshot = result.value.payload;
                    assert({{api}}_release_handle(runtime, alias) == 0);
                    assert({{api}}_string_copy(runtime, snapshot, units, 64, &length) == 0);
                    assert(length == sizeof(expected_text)/sizeof(expected_text[0]) && memcmp(units, expected_text, sizeof(expected_text)) == 0);
                    args[0].payload = other;
                    assert({{api}}_invoke_records(independent, 3, args, 1, fields, 3, &result, NULL) == 0);
                    assert(fields[0].payload == 0 && fields[1].payload == UINT64_C(9007199254740993) && fields[2].payload == 3);
                    old_runtime = runtime; old_session = snapshot;
                    assert({{api}}_destroy(runtime) == 0 && {{api}}_destroy(independent) == 0);
                    assert(memcmp(units, expected_text, sizeof(expected_text)) == 0); /* Host copy survives destruction. */
                }
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

    private static async Task Check(string executable, string[] arguments, string directory)
    {
        var result = await NativeProcess.Run(executable, arguments, directory);
        Assert.True(result.ExitCode == 0, result.Stdout + result.Stderr); Assert.Empty(result.Stderr);
    }
}
