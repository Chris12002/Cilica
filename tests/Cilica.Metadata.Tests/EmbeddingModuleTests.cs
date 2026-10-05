using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class EmbeddingModuleTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram>> Program = new(async () =>
        (await CoreLibraryBuildTests.TranslateFixtures(typeof(EmbeddingModuleProbe), nameof(EmbeddingModuleProbe.ModuleRoot)))[0]);
    private static readonly Lazy<(int Delta, int Value, int State, bool Fault)[]> Oracle = new(() =>
    {
        EmbeddingModuleProbe.Reset(10);
        return Enumerable.Range(0, 100).Select(i => {
            int delta = i % 7 == 0 ? -3 : 2, value = 0; bool fault = false;
            try { value = EmbeddingModuleProbe.Step(10, delta); }
            catch (InvalidOperationException) { fault = true; }
            return (delta, value, EmbeddingModuleProbe.Read(), fault);
        }).ToArray();
    });

    private static EmbeddingModuleContract Contract(ManagedExceptionIrProgram program)
    {
        var methods = program.Flow.Flow.Flow.Methods;
        EmbeddingExport Export(uint id, string name, string method, int arity) => new(id, name,
            IrMethodId.From(methods.Single(m => m.Source.Token == typeof(EmbeddingModuleProbe).GetMethod(method)!.MetadataToken).Source), arity);
        return new("counter", 1, [Export(10, "reset", "Reset", 1), Export(20, "step", "Step", 2),
            Export(30, "read", "Read", 0), Export(40, "recover", "Catch", 2)]);
    }

    [Theory]
    [InlineData("name")] [InlineData("reserved_name")] [InlineData("trailing_name")]
    [InlineData("version")] [InlineData("empty")] [InlineData("default")]
    [InlineData("count")] [InlineData("zero_id")] [InlineData("duplicate_id")]
    [InlineData("duplicate_name")] [InlineData("duplicate_method")] [InlineData("export_name")]
    [InlineData("missing")] [InlineData("arity")] [InlineData("private")] [InlineData("inline")]
    public async Task Invalid_contracts_fail_before_a_module_is_returned(string mutation)
    {
        var program = await Program.Value; var contract = Contract(program); var exports = contract.Exports;
        var first = exports[0];
        contract = mutation switch {
            "name" => contract with { Name = "counter\n#error injected" },
            "reserved_name" => contract with { Name = "counter__engine" },
            "trailing_name" => contract with { Name = "counter_" },
            "version" => contract with { SchemaVersion = 0 },
            "empty" => contract with { Exports = [] },
            "default" => contract with { Exports = default },
            "count" => contract with { Exports = Enumerable.Repeat(first, 65).ToImmutableArray() },
            _ => contract with { Exports = exports.SetItem(0, mutation switch {
                "zero_id" => first with { Id = 0 },
                "duplicate_id" => first with { Id = exports[1].Id },
                "duplicate_name" => first with { Name = exports[1].Name },
                "duplicate_method" => first with { Method = exports[1].Method, ParameterCount = 2 },
                "export_name" => first with { Name = "reset);" },
                "missing" => first with { Method = first.Method with { Token = 0x06ffffff } },
                "arity" => first with { ParameterCount = 2 },
                "private" => first with { Method = IrMethodId.From(program.Flow.Flow.Flow.Methods.Single(m => m.Source.Name.EndsWith("::Private", StringComparison.Ordinal)).Source) },
                "inline" => first with { Method = IrMethodId.From(program.Flow.Flow.Flow.Methods.Single(m => m.Source.Token == typeof(EmbeddingModuleProbe).GetMethod("Inline")!.MetadataToken).Source) },
                _ => throw new InvalidOperationException(mutation)
            }) }
        };
        Assert.Throws<ArgumentException>(() => EmbeddingModuleEmitter.EmitExceptions(program, contract));
    }

    [Fact]
    public async Task Manifest_is_canonical_and_compatibility_hash_excludes_build_bindings()
    {
        var program = await Program.Value; var contract = Contract(program);
        var first = EmbeddingModuleEmitter.EmitExceptions(program, contract);
        var reordered = EmbeddingModuleEmitter.EmitExceptions(program, contract with { Exports = [.. contract.Exports.Reverse()] });
        Assert.Equal(first, reordered);
        using var manifest = JsonDocument.Parse(first.ManifestJson);
        Assert.Equal(4, manifest.RootElement.GetProperty("bindings").GetArrayLength());
        Assert.Equal(new uint[] { 10, 20, 30, 40 }, manifest.RootElement.GetProperty("contract").GetProperty("exports").EnumerateArray().Select(e => e.GetProperty("id").GetUInt32()));
        // An application schema-version change must change the negotiated interface.
        var changed = EmbeddingModuleEmitter.EmitExceptions(program, contract with { SchemaVersion = 2 });
        Assert.NotEqual(first.ContractHash, changed.ContractHash);
        Assert.Equal(first.SymbolPrefix, changed.SymbolPrefix);
        // Changing only which compatible method implements an export is a build change.
        var exports = contract.Exports;
        var remapped = contract with { Exports = exports.SetItem(1, exports[1] with { Method = exports[3].Method })
            .SetItem(3, exports[3] with { Method = exports[1].Method }) };
        var rebuilt = EmbeddingModuleEmitter.EmitExceptions(program, remapped);
        Assert.Equal(first.ContractHash, rebuilt.ContractHash);
        Assert.NotEqual(first.ManifestJson, rebuilt.ManifestJson);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Maximum_export_table_bounds_native_stack_and_serializes_all_export_entries(bool optimized)
    {
        var seed = NativeObjectExecutionTests.Translate(nameof(ObjectProbe.ArgumentBeforeNull));
        var original = seed.Flow.Methods.Single(m => IrMethodId.From(m.Source) == seed.Flow.Entry);
        var binding = seed.Bindings.Single(b => b.Method == seed.Flow.Entry);
        IrMethodId Id(int index) => new(original.Source.ContentHash, 0x06001000 + index);
        var methods = Enumerable.Range(0, 64).Select(i => original with {
            Source = original.Source with { Token = Id(i).Token }, Parameters = [IrType.Int32], Locals = [],
            Blocks = [new(0, 0, [], [
                new IrStatement.Define(new(0, IrType.Int32, 0, new IrExpression.Parameter(0))),
                new IrStatement.Define(new(1, IrType.Int32, 1, new IrExpression.Constant(i))),
                new IrStatement.Define(new(2, IrType.Int32, 2, new IrExpression.Binary(Int32Operation.AddWrapping, 0, 1)))], new IrReturn(3, 2))]
        }).ToArray();
        ImmutableArray<IrStatement> statements = [new IrStatement.Define(new(0, IrType.Int32, 0, new IrExpression.Parameter(0))),
            .. Enumerable.Range(1, 63).Select(i => new IrStatement.Define(new(i, IrType.Int32, i, new IrExpression.Call(Id(i), [0])), IrExceptionTarget.MethodExit))];
        methods[0] = methods[0] with { Blocks = [new(0, 0, [], statements, new IrReturn(64, 63))] };
        var program = seed with { Flow = seed.Flow with { Entry = Id(0), Methods = [.. methods],
            Types = new([seed.Flow.Types.ObjectBase], seed.Flow.Types.ObjectBase,
                [seed.Flow.Types.Types.Single(t => ObjectTypeId.From(t.Source) == seed.Flow.Types.ObjectBase)]) },
            Bindings = [.. methods.Select(m => binding with { Method = IrMethodId.From(m.Source) })] };
        var artifact = EmbeddingModuleEmitter.EmitProgram(program, new("capacity", 1,
            [.. Enumerable.Range(0, 64).Select(i => new EmbeddingExport((uint)i + 1, "export_" + i.ToString(CultureInfo.InvariantCulture), Id(i), 1))]));
        string api = artifact.SymbolPrefix;
        var source = artifact.Text + $$"""
            #include <cassert>
            #include <thread>
            #include <barrier>
            int main() {
                uint64_t runtime = 0;
                assert({{api}}_create(2, CILICA_CAPACITY_ABI_V2_CONTRACT_HASH, nullptr, &runtime) == CILICA_V2_OK);
                const int32_t input = 10;
                for (uint32_t id = 1; id <= 64; ++id) {
                    cilica_v2_result result{};
                    assert({{api}}_invoke(runtime, id, &input, 1, &result) == CILICA_V2_OK);
                    assert(result.value == (id == 1 ? 73 : 10 + static_cast<int32_t>(id) - 1));
                }
                std::array<std::thread, 4> workers;
                std::barrier start(4);
                for (std::size_t i = 0; i < workers.size(); ++i) workers[i] = std::thread([&, i] {
                    start.arrive_and_wait();
                    for (int call = 0; call < 100; ++call) {
                        cilica_v2_result result{}; result.value = -123;
                        auto status = {{api}}_invoke(runtime, static_cast<uint32_t>(i + 1), &input, 1, &result);
                        assert((status == CILICA_V2_OK && result.value == (i == 0 ? 73 : 10 + static_cast<int32_t>(i)))
                            || (status == CILICA_V2_BUSY && result.value == -123));
                    }
                });
                for (auto& worker : workers) worker.join();
                assert({{api}}_destroy(runtime) == CILICA_V2_OK);
                uint64_t slots[64];
                for (auto& slot : slots) assert({{api}}_create(2, CILICA_CAPACITY_ABI_V2_CONTRACT_HASH, nullptr, &slot) == CILICA_V2_OK);
                assert({{api}}_create(2, CILICA_CAPACITY_ABI_V2_CONTRACT_HASH, nullptr, &runtime) == CILICA_V2_LIMIT);
                for (auto slot : slots) assert({{api}}_destroy(slot) == CILICA_V2_OK);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("embedding-capacity");
        File.WriteAllText(Path.Combine(directory, "capacity.cpp"), source);
        string executable = Path.Combine(directory, "capacity");
        await Check(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", "-pthread", "-Wframe-larger-than=2048",
                optimized ? "-O2" : "-O0", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", "capacity.cpp", "-o", executable], directory);
        await Check(executable, [], directory);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task C_client_dispatches_shared_state_and_checks_manifest_quota_and_error_contracts(bool optimized)
    {
        var program = await Program.Value;
        var artifact = EmbeddingModuleEmitter.EmitExceptions(program, Contract(program));
        string api = artifact.SymbolPrefix;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("embedding-module");
        File.WriteAllText(Path.Combine(directory, "engine.h"), artifact.Header);
        File.WriteAllText(Path.Combine(directory, "engine.cpp"), artifact.Text);
        File.WriteAllText(Path.Combine(directory, "manifest.json"), artifact.ManifestJson);
        // Only authored fixture code executes in the .NET oracle; compilation reads IL.
        var expected = new StringBuilder();
        foreach (var sample in Oracle.Value)
        {
            expected.Append(CultureInfo.InvariantCulture, $$"""
                args[1] = {{sample.Delta}};
                status = {{api}}_invoke(runtime, 20, args, 2, &result);
                assert(status == {{(sample.Fault ? "CILICA_V2_EXECUTION_FAILED" : "CILICA_V2_OK")}});
                assert(result.value == {{sample.Value}});
                {{(sample.Fault ? "assert(result.fault_code == 13 && strlen(result.image_hash) == 64);" : "assert(result.fault_code == 0 && result.image_hash[0] == 0);")}}
                assert({{api}}_invoke(runtime, 30, NULL, 0, &result) == CILICA_V2_OK && result.value == {{sample.State}});
                """);
        }
        File.WriteAllText(Path.Combine(directory, "consumer.c"), $$"""
            #include "engine.h"
            #include "engine.h"
            #include <assert.h>
            #include <stddef.h>
            #include <string.h>
            _Static_assert(sizeof(cilica_v2_config) == 20, "config layout");
            _Static_assert(offsetof(cilica_v2_result, image_hash) == 16, "result layout");
            int main(void) {
                assert({{api}}_version() == 2);
                assert({{api}}_capabilities() == (CILICA_V2_CAP_INT32 | CILICA_V2_CAP_PERSISTENT | CILICA_V2_CAP_MULTI_EXPORT |
                    CILICA_V2_CAP_COPIED_FAULT | CILICA_V2_CAP_GC_STRESS | CILICA_V2_CAP_QUOTAS));
                uint64_t runtime = 0, other = 0, unchanged = 123;
                cilica_v2_config config = CILICA_V2_CONFIG_INIT;
                const char* hash = CILICA_COUNTER_ABI_V2_CONTRACT_HASH;
                char bad[65]; memcpy(bad, hash, 65); bad[0] = bad[0] == '0' ? '1' : '0';
                assert({{api}}_create(2, bad, NULL, &unchanged) == CILICA_V2_CONTRACT_MISMATCH && unchanged == 123);
                assert({{api}}_create(1, hash, NULL, &unchanged) == CILICA_V2_UNSUPPORTED_VERSION && unchanged == 123);
                assert({{api}}_create(2, NULL, NULL, &unchanged) == CILICA_V2_INVALID_ARGUMENT);
                assert({{api}}_create(2, hash, NULL, NULL) == CILICA_V2_INVALID_ARGUMENT);
                config.struct_size = 0;
                assert({{api}}_create(2, hash, &config, &unchanged) == CILICA_V2_INVALID_ARGUMENT);
                config.struct_size = sizeof(config); config.flags = 2;
                assert({{api}}_create(2, hash, &config, &unchanged) == CILICA_V2_INVALID_ARGUMENT);
                config.flags = 0; config.heap_bytes++;
                assert({{api}}_create(2, hash, &config, &unchanged) == CILICA_V2_INVALID_ARGUMENT);
                config.heap_bytes--; config.object_limit++;
                assert({{api}}_create(2, hash, &config, &unchanged) == CILICA_V2_INVALID_ARGUMENT);
                config.object_limit--; config.execution_bytes++;
                assert({{api}}_create(2, hash, &config, &unchanged) == CILICA_V2_INVALID_ARGUMENT);
                config.execution_bytes--;
                for (uint32_t flags = 0; flags < 2; ++flags) {
                    config.flags = flags;
                    assert({{api}}_create(2, hash, &config, &runtime) == CILICA_V2_OK);
                    assert({{api}}_create(2, hash, NULL, &other) == CILICA_V2_OK);
                    int32_t args[] = {10, 2}; cilica_v2_result result = {0}; result.value = 123;
                    assert({{api}}_invoke(runtime, 0, args, 2, &result) == CILICA_V2_UNKNOWN_EXPORT && result.value == 123);
                    assert({{api}}_invoke(runtime, 10, args, 2, &result) == CILICA_V2_INVALID_ARGUMENT && result.value == 123);
                    assert({{api}}_invoke(runtime, 20, NULL, 2, &result) == CILICA_V2_INVALID_ARGUMENT);
                    assert({{api}}_invoke(runtime, 20, args, 2, NULL) == CILICA_V2_INVALID_ARGUMENT);
                    assert({{api}}_invoke(0, 30, NULL, 0, &result) == CILICA_V2_INVALID_RUNTIME && result.value == 123);
                    assert({{api}}_invoke(runtime, CILICA_COUNTER_ABI_V2_EXPORT_READ, NULL, 0, &result) == CILICA_V2_OK && result.value == -100);
                    assert({{api}}_invoke(runtime, CILICA_COUNTER_ABI_V2_EXPORT_RESET, args, 1, &result) == CILICA_V2_OK && result.value == 10);
                    uint32_t status;
                    {{expected}}
                    assert({{api}}_invoke(other, 30, NULL, 0, &result) == CILICA_V2_OK && result.value == -100);
                    args[1] = -3;
                    assert({{api}}_invoke(runtime, 40, args, 2, &result) == CILICA_V2_OK && result.value == 42);
                    assert(result.fault_code == 0 && result.method_token == 0 && result.il_offset == 0 && result.image_hash[0] == 0);
                    assert({{api}}_invoke(runtime, 30, NULL, 0, &result) == CILICA_V2_OK && result.value == {{Oracle.Value[^1].State - 3}});
                    assert({{api}}_destroy(runtime) == CILICA_V2_OK);
                    assert({{api}}_invoke(runtime, 30, NULL, 0, &result) == CILICA_V2_INVALID_RUNTIME);
                    assert({{api}}_destroy(runtime) == CILICA_V2_INVALID_RUNTIME);
                    assert({{api}}_destroy(other) == CILICA_V2_OK);
                }
                // Explicit zero limits are real limits, not aliases for defaults.
                for (int quota = 0; quota < 3; ++quota) {
                    cilica_v2_config limited = CILICA_V2_CONFIG_INIT;
                    if (quota == 0) limited.heap_bytes = 0;
                    if (quota == 1) limited.object_limit = 0;
                    if (quota == 2) limited.execution_bytes = 0;
                    assert({{api}}_create(2, hash, &limited, &runtime) == CILICA_V2_OK);
                    int32_t input = 10; cilica_v2_result result = {0};
                    assert({{api}}_invoke(runtime, 10, &input, 1, &result) == CILICA_V2_EXECUTION_FAILED);
                    assert(result.value == 0 && result.fault_code == (quota == 2 ? 9u : 5u));
                    assert({{api}}_destroy(runtime) == CILICA_V2_OK);
                }
                return 0;
            }
            """);
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        string[] flags = ["-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-fsanitize=address,undefined", "-fno-sanitize-recover=all"];
        string library = Path.Combine(directory, OperatingSystem.IsMacOS() ? "libengine.dylib" : "libengine.so");
        await Check(compiler, ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-fvisibility=hidden", "-fPIC",
            OperatingSystem.IsMacOS() ? "-dynamiclib" : "-shared", .. flags, "engine.cpp", "-o", library], directory);
        await Check(compiler, ["-x", "c", "-std=c11", .. flags, "-c", "consumer.c", "-o", "consumer.o"], directory);
        string executable = Path.Combine(directory, "consumer");
        await Check(compiler, [.. flags, "consumer.o", library, "-o", executable], directory);
        await Check(executable, [], directory);
    }

    private static async Task Check(string executable, string[] arguments, string directory)
    {
        var result = await NativeProcess.Run(executable, arguments, directory);
        Assert.True(result.ExitCode == 0, result.Stdout + result.Stderr);
        Assert.Empty(result.Stderr);
    }
}
