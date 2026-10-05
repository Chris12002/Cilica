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

public sealed class EmbeddingInteger64Tests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram>> Program = new(async () =>
        (await CoreLibraryBuildTests.TranslateFixtures(typeof(EmbeddingInteger64Probe), nameof(EmbeddingInteger64Probe.Root)))[0]);
    private static readonly EmbeddingValueShape Wide = new(EmbeddingValueKind.Integer64), Narrow = new(EmbeddingValueKind.Int32),
        Box = new(EmbeddingValueKind.Handle, 1);
    private static EmbeddingObjectContract Contract(ManagedExceptionIrProgram program)
    {
        var methods = program.Flow.Flow.Flow.Methods;
        IrMethod Method(string name) => methods.Single(m => m.Source.Token == typeof(EmbeddingInteger64Probe).GetMethod(name)!.MetadataToken);
        EmbeddingObjectExport Export(uint id, string name, string method, EmbeddingValueShape result, params EmbeddingValueShape[] parameters) =>
            new(id, name, IrMethodId.From(Method(method).Source), [.. parameters], result);
        return new("wide", 1, [new(1, "box", Method("Make").ReturnType.Class!)], [
            Export(1, "signed", "Signed", Wide, Wide), Export(2, "unsigned", "Unsigned", Wide, Wide),
            Export(3, "add", "Add", Wide, Wide, Wide), Export(4, "checked_add", "CheckedAdd", Wide, Wide, Wide),
            Export(5, "checked_unsigned_add", "CheckedUnsignedAdd", Wide, Wide, Wide),
            Export(6, "divide", "Divide", Wide, Wide, Wide), Export(7, "unsigned_divide", "UnsignedDivide", Wide, Wide, Wide),
            Export(8, "shift", "Shift", Wide, Wide, Narrow), Export(9, "make", "Make", Box, Wide),
            Export(10, "read", "Read", Wide, Box), Export(11, "set", "Set", new(EmbeddingValueKind.Void), Box, Wide),
            Export(12, "constant", "Constant", Wide)]);
    }

    [Theory]
    [InlineData("parameter")] [InlineData("result")] [InlineData("reference_id")] [InlineData("unknown_kind")]
    public async Task Wide_contract_rejects_wrong_shapes(string mutation)
    {
        var program = await Program.Value; var contract = Contract(program); var export = contract.Exports[0];
        export = mutation switch {
            "parameter" => export with { Parameters = [Narrow] },
            "result" => export with { Result = Narrow },
            "reference_id" => export with { Parameters = [new(EmbeddingValueKind.Integer64, 1)] },
            _ => export with { Parameters = [new((EmbeddingValueKind)99)] }
        };
        Assert.Throws<ArgumentException>(() => EmbeddingObjectModuleEmitter.EmitExceptions(program,
            contract with { Exports = contract.Exports.SetItem(0, export) }));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task C_host_transfers_wide_bits_and_matches_signed_unsigned_dotnet_operations(bool optimized)
    {
        var program = await Program.Value;
        var artifact = EmbeddingObjectModuleEmitter.EmitExceptions(program, Contract(program));
        Assert.Contains("integer64", artifact.ManifestJson, StringComparison.Ordinal);
        string api = artifact.SymbolPrefix;
        ulong[] edges = [0, 1, 63, 64, 65, uint.MaxValue, 0x100000000, 0x1fffffffffffff, 0x20000000000001,
            0x7fffffffffffffff, 0x8000000000000000, 0x8000000000000001, ulong.MaxValue];
        var cases = new StringBuilder();
        foreach (uint id in Enumerable.Range(1, 8).Select(i => (uint)i))
            foreach (ulong a in edges) foreach (ulong b in edges) {
                uint fault = 0; ulong expected = 0;
                try {
                    expected = id switch {
                        1 => unchecked((ulong)EmbeddingInteger64Probe.Signed((long)a)),
                        2 => EmbeddingInteger64Probe.Unsigned(a),
                        3 => unchecked((ulong)EmbeddingInteger64Probe.Add((long)a, (long)b)),
                        4 => unchecked((ulong)EmbeddingInteger64Probe.CheckedAdd((long)a, (long)b)),
                        5 => EmbeddingInteger64Probe.CheckedUnsignedAdd(a, b),
                        6 => unchecked((ulong)EmbeddingInteger64Probe.Divide((long)a, (long)b)),
                        7 => EmbeddingInteger64Probe.UnsignedDivide(a, b),
                        _ => unchecked((ulong)EmbeddingInteger64Probe.Shift((long)a, (int)b))
                    };
                } catch (DivideByZeroException) { fault = 1; } catch (OverflowException) { fault = 2; }
                cases.Append(CultureInfo.InvariantCulture, $"{{{id}u, UINT64_C({a}), UINT64_C({b}), {fault}u, UINT64_C({expected})}},\n");
            }
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("embedding-wide");
        File.WriteAllText(Path.Combine(directory, "engine.h"), artifact.Header);
        File.WriteAllText(Path.Combine(directory, "engine.cpp"), artifact.Text);
        File.WriteAllText(Path.Combine(directory, "consumer.c"), $$"""
            #include "engine.h"
            #include <assert.h>
            #include <string.h>
            #include <stddef.h>
            _Static_assert(sizeof(cilica_v3_value) == 16 && offsetof(cilica_v3_value, payload) == 8, "unchanged value layout");
            static const struct { uint32_t id; uint64_t a, b; uint32_t fault; uint64_t expected; } cases[] = {
                {{cases}}
            };
            int main(void) {
                assert({{api}}_capabilities() == (255 | CILICA_V3_CAP_INTEGER64));
                cilica_v3_config config = CILICA_V3_CONFIG_INIT; config.flags = 1;
                config.handle_limit = 0;
                uint64_t runtime = 0;
                assert({{api}}_create(3, CILICA_WIDE_ABI_V3_CONTRACT_HASH, &config, &runtime) == 0);
                cilica_v3_result result = {0};
                assert({{api}}_invoke(runtime, 12, NULL, 0, &result) == 0);
                assert(result.value.kind == CILICA_V3_INTEGER64 && result.value.payload == UINT64_C(9223372036854775808));
                for (size_t i = 0; i < sizeof(cases) / sizeof(cases[0]); ++i) {
                    cilica_v3_value args[2] = { {3, 0, cases[i].a}, {3, 0, cases[i].b} };
                    if (cases[i].id == 8) { args[1].kind = 1; args[1].payload = (uint32_t)args[1].payload; }
                    uint32_t status = {{api}}_invoke(runtime, cases[i].id, args, cases[i].id <= 2 ? 1 : 2, &result);
                    assert(result.fault_code == cases[i].fault);
                    if (cases[i].fault == 0) {
                        assert(status == CILICA_V3_OK && result.value.kind == CILICA_V3_INTEGER64);
                        assert(result.value.reserved == 0 && result.value.payload == cases[i].expected);
                        assert(result.method_token == 0 && result.il_offset == 0 && result.image_hash[0] == 0);
                    } else {
                        assert(status == CILICA_V3_EXECUTION_FAILED && result.value.kind == 0 && result.value.payload == 0);
                        assert(result.method_token != 0 && strlen(result.image_hash) == 64);
                    }
                }
                assert({{api}}_destroy(runtime) == 0);
                config.handle_limit = 4;
                assert({{api}}_create(3, CILICA_WIDE_ABI_V3_CONTRACT_HASH, &config, &runtime) == 0);
                cilica_v3_value args[2] = { {3, 0, UINT64_MAX}, {3, 0, UINT64_C(9007199254740993)} };
                assert({{api}}_invoke(runtime, 9, args, 1, &result) == 0);
                uint64_t box = result.value.payload; args[0].kind = 2; args[0].payload = box;
                assert({{api}}_invoke(runtime, 10, args, 1, &result) == 0 && result.value.payload == UINT64_MAX);
                assert({{api}}_invoke(runtime, 11, args, 2, &result) == 0 && result.value.kind == 0);
                assert({{api}}_invoke(runtime, 10, args, 1, &result) == 0 && result.value.payload == UINT64_C(9007199254740993));
                cilica_v3_result untouched; memset(&untouched, 0x5a, sizeof(untouched));
                memcpy(&result, &untouched, sizeof(result)); args[1].kind = 1;
                assert({{api}}_invoke(runtime, 11, args, 2, &result) == CILICA_V3_INVALID_ARGUMENT && memcmp(&result, &untouched, sizeof(result)) == 0);
                args[1].kind = 3; args[1].reserved = 1;
                assert({{api}}_invoke(runtime, 11, args, 2, &result) == CILICA_V3_INVALID_ARGUMENT && memcmp(&result, &untouched, sizeof(result)) == 0);
                args[1].reserved = 0; args[1].kind = 2; args[1].payload = box;
                assert({{api}}_invoke(runtime, 11, args, 2, &result) == CILICA_V3_INVALID_ARGUMENT);
                args[1].kind = 99;
                assert({{api}}_invoke(runtime, 11, args, 2, &result) == CILICA_V3_INVALID_ARGUMENT);
                assert({{api}}_invoke(runtime, 10, args, 1, &result) == 0 && result.value.payload == UINT64_C(9007199254740993));
                assert({{api}}_release_handle(runtime, box) == 0);
                assert({{api}}_destroy(runtime) == 0);
                config.execution_bytes = 0;
                assert({{api}}_create(3, CILICA_WIDE_ABI_V3_CONTRACT_HASH, &config, &runtime) == 0);
                assert({{api}}_invoke(runtime, 12, NULL, 0, &result) == CILICA_V3_EXECUTION_FAILED && result.fault_code == 9);
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
    public async Task Ordinary_object_packet_preserves_all_wide_bits(bool optimized)
    {
        var seed = NativeObjectExecutionTests.Translate(nameof(ObjectProbe.ArgumentBeforeNull));
        var original = seed.Flow.Methods.Single(m => IrMethodId.From(m.Source) == seed.Flow.Entry);
        var binding = seed.Bindings.Single(b => b.Method == seed.Flow.Entry);
        var wide = IntegerIrTests.Binary(Int64Operation.AddWrapping) with {
            Source = original.Source with { Token = 0x06001000 }
        };
        var wideId = IrMethodId.From(wide.Source);
        var root = original with { Parameters = [], Locals = [], Blocks = [new(0, 0, [], [
            new IrStatement.Define(new(0, IrType.Int64, 0, new IrExpression.Constant64(long.MinValue))),
            new IrStatement.Define(new(1, IrType.Int64, 1, new IrExpression.Call(wideId, [0, 0])), IrExceptionTarget.MethodExit),
            new IrStatement.Define(new(2, IrType.Int32, 2, new IrExpression.ConvertInteger(IntegerConversion.Truncate64, 1)))], new IrReturn(3, 2))] };
        var program = seed with {
            Flow = seed.Flow with { Methods = [root, wide], Types = new([seed.Flow.Types.ObjectBase], seed.Flow.Types.ObjectBase,
                [seed.Flow.Types.Types.Single(t => ObjectTypeId.From(t.Source) == seed.Flow.Types.ObjectBase)]) { Integer64 = true } },
            Bindings = [binding, binding with { Method = wideId }]
        };
        var artifact = EmbeddingObjectModuleEmitter.EmitProgram(program, new("ordinary_wide", 1, [], [new(1, "add", wideId, [Wide, Wide], Wide)]));
        string api = artifact.SymbolPrefix;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("embedding-ordinary-wide");
        File.WriteAllText(Path.Combine(directory, "ordinary.cpp"), artifact.Text + $$"""
            #include <cassert>
            int main() {
                uint64_t runtime = 0;
                assert({{api}}_create(3, CILICA_ORDINARY_WIDE_ABI_V3_CONTRACT_HASH, nullptr, &runtime) == 0);
                cilica_v3_value args[2]{ {3, 0, UINT64_C(9223372036854775808)}, {3, 0, UINT64_C(9007199254740993)} };
                cilica_v3_result result{};
                assert({{api}}_invoke(runtime, 1, args, 2, &result) == 0);
                assert(result.value.kind == 3 && result.value.payload == UINT64_C(9232379236109516801));
                args[0].payload = UINT64_MAX; args[1].payload = 1;
                assert({{api}}_invoke(runtime, 1, args, 2, &result) == 0 && result.value.payload == 0);
                assert({{api}}_destroy(runtime) == 0);
            }
            """);
        string executable = Path.Combine(directory, "ordinary");
        await Check(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", "-Wframe-larger-than=2048",
                optimized ? "-O2" : "-O0", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", "ordinary.cpp", "-o", executable], directory);
        await Check(executable, [], directory);
    }

    private static async Task Check(string executable, string[] arguments, string directory)
    {
        var result = await NativeProcess.Run(executable, arguments, directory);
        Assert.True(result.ExitCode == 0, result.Stdout + result.Stderr); Assert.Empty(result.Stderr);
    }
}
