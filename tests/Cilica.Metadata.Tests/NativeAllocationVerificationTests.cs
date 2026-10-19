using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeAllocationVerificationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Allocation_verification_is_optional_but_observers_and_collection_remain_checked(bool optimized, bool verify)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(TracingProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var translated = ObjectTranslator.Translate(resolver, input,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(TracingProbe).GetMethod(nameof(TracingProbe.Run))!.MetadataToken), library);
        Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
        var program = translated.Program!;
        var artifact = ObjectCppEmitter.EmitProgram(program);
        string Type(ObjectTypeId id) => "t_" + id.ContentHash + "_" + id.Token.ToString("x8", CultureInfo.InvariantCulture)
            + (id.ArrayElement is null ? "" : "_vector_i4");
        string node = Type(ObjectTypeId.From(program.Flow.Types.Types.Single(t => t.Source.Token == typeof(TraceNode).MetadataToken && t.Source.ContentHash == input.ContentHash).Source));
        string array = Type(program.Flow.Types.Arrays.Single(a => a.ArrayElement!.Kind == ObjectArrayElementKind.Int32));
        string text = Type(program.Flow.Types.String!);
        string source = artifact.Text + $$"""

            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            static int observations = 0;
            static void observe(Context& c) noexcept { ++observations; assert(valid_roots(c)); }
            static bool allocate_kind(Context& c, int kind) {
                switch (kind) {
                case 0: return allocate<{{node}}>(c, {{node}}_descriptor) != nullptr;
                case 1: return allocate_array(c, {{array}}_descriptor, 2) != nullptr;
                case 2: return allocate_string(c, {{text}}_descriptor, nullptr, 0, nullptr, 0) != nullptr;
                case 3: return ensure_static_storage(c);
                default: return ensure_initialization_storage(c);
                }
            }
            int main() {
                for (int kind = 0; kind < 5; ++kind) {
                    // Deliberately corrupt a private root. Normal allocation does
                    // not inspect it; verification/observation must fail before delivery.
                    for (bool observer : {false, true}) {
                        Instance heap; Context c(heap);
                        Object* roots[] = {reinterpret_cast<Object*>(std::uintptr_t{1})};
                        Frame frame; RootScope scope(c, frame, roots, 1);
                        observations = 0; if (observer) c.observe = observe;
                        const bool rejected = CILICA_VERIFY_ALLOCATION_ROOTS || observer;
                        assert(allocate_kind(c, kind) == !rejected);
                        assert(c.pending == (rejected ? 6u : 0u) && observations == 0);
                        if (rejected) assert(heap.count == 0 && heap.bytes == 0 && heap.statics == nullptr && heap.initializers == nullptr);
                        roots[0] = nullptr; // Restore the private fault injection.
                    }
                    // Observers still run once at a valid published safepoint.
                    {
                        Instance heap; Context c(heap); Object* roots[] = {nullptr};
                        Frame frame; RootScope scope(c, frame, roots, 1);
                        c.observe = observe; observations = 0;
                        assert(allocate_kind(c, kind) && observations == 1 && c.pending == 0);
                    }
                    // Collection still rejects invalid roots without reclaiming
                    // anything, in both modes, on stress and on allocation pressure.
                    for (bool stress : {false, true}) {
                        Instance foreign; Context other(foreign);
                        Object* alien = allocate<{{node}}>(other, {{node}}_descriptor);
                        Instance heap; Context c(heap);
                        Object* garbage = allocate<{{node}}>(c, {{node}}_descriptor);
                        assert(alien && garbage);
                        const auto bytes = heap.bytes, count = heap.count;
                        Object* roots[] = {alien}; Frame frame; RootScope scope(c, frame, roots, 1);
                        heap.collect_before_allocation = stress;
                        if (!stress) heap.object_limit = count;
                        assert(!allocate_kind(c, kind) && c.pending == 6u);
                        assert(heap.bytes == bytes && heap.count == count && heap.owns(garbage));
                        assert(heap.statics == nullptr && heap.initializers == nullptr);
                        roots[0] = nullptr;
                    }
                }
                // A published object survives repeated stress collections in
                // ordinary and verification builds, while unrooted garbage dies.
                Instance heap; Context c(heap); Object* roots[] = {nullptr};
                Frame frame; RootScope scope(c, frame, roots, 1);
                roots[0] = allocate<{{node}}>(c, {{node}}_descriptor);
                Object* live = roots[0]; assert(live);
                heap.collect_before_allocation = true;
                for (int i = 0; i < 8; ++i) {
                    assert(allocate_array(c, {{array}}_descriptor, 2));
                    assert(c.pending == 0 && roots[0] == live && heap.owns(live) && heap.count == 2);
                }
                assert(collect(c).status == MarkStatus::complete && heap.count == 1 && heap.owns(live));
            }
            """;
        using var files = new ResolverFiles();
        string directory = files.DirectoryPath("allocation-verification");
        string cpp = Path.Combine(directory, "probe.cpp"), executable = Path.Combine(directory, "probe");
        File.WriteAllText(cpp, source, new UTF8Encoding(false));
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        var build = await NativeProcess.Run(compiler,
            ["-std=c++20", optimized ? "-O2" : "-O0", "-DCILICA_VERIFY_ALLOCATION_ROOTS=" + (verify ? "1" : "0"),
                "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", "-fsanitize=address,undefined",
                "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stdout + build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr);
        Assert.Empty(run.Stderr);
    }
}
