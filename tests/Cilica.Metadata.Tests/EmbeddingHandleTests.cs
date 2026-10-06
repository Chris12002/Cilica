using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class EmbeddingHandleTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram>> Program = new(async () =>
        (await CoreLibraryBuildTests.TranslateFixtures(typeof(EmbeddingHandleProbe), nameof(EmbeddingHandleProbe.Root)))[0]);
    private static readonly EmbeddingValueShape I32 = new(EmbeddingValueKind.Int32), Void = new(EmbeddingValueKind.Void),
        Node = new(EmbeddingValueKind.Handle, 1), Other = new(EmbeddingValueKind.Handle, 2), Base = new(EmbeddingValueKind.Handle, 3);

    private static EmbeddingObjectContract Contract(ManagedExceptionIrProgram program)
    {
        var methods = program.Flow.Flow.Flow.Methods;
        IrMethod Method(string name) => methods.Single(m => m.Source.Token == typeof(EmbeddingHandleProbe).GetMethod(name)!.MetadataToken);
        EmbeddingObjectExport Export(uint id, string name, string method, EmbeddingValueShape result, params EmbeddingValueShape[] parameters) =>
            new(id, name, IrMethodId.From(Method(method).Source), [.. parameters], result);
        return new("handles", 1, [new(1, "node", Method("Create").ReturnType.Class!), new(2, "other", Method("Other").ReturnType.Class!),
            new(3, "base", Method("ReadBase").Parameters[0].Class!)], [
            Export(10, "create", "Create", Node, I32), Export(20, "read", "Read", I32, Node),
            Export(30, "mutate", "Mutate", Void, Node, I32), Export(40, "alias", "Alias", Node, Node),
            Export(50, "same", "Same", I32, Node, Node), Export(60, "other", "Other", Other),
            Export(70, "calls", "Calls", I32), Export(80, "churn", "Churn", I32, I32),
            Export(90, "link", "Link", Void, Node, Node), Export(100, "read_base", "ReadBase", I32, Base)]);
    }

    [Theory]
    [InlineData("shape")] [InlineData("void_parameter")] [InlineData("missing_type")] [InlineData("duplicate_type")]
    [InlineData("unused_type")] [InlineData("duplicate_id")] [InlineData("arity")] [InlineData("null_shape")]
    [InlineData("scalar_type_id")] [InlineData("wrong_result")] [InlineData("reference_name")]
    public async Task Typed_contracts_reject_incompatible_logical_signatures(string mutation)
    {
        var program = await Program.Value; var contract = Contract(program); var exports = contract.Exports;
        var read = exports[1];
        contract = mutation switch {
            "missing_type" => contract with { References = contract.References.RemoveAt(0) },
            "duplicate_type" => contract with { References = contract.References.SetItem(1, contract.References[1] with { Type = contract.References[0].Type }) },
            "unused_type" => contract with { Exports = exports.RemoveAt(9) },
            "duplicate_id" => contract with { References = contract.References.SetItem(1, contract.References[1] with { Id = 1 }) },
            "reference_name" => contract with { References = contract.References.SetItem(0, contract.References[0] with { Name = "node__bad" }) },
            _ => contract with { Exports = exports.SetItem(1, mutation switch {
                "shape" => read with { Parameters = [Other] },
                "void_parameter" => read with { Parameters = [Void] },
                "arity" => read with { Parameters = [] },
                "null_shape" => read with { Parameters = [null!] },
                "scalar_type_id" => read with { Result = new(EmbeddingValueKind.Int32, 1) },
                "wrong_result" => read with { Result = Node },
                _ => throw new InvalidOperationException(mutation)
            }) }
        };
        Assert.Throws<ArgumentException>(() => EmbeddingObjectModuleEmitter.EmitExceptions(program, contract));
    }

    [Fact]
    public async Task Typed_manifest_is_order_independent_and_pins_reference_aliases()
    {
        var program = await Program.Value; var contract = Contract(program);
        var artifact = EmbeddingObjectModuleEmitter.EmitExceptions(program, contract);
        Assert.Equal(artifact, EmbeddingObjectModuleEmitter.EmitExceptions(program,
            contract with { References = [.. contract.References.Reverse()], Exports = [.. contract.Exports.Reverse()] }));
        var renamed = contract with { References = contract.References.SetItem(0, contract.References[0] with { Name = "renamed" }) };
        Assert.NotEqual(artifact.ContractHash, EmbeddingObjectModuleEmitter.EmitExceptions(program, renamed).ContractHash);
        using var manifest = JsonDocument.Parse(artifact.ManifestJson);
        Assert.Equal("handle", manifest.RootElement.GetProperty("contract").GetProperty("exports")[0].GetProperty("result").GetProperty("kind").GetString());
        Assert.Equal(3, manifest.RootElement.GetProperty("referenceBindings").GetArrayLength());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task C_host_preserves_handle_identity_roots_types_faults_and_explicit_release(bool optimized)
    {
        var program = await Program.Value;
        var artifact = EmbeddingObjectModuleEmitter.EmitExceptions(program, Contract(program));
        string api = artifact.SymbolPrefix;
        // Authored .NET oracle: the host sequence preserves identity and mutates the same object.
        var node = EmbeddingHandleProbe.Create(10);
        var alias = EmbeddingHandleProbe.Alias(node);
        EmbeddingHandleProbe.Mutate(alias, -15);
        int expected = EmbeddingHandleProbe.Read(node), same = EmbeddingHandleProbe.Same(node, alias);
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("embedding-handles");
        File.WriteAllText(Path.Combine(directory, "engine.h"), artifact.Header);
        File.WriteAllText(Path.Combine(directory, "engine.cpp"), artifact.Text);
        File.WriteAllText(Path.Combine(directory, "consumer.c"), $$"""
            #include "engine.h"
            #include <assert.h>
            #include <string.h>
            #include <stddef.h>
            _Static_assert(sizeof(cilica_v3_value) == 16 && offsetof(cilica_v3_value, payload) == 8, "value layout");
            _Static_assert(sizeof(cilica_v3_config) == 24, "config layout");
            static cilica_v3_value integer(int32_t value) { cilica_v3_value result = {1, 0, (uint32_t)value}; return result; }
            static cilica_v3_value handle(uint64_t token) { cilica_v3_value result = {2, 0, token}; return result; }
            static uint64_t create_node(uint64_t runtime, int32_t value) {
                cilica_v3_value argument = integer(value); cilica_v3_result result = {0};
                assert({{api}}_invoke(runtime, 10, &argument, 1, &result) == CILICA_V3_OK);
                assert(result.value.kind == 2 && result.value.payload != 0); return result.value.payload;
            }
            static uint64_t calls(uint64_t runtime) {
                cilica_v3_result result = {0};
                assert({{api}}_invoke(runtime, 70, NULL, 0, &result) == CILICA_V3_OK); return result.value.payload;
            }
            int main(void) {
                assert({{api}}_version() == 3 && {{api}}_capabilities() == UINT64_C(255));
                const char* hash = CILICA_HANDLES_ABI_V3_CONTRACT_HASH;
                uint64_t runtime = 0, other = 0, sentinel = 123;
                cilica_v3_config config = CILICA_V3_CONFIG_INIT;
                config.handle_limit = 1025;
                assert({{api}}_create(3, hash, &config, &sentinel) == CILICA_V3_INVALID_ARGUMENT && sentinel == 123);
                config.handle_limit = 8; config.object_limit = 16;
                assert({{api}}_create(2, hash, NULL, &sentinel) == CILICA_V3_UNSUPPORTED_VERSION);
                char wrong[65]; memcpy(wrong, hash, 65); wrong[0] = '?';
                assert({{api}}_create(3, wrong, NULL, &sentinel) == CILICA_V3_CONTRACT_MISMATCH);
                for (uint32_t stress = 0; stress < 2; ++stress) {
                    config.flags = stress;
                    assert({{api}}_create(3, hash, &config, &runtime) == CILICA_V3_OK);
                    assert({{api}}_create(3, hash, &config, &other) == CILICA_V3_OK);
                    uint64_t original = create_node(runtime, 10), clone = 0;
                    assert({{api}}_retain_handle(runtime, original, &clone) == CILICA_V3_OK && clone != original && clone != 0);
                    cilica_v3_result result = {0}; cilica_v3_value args[2] = {handle(original), handle(clone)};
                    assert({{api}}_invoke(runtime, 50, args, 2, &result) == CILICA_V3_OK && result.value.payload == {{same}});
                    assert({{api}}_invoke(runtime, 40, args, 1, &result) == CILICA_V3_OK);
                    uint64_t returned_alias = result.value.payload;
                    assert(returned_alias != original && returned_alias != clone);
                    assert({{api}}_release_handle(runtime, original) == CILICA_V3_OK);
                    result.value = integer(123);
                    assert({{api}}_invoke(runtime, 20, args, 1, &result) == CILICA_V3_INVALID_HANDLE && result.value.payload == 123);
                    assert({{api}}_release_handle(runtime, original) == CILICA_V3_INVALID_HANDLE);
                    assert({{api}}_retain_handle(runtime, original, &sentinel) == CILICA_V3_INVALID_HANDLE && sentinel == 123);
                    args[0] = handle(clone); args[1] = integer(-15);
                    assert({{api}}_invoke(runtime, 30, args, 2, &result) == CILICA_V3_OK);
                    assert(result.value.kind == 0 && result.value.payload == 0 && result.fault_code == 0);
                    assert({{api}}_invoke(runtime, 20, args, 1, &result) == CILICA_V3_OK && result.value.payload == (uint32_t){{expected}});
                    assert({{api}}_invoke(runtime, 100, args, 1, &result) == CILICA_V3_OK && result.value.payload == (uint32_t){{expected}});
                    result.value = integer(123);
                    assert({{api}}_invoke(other, 20, args, 1, &result) == CILICA_V3_INVALID_HANDLE && result.value.payload == 123);
                    assert({{api}}_release_handle(other, clone) == CILICA_V3_INVALID_HANDLE);
                    assert({{api}}_retain_handle(other, clone, &sentinel) == CILICA_V3_INVALID_HANDLE && sentinel == 123);
                    assert({{api}}_invoke(runtime, 60, NULL, 0, &result) == CILICA_V3_OK);
                    uint64_t unrelated = result.value.payload; args[0] = handle(unrelated); result.value = integer(123);
                    assert({{api}}_invoke(runtime, 20, args, 1, &result) == CILICA_V3_TYPE_MISMATCH && result.value.payload == 123);
                    assert({{api}}_release_handle(runtime, unrelated) == CILICA_V3_OK);
                    args[0] = handle(0);
                    assert({{api}}_invoke(runtime, 40, args, 1, &result) == CILICA_V3_OK && result.value.kind == 2 && result.value.payload == 0);
                    assert({{api}}_invoke(runtime, 20, args, 1, &result) == CILICA_V3_EXECUTION_FAILED && result.fault_code == 4 && result.value.kind == 0);
                    assert(strlen(result.image_hash) == 64);
                    assert({{api}}_release_handle(runtime, 0) == CILICA_V3_OK);
                    uint64_t null_clone = 123;
                    assert({{api}}_retain_handle(runtime, 0, &null_clone) == CILICA_V3_OK && null_clone == 0);
                    // Wrong tags, noncanonical scalars and reserved bits never execute the export.
                    const uint64_t before = calls(runtime); args[0] = integer(9); args[0].reserved = 1;
                    result.value = integer(123);
                    assert({{api}}_invoke(runtime, 10, args, 1, &result) == CILICA_V3_INVALID_ARGUMENT && result.value.payload == 123);
                    args[0].reserved = 0; args[0].payload = UINT64_MAX;
                    assert({{api}}_invoke(runtime, 10, args, 1, &result) == CILICA_V3_INVALID_ARGUMENT);
                    args[0] = handle(clone);
                    assert({{api}}_invoke(runtime, 10, args, 1, &result) == CILICA_V3_INVALID_ARGUMENT);
                    assert(calls(runtime) == before);
                    // Only host handles retain these objects, including a cyclic graph.
                    uint64_t second = create_node(runtime, 4);
                    args[0] = handle(clone); args[1] = handle(second);
                    assert({{api}}_invoke(runtime, 90, args, 2, &result) == CILICA_V3_OK);
                    args[0] = handle(second); args[1] = handle(clone);
                    assert({{api}}_invoke(runtime, 90, args, 2, &result) == CILICA_V3_OK);
                    args[0] = integer(100);
                    assert({{api}}_invoke(runtime, 80, args, 1, &result) == CILICA_V3_OK && result.value.payload == 4950);
                    args[0] = handle(returned_alias);
                    assert({{api}}_invoke(runtime, 20, args, 1, &result) == CILICA_V3_OK && result.value.payload == (uint32_t){{expected}});
                    assert({{api}}_release_handle(runtime, clone) == CILICA_V3_OK);
                    assert({{api}}_release_handle(runtime, returned_alias) == CILICA_V3_OK);
                    assert({{api}}_release_handle(runtime, second) == CILICA_V3_OK);
                    for (int i = 0; i < 40; ++i) {
                        uint64_t a = create_node(runtime, i), b = create_node(runtime, i);
                        args[0] = handle(a); args[1] = handle(b);
                        assert({{api}}_invoke(runtime, 90, args, 2, &result) == CILICA_V3_OK);
                        args[0] = handle(b); args[1] = handle(a);
                        assert({{api}}_invoke(runtime, 90, args, 2, &result) == CILICA_V3_OK);
                        assert({{api}}_release_handle(runtime, a) == CILICA_V3_OK);
                        assert({{api}}_release_handle(runtime, b) == CILICA_V3_OK);
                    }
                    // Destruction invalidates retained handles even when a registry slot is reused.
                    uint64_t stale = create_node(runtime, 5);
                    assert({{api}}_destroy(runtime) == CILICA_V3_OK);
                    assert({{api}}_release_handle(runtime, stale) == CILICA_V3_INVALID_RUNTIME);
                    assert({{api}}_create(3, hash, &config, &runtime) == CILICA_V3_OK);
                    args[0] = handle(stale);
                    assert({{api}}_invoke(runtime, 20, args, 1, &result) == CILICA_V3_INVALID_HANDLE);
                    assert({{api}}_destroy(runtime) == CILICA_V3_OK);
                    assert({{api}}_destroy(other) == CILICA_V3_OK);
                }
                // Reserve before execution: no mutation when the host cannot own the return.
                config.handle_limit = 1;
                assert({{api}}_create(3, hash, &config, &runtime) == CILICA_V3_OK);
                uint64_t held = create_node(runtime, 1); cilica_v3_value arg = integer(-1); cilica_v3_result result = {0};
                result.value = integer(123);
                assert({{api}}_invoke(runtime, 10, &arg, 1, &result) == CILICA_V3_LIMIT && result.value.payload == 123);
                assert(calls(runtime) == 1);
                assert({{api}}_retain_handle(runtime, held, &sentinel) == CILICA_V3_LIMIT && sentinel == 123);
                assert({{api}}_release_handle(runtime, held) == CILICA_V3_OK);
                assert({{api}}_invoke(runtime, 10, &arg, 1, &result) == CILICA_V3_EXECUTION_FAILED && result.fault_code == 13 && result.value.kind == 0);
                assert(calls(runtime) == 2);
                arg = handle(0);
                assert({{api}}_invoke(runtime, 40, &arg, 1, &result) == CILICA_V3_OK && result.value.payload == 0);
                held = create_node(runtime, 2);
                assert({{api}}_destroy(runtime) == CILICA_V3_OK);
                config.handle_limit = 0;
                assert({{api}}_create(3, hash, &config, &runtime) == CILICA_V3_OK);
                arg = integer(2);
                assert({{api}}_invoke(runtime, 10, &arg, 1, &result) == CILICA_V3_LIMIT && calls(runtime) == 0);
                assert({{api}}_destroy(runtime) == CILICA_V3_OK);
                return 0;
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
    public async Task Handle_release_retain_and_managed_invocation_share_one_admission_guard(bool optimized)
    {
        var program = await Program.Value;
        var artifact = EmbeddingObjectModuleEmitter.EmitExceptions(program, Contract(program));
        string api = artifact.SymbolPrefix;
        string source = artifact.Text + $$"""
            #include <cassert>
            #include <thread>
            #include <barrier>
            int main() {
                cilica_v3_config config = CILICA_V3_CONFIG_INIT; config.flags = 1;
                uint64_t runtime = 0;
                assert({{api}}_create(3, CILICA_HANDLES_ABI_V3_CONTRACT_HASH, &config, &runtime) == CILICA_V3_OK);
                for (int i = 0; i < 100; ++i) {
                    cilica_v3_value value{1, 0, 10}; cilica_v3_result created{};
                    assert({{api}}_invoke(runtime, 10, &value, 1, &created) == CILICA_V3_OK);
                    const auto original = created.value.payload; uint64_t clone = 0;
                    std::barrier start(3);
                    std::thread reader([&] {
                        cilica_v3_value arg{2, 0, original}; cilica_v3_result result{}; result.value.payload = 123;
                        start.arrive_and_wait();
                        auto status = {{api}}_invoke(runtime, 20, &arg, 1, &result);
                        assert((status == CILICA_V3_OK && result.value.payload == 10)
                            || ((status == CILICA_V3_BUSY || status == CILICA_V3_INVALID_HANDLE) && result.value.payload == 123));
                    });
                    std::thread releaser([&] {
                        start.arrive_and_wait(); auto status = {{api}}_release_handle(runtime, original);
                        assert(status == CILICA_V3_OK || status == CILICA_V3_BUSY);
                    });
                    std::thread retainer([&] {
                        start.arrive_and_wait(); auto status = {{api}}_retain_handle(runtime, original, &clone);
                        assert((status == CILICA_V3_OK && clone != 0)
                            || ((status == CILICA_V3_BUSY || status == CILICA_V3_INVALID_HANDLE) && clone == 0));
                    });
                    reader.join(); releaser.join(); retainer.join();
                    auto status = {{api}}_release_handle(runtime, original);
                    assert(status == CILICA_V3_OK || status == CILICA_V3_INVALID_HANDLE);
                    if (clone != 0) {
                        cilica_v3_value arg{2, 0, clone}; cilica_v3_result result{};
                        assert({{api}}_invoke(runtime, 20, &arg, 1, &result) == CILICA_V3_OK && result.value.payload == 10);
                        assert({{api}}_release_handle(runtime, clone) == CILICA_V3_OK);
                    }
                }
                assert({{api}}_destroy(runtime) == CILICA_V3_OK);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("embedding-handle-races");
        File.WriteAllText(Path.Combine(directory, "races.cpp"), source);
        string executable = Path.Combine(directory, "races");
        await Check(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", "-pthread", "-Wframe-larger-than=2048",
                optimized ? "-O2" : "-O0", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", "races.cpp", "-o", executable], directory);
        await Check(executable, [], directory);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Ordinary_object_program_exports_scalar_and_reference_results(bool optimized)
    {
        var program = NativeObjectExecutionTests.Translate(nameof(ObjectProbe.AcrossCalls));
        var create = program.Flow.Methods.Single(m => m.Source.Token == typeof(ProbeNode).GetMethod(nameof(ProbeNode.Create))!.MetadataToken);
        var contract = new EmbeddingObjectContract("ordinary", 1, [new(1, "node", create.ReturnType.Class!)], [
            new(1, "create", IrMethodId.From(create.Source), [I32], Node),
            new(2, "run", program.Flow.Entry, [I32, I32], I32)]);
        var artifact = EmbeddingObjectModuleEmitter.EmitProgram(program, contract);
        string api = artifact.SymbolPrefix;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("embedding-ordinary-handles");
        File.WriteAllText(Path.Combine(directory, "ordinary.cpp"), artifact.Text + $$"""
            #include <cassert>
            int main() {
                cilica_v3_config config = CILICA_V3_CONFIG_INIT; config.flags = 1;
                uint64_t runtime = 0, clone = 0;
                assert({{api}}_create(3, CILICA_ORDINARY_ABI_V3_CONTRACT_HASH, &config, &runtime) == 0);
                cilica_v3_value args[2]{ {1, 0, 10}, {1, 0, 5} }; cilica_v3_result result{};
                assert({{api}}_invoke(runtime, 1, args, 1, &result) == 0);
                assert(result.value.kind == 2 && result.value.payload != 0);
                auto token = result.value.payload;
                assert({{api}}_invoke(runtime, 2, args, 2, &result) == 0);
                assert(result.value.kind == 1 && result.value.payload == {{ObjectProbe.AcrossCalls(10, 5)}});
                assert({{api}}_retain_handle(runtime, token, &clone) == 0 && clone != token);
                assert({{api}}_release_handle(runtime, token) == 0);
                assert({{api}}_release_handle(runtime, clone) == 0);
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
