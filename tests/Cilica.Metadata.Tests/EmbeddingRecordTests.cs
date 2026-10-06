using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class EmbeddingRecordTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram>> Program = new(async () =>
        (await CoreLibraryBuildTests.TranslateFixtures(typeof(EmbeddingRecordProbe), nameof(EmbeddingRecordProbe.Root)))[0]);
    private static readonly EmbeddingValueShape I32 = new(EmbeddingValueKind.Int32), I64 = new(EmbeddingValueKind.Integer64),
        Packet = new(EmbeddingValueKind.Record, RecordTypeId: 1), Other = new(EmbeddingValueKind.Handle, 1);
    private static IrMethod Method(ManagedExceptionIrProgram program, string name) => program.Flow.Flow.Flow.Methods.Single(
        m => m.Source.Token == typeof(EmbeddingRecordProbe).GetMethod(name)!.MetadataToken);
    private static EmbeddingObjectContract Contract(ManagedExceptionIrProgram program)
    {
        var type = Method(program, "Make").ReturnType.Class!;
        var slots = ValueLayoutPlanner.Build(program.Flow.Flow.Flow.Types).Single(p => p.Type == type).Slots;
        var record = new EmbeddingRecordBinding(1, "packet", type, [
            new(30, "x", slots[0].Fields, EmbeddingValueKind.Int32),
            new(20, "y", slots[1].Fields, EmbeddingValueKind.Integer64),
            new(10, "tag", slots[2].Fields, EmbeddingValueKind.Int32)]);
        EmbeddingObjectExport Export(uint id, string name, string method, EmbeddingValueShape result, params EmbeddingValueShape[] parameters) =>
            new(id, name, IrMethodId.From(Method(program, method).Source), [.. parameters], result);
        return new("records", 1, [new(1, "other", Method(program, "Other").ReturnType.Class!)], [
            Export(1, "make", "Make", Packet, I32), Export(2, "transform", "Transform", Packet, Packet, I32),
            Export(3, "score", "Score", I64, Packet), Export(4, "mixed", "Mixed", Packet, Other, Packet, I64),
            Export(5, "other", "Other", Other, I32), Export(6, "calls", "Calls", I32)], CaptureExceptions: true) { Records = [record] };
    }

    [Theory]
    [InlineData("width")] [InlineData("path")] [InlineData("missing")] [InlineData("duplicate_id")]
    [InlineData("duplicate_path")] [InlineData("unknown_record")] [InlineData("mixed_ids")]
    [InlineData("unused_record")] [InlineData("too_many")] [InlineData("borrow")]
    [InlineData("reference_leaf")]
    public async Task Record_contract_rejects_unsupported_or_ambiguous_bindings(string mutation)
    {
        var program = await Program.Value; var contract = Contract(program); var record = contract.Records[0];
        contract = mutation switch {
            "width" => contract with { Records = [record with { Fields = record.Fields.SetItem(0, record.Fields[0] with { Kind = EmbeddingValueKind.Integer64 }) }] },
            "path" => contract with { Records = [record with { Fields = record.Fields.SetItem(0, record.Fields[0] with { Path = [record.Fields[0].Path[0]] }) }] },
            "missing" => contract with { Records = [record with { Fields = record.Fields.RemoveAt(0) }] },
            "duplicate_id" => contract with { Records = [record with { Fields = record.Fields.SetItem(0, record.Fields[0] with { Id = 20 }) }] },
            "duplicate_path" => contract with { Records = [record with { Fields = record.Fields.SetItem(0, record.Fields[0] with { Path = record.Fields[2].Path }) }] },
            "unknown_record" => contract with { Exports = contract.Exports.SetItem(0, contract.Exports[0] with { Result = new(EmbeddingValueKind.Record, RecordTypeId: 99) }) },
            "mixed_ids" => contract with { Exports = contract.Exports.SetItem(0, contract.Exports[0] with { Result = new(EmbeddingValueKind.Record, 1, 1) }) },
            "unused_record" => contract with { Exports = [contract.Exports[4], contract.Exports[5]] },
            "too_many" => contract with { Records = [.. Enumerable.Repeat(record, 33)] },
            "borrow" => contract with { Exports = contract.Exports.Add(new(7, "change", IrMethodId.From(Method(program, "Change").Source), [Packet], new(EmbeddingValueKind.Void))) },
            _ => contract with { Records = [record, ReferenceRecord(program)] }
        };
        Assert.Throws<ArgumentException>(() => EmbeddingObjectModuleEmitter.EmitExceptions(program, contract));
    }

    private static EmbeddingRecordBinding ReferenceRecord(ManagedExceptionIrProgram program)
    {
        var type = Method(program, "Reference").ReturnType.Class!;
        var path = ValueLayoutPlanner.Build(program.Flow.Flow.Flow.Types).Single(p => p.Type == type).Slots[0].Fields;
        return new(2, "reference", type, [new(1, "link", path, EmbeddingValueKind.Int32)]);
    }

    [Fact]
    public async Task Record_manifest_uses_stable_field_order_and_pins_names_and_widths()
    {
        var program = await Program.Value; var contract = Contract(program);
        var artifact = EmbeddingObjectModuleEmitter.EmitExceptions(program, contract);
        Assert.Equal(artifact, EmbeddingObjectModuleEmitter.EmitExceptions(program, contract with {
            Exports = [.. contract.Exports.Reverse()], Records = [contract.Records[0] with { Fields = [.. contract.Records[0].Fields.Reverse()] }]
        }));
        var changed = contract with { Records = [contract.Records[0] with { Name = "renamed" }] };
        Assert.NotEqual(artifact.ContractHash, EmbeddingObjectModuleEmitter.EmitExceptions(program, changed).ContractHash);
        Assert.Contains("recordBindings", artifact.ManifestJson, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => EmbeddingObjectModuleEmitter.EmitProgram(NativeObjectExecutionTests.Translate(nameof(ObjectProbe.AcrossCalls)), contract with { CaptureExceptions = false }));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task C_host_copies_nested_numeric_records_and_preserves_failures_and_ownership(bool optimized)
    {
        var program = await Program.Value; var contract = Contract(program);
        var artifact = EmbeddingObjectModuleEmitter.EmitExceptions(program, contract);
        string api = artifact.SymbolPrefix;
        var original = EmbeddingRecordProbe.Make(10);
        var expected = EmbeddingRecordProbe.Transform(original, 3);
        var mixed = EmbeddingRecordProbe.Mixed(new EmbeddingRecordOther { Value = 2 }, expected, long.MinValue);
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("embedding-records");
        File.WriteAllText(Path.Combine(directory, "engine.h"), artifact.Header);
        File.WriteAllText(Path.Combine(directory, "engine.cpp"), artifact.Text);
        File.WriteAllText(Path.Combine(directory, "consumer.c"), $$"""
            #include "engine.h"
            #include <assert.h>
            #include <string.h>
            static uint32_t calls(uint64_t runtime) {
                cilica_v3_result result = {0}; assert({{api}}_invoke(runtime, 6, NULL, 0, &result) == 0); return (uint32_t)result.value.payload;
            }
            int main(void) {
                assert({{api}}_capabilities() == (255 | 512 | 1024 | CILICA_V3_CAP_RECORDS));
                cilica_v3_config config = CILICA_V3_CONFIG_INIT; config.object_limit = 16; config.handle_limit = 4;
                for (uint32_t stress = 0; stress < 2; ++stress) {
                    config.flags = stress; uint64_t runtime = 0;
                    assert({{api}}_create(3, CILICA_RECORDS_ABI_V3_CONTRACT_HASH, &config, &runtime) == 0);
                    cilica_v3_value arg = {1, 0, 10}, fields[4]; memset(fields, 0x5a, sizeof(fields));
                    cilica_v3_value untouched[4]; memcpy(untouched, fields, sizeof(fields));
                    cilica_v3_result result = {0}; result.value.payload = 123; uint64_t exception = 123;
                    assert({{api}}_invoke(runtime, 1, &arg, 1, &result) == CILICA_V3_INVALID_ARGUMENT && result.value.payload == 123);
                    assert({{api}}_invoke_records(runtime, 1, &arg, 1, fields, 2, &result, &exception) == CILICA_V3_BUFFER_TOO_SMALL);
                    assert(exception == 123 && result.value.payload == 123 && memcmp(fields, untouched, sizeof(fields)) == 0);
                    assert({{api}}_invoke_records(runtime, 1, &arg, 1, NULL, 3, &result, NULL) == CILICA_V3_INVALID_ARGUMENT);
                    assert({{api}}_invoke_records(runtime, 1, &arg, 1, fields, 4, &result, NULL) == 0);
                    assert(result.value.kind == CILICA_V3_RECORD && result.value.payload == CILICA_RECORDS_ABI_V3_RECORD_PACKET);
                    assert(fields[0].kind == 1 && fields[0].payload == {{original.Tag}} && fields[1].kind == 3 && fields[1].payload == UINT64_C({{unchecked((ulong)original.Point.Y)}}));
                    assert(fields[2].kind == 1 && fields[2].payload == {{original.Point.X}} && memcmp(&fields[3], &untouched[3], sizeof(fields[3])) == 0);
                    cilica_v3_value args[5] = { fields[0], fields[1], fields[2], {1, 0, 3}, {3, 0, UINT64_C(9223372036854775808)} };
                    cilica_v3_value saved[5]; memcpy(saved, args, sizeof(args));
                    assert({{api}}_invoke_records(runtime, 2, args, 4, fields, 3, &result, &exception) == 0 && exception == 0);
                    assert(fields[0].payload == {{expected.Tag}} && fields[1].payload == UINT64_C({{unchecked((ulong)expected.Point.Y)}}) && fields[2].payload == {{expected.Point.X}});
                    assert(memcmp(saved, args, sizeof(args)) == 0); /* Struct inputs are copied. */
                    assert({{api}}_invoke_records(runtime, 3, fields, 3, NULL, 0, &result, NULL) == 0);
                    assert(result.value.kind == 3 && result.value.payload == UINT64_C({{unchecked((ulong)EmbeddingRecordProbe.Score(expected))}}));
                    arg.payload = 2;
                    assert({{api}}_invoke(runtime, 5, &arg, 1, &result) == 0); uint64_t other = result.value.payload;
                    args[0].kind = 2; args[0].payload = other; args[1] = fields[0]; args[2] = fields[1]; args[3] = fields[2];
                    assert({{api}}_invoke_records(runtime, 4, args, 5, fields, 3, &result, NULL) == 0);
                    assert(fields[0].payload == {{mixed.Tag}} && fields[1].payload == UINT64_C({{unchecked((ulong)mixed.Point.Y)}}) && fields[2].payload == {{mixed.Point.X}});
                    assert({{api}}_release_handle(runtime, other) == 0);
                    memcpy(args, saved, sizeof(args)); uint32_t before = calls(runtime);
                    memcpy(fields, untouched, sizeof(fields)); result.value.payload = 123; exception = 123;
                    assert({{api}}_invoke_records(runtime, 2, args, 4, fields, 2, &result, &exception) == CILICA_V3_BUFFER_TOO_SMALL && calls(runtime) == before);
                    args[1].kind = 1;
                    assert({{api}}_invoke_records(runtime, 2, args, 4, fields, 3, &result, &exception) == CILICA_V3_INVALID_ARGUMENT && calls(runtime) == before);
                    args[1].kind = 3; args[2].reserved = 1;
                    assert({{api}}_invoke_records(runtime, 2, args, 4, fields, 3, &result, &exception) == CILICA_V3_INVALID_ARGUMENT);
                    assert(result.value.payload == 123 && exception == 123 && memcmp(fields, untouched, sizeof(fields)) == 0);
                    args[2].reserved = 0; args[3].payload = UINT32_MAX;
                    assert({{api}}_invoke_records(runtime, 2, args, 4, fields, 3, &result, &exception) == CILICA_V3_EXECUTION_FAILED && exception != 0);
                    assert(result.value.kind == 0 && result.fault_code == 13 && memcmp(fields, untouched, sizeof(fields)) == 0);
                    cilica_v3_exception_info info = {0};
                    assert({{api}}_exception_copy(runtime, exception, NULL, 0, NULL, 0, &info) == CILICA_V3_BUFFER_TOO_SMALL && info.message_length == 14);
                    assert({{api}}_release_handle(runtime, exception) == 0);
                    assert({{api}}_destroy(runtime) == 0);
                }
                uint64_t runtime = 0; config.handle_limit = 0;
                assert({{api}}_create(3, CILICA_RECORDS_ABI_V3_CONTRACT_HASH, &config, &runtime) == 0);
                cilica_v3_value arg = {1, 0, 9}, fields[3]; cilica_v3_result result = {0}; uint64_t exception = 123;
                assert({{api}}_invoke_records(runtime, 1, &arg, 1, fields, 3, &result, NULL) == 0);
                assert({{api}}_invoke_records(runtime, 1, &arg, 1, fields, 3, &result, &exception) == CILICA_V3_LIMIT && exception == 123);
                assert({{api}}_destroy(runtime) == 0);
                config.execution_bytes = 0;
                assert({{api}}_create(3, CILICA_RECORDS_ABI_V3_CONTRACT_HASH, &config, &runtime) == 0);
                memset(fields, 0x5a, sizeof(fields)); cilica_v3_value untouched[3]; memcpy(untouched, fields, sizeof(fields));
                assert({{api}}_invoke_records(runtime, 1, &arg, 1, fields, 3, &result, NULL) == CILICA_V3_EXECUTION_FAILED && result.fault_code == 9);
                assert(memcmp(fields, untouched, sizeof(fields)) == 0);
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
    public async Task Maximum_record_return_preserves_frame_bound_and_capture_can_be_disabled(bool optimized)
    {
        var seed = await Program.Value; var flow = seed.Flow.Flow.Flow;
        var type = Method(seed, "Make").ReturnType.Class!;
        var declaration = flow.Types.Types.Single(t => ObjectTypeId.From(t.Source) == type);
        var padding = Enumerable.Range(0, 61).Select(i => new ObjectField(new(type, 0x04007000 + i),
            "Tail" + i.ToString(CultureInfo.InvariantCulture), FieldAttributes.Public, new ObjectStorage.Int64())).ToArray();
        var updated = flow with { Types = flow.Types with { Types = flow.Types.Types.Replace(declaration,
            declaration with { Fields = [.. declaration.Fields, .. padding] }) } };
        var program = seed with { Flow = seed.Flow with { Flow = seed.Flow.Flow with { Flow = updated } } };
        var contract = Contract(program) with { CaptureExceptions = false };
        var slots = ValueLayoutPlanner.Build(updated.Types).Single(p => p.Type == type).Slots;
        Assert.Equal(64, slots.Length);
        var record = contract.Records[0] with { Fields = [.. contract.Records[0].Fields,
            .. Enumerable.Range(3, 61).Select(i => new EmbeddingRecordField((uint)i + 100,
                "tail_" + i.ToString(CultureInfo.InvariantCulture), slots[i].Fields, EmbeddingValueKind.Integer64))] };
        contract = contract with { Records = [record] };
        // Public copies must not accidentally expose a private leaf inside a public value.
        var privateLayout = updated.Types.Types.Single(t => ObjectTypeId.From(t.Source) == type);
        var privateFlow = updated with { Types = updated.Types with { Types = updated.Types.Types.Replace(privateLayout,
            privateLayout with { Fields = privateLayout.Fields.SetItem(privateLayout.Fields.Length - 1, padding[^1] with { Attributes = FieldAttributes.Private }) }) } };
        Assert.Throws<ArgumentException>(() => EmbeddingObjectModuleEmitter.EmitExceptions(
            program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = privateFlow } } }, contract));
        var artifact = EmbeddingObjectModuleEmitter.EmitExceptions(program, contract); string api = artifact.SymbolPrefix;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("embedding-record-capacity");
        File.WriteAllText(Path.Combine(directory, "capacity.cpp"), artifact.Text + $$"""
            #include <cassert>
            static cilica_v3_value input[65], output[64];
            int main() {
                assert({{api}}_capabilities() == (255 | 512 | CILICA_V3_CAP_RECORDS));
                cilica_v3_config config = CILICA_V3_CONFIG_INIT; config.handle_limit = 0; config.flags = 1;
                uint64_t runtime = 0;
                assert({{api}}_create(3, CILICA_RECORDS_ABI_V3_CONTRACT_HASH, &config, &runtime) == 0);
                for (uint32_t i = 0; i < 64; ++i) input[i] = {i == 0 || i == 2 ? 1u : 3u, 0u, i == 0 || i == 2 ? i : UINT64_MAX - i};
                input[64] = {1, 0, 0}; cilica_v3_result result{};
                assert({{api}}_invoke_records(runtime, 2, input, 65, output, 64, &result, nullptr) == 0);
                assert(result.value.kind == 4 && result.value.payload == 1);
                for (uint32_t i = 0; i < 64; ++i) assert(output[i].kind == input[i].kind && output[i].payload == input[i].payload && output[i].reserved == 0);
                uint64_t exception = 123;
                assert({{api}}_invoke_records(runtime, 2, input, 65, output, 64, &result, &exception) == CILICA_V3_INVALID_ARGUMENT && exception == 123);
                assert({{api}}_destroy(runtime) == 0);
            }
            """);
        string executable = Path.Combine(directory, "capacity");
        await Check(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", "-Wframe-larger-than=2048",
                optimized ? "-O2" : "-O0", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", "capacity.cpp", "-o", executable], directory);
        await Check(executable, [], directory);
    }

    private static async Task Check(string executable, string[] arguments, string directory)
    {
        var result = await NativeProcess.Run(executable, arguments, directory);
        Assert.True(result.ExitCode == 0, result.Stdout + result.Stderr); Assert.Empty(result.Stderr);
    }
}
