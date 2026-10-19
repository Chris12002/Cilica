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

public sealed class NativeMembershipIndexTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Marking_index_preserves_checked_membership_cleanup_and_allocation_failure(bool optimized, bool verify)
    {
        var program = NativeObjectExecutionTests.Translate(nameof(ObjectProbe.Run));
        var artifact = ObjectCppEmitter.EmitProgram(program);
        var node = program.Flow.Types.Types.Single(t => t.Source.Name.EndsWith("ProbeNode", StringComparison.Ordinal));
        string type = "t_" + node.Source.ContentHash.ToLowerInvariant() + "_" + node.Source.Token.ToString("x8", CultureInfo.InvariantCulture);
        int next = node.Fields.Select((f, i) => (f, i)).Single(pair => pair.f.Name == "Next").i;
        // Fail only nothrow array allocation during collection. Successful calls
        // use the ordinary matching new[]/delete[] allocation family for ASan.
        string source = """
            #include <new>
            #include <cstddef>
            static bool fail_scratch = false;
            static std::size_t scratch_attempts = 0;
            void* operator new[](std::size_t size, const std::nothrow_t&) noexcept {
                ++scratch_attempts;
                return fail_scratch ? nullptr : ::operator new[](size);
            }
            """ + artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            static Object*& next(Object* value) { return *static_cast<Object**>(field_address(value, {{next}})); }
            static bool report(void* opaque, Object*, bool) noexcept {
                auto& c = *static_cast<Context*>(opaque);
                assert(c.instance.membership_size == 0 && c.instance.marking);
                assert(mark_reachable(c).status == MarkStatus::busy);
                return false;
            }
            int main() {
                Instance heap, foreign; Context c(heap), other(foreign);
                Object* roots[] = {nullptr, nullptr}; Frame frame; RootScope scope(c, frame, roots, 2);
                auto* alien = allocate<{{type}}>(other, {{type}}_descriptor);
                auto* bogus = reinterpret_cast<Object*>(std::uintptr_t{1});
                assert(mark_reachable(c).status == MarkStatus::complete && heap.membership_capacity == 0);
                for (int i = 0; i < 32; ++i) {
                    auto* value = allocate<{{type}}>(c, {{type}}_descriptor); assert(value);
                    next(value) = roots[0]; roots[0] = value;
                }
                const auto bytes = heap.bytes;
                fail_scratch = true;
                auto attempts = scratch_attempts;
                assert(collect(c).status == MarkStatus::complete && heap.count == 32);
                assert(scratch_attempts == attempts + 1 && heap.membership_capacity == 0);
                // Failed scratch allocation cannot turn an invalid graph into a sweep.
                roots[1] = bogus;
                assert(collect(c).status == MarkStatus::invalid && heap.count == 32 && heap.bytes == bytes);
                roots[1] = nullptr; fail_scratch = false;
                {
                    MarkState state(heap, 262144);
                    assert(mark_graph(c, {}, state).status == MarkStatus::complete);
                    assert(heap.membership_size >= 64);
                    for (auto* value = heap.head; value; value = value->next) assert(heap.owns(value));
                    assert(heap.owns(nullptr) && !heap.owns(bogus) && !heap.owns(alien));
                    assert(!heap.owns(reinterpret_cast<Object*>(reinterpret_cast<std::uintptr_t>(roots[0]) + 1)));
                }
                assert(!heap.marking && heap.membership_size == 0);
                auto** buffer = heap.membership_slots; attempts = scratch_attempts;
                fail_scratch = true;
                assert(collect(c).status == MarkStatus::complete);
                assert(heap.membership_slots == buffer && scratch_attempts == attempts);
                fail_scratch = false;
                for (auto* bad : {bogus, alien}) {
                    roots[1] = bad;
                    assert(collect(c).status == MarkStatus::invalid && heap.count == 32);
                    assert(heap.membership_size == 0 && !heap.marking);
                    roots[1] = nullptr;
                    auto* saved = next(roots[0]); next(roots[0]) = bad;
                    assert(collect(c).status == MarkStatus::invalid && heap.count == 32);
                    next(roots[0]) = saved;
                }
                assert(collect(c, {4096, 0}).status == MarkStatus::limit && heap.membership_size == 0);
                assert(mark_reachable(c, {}, report, &c).status == MarkStatus::stopped && heap.membership_size == 0);
                for (int i = 0; i < 225; ++i) {
                    auto* value = allocate<{{type}}>(c, {{type}}_descriptor); assert(value);
                    next(value) = roots[0]; roots[0] = value;
                }
                fail_scratch = true; attempts = scratch_attempts;
                assert(collect(c).status == MarkStatus::complete && heap.count == 257);
                assert(scratch_attempts == attempts + 1 && heap.membership_slots == buffer);
                fail_scratch = false;
                assert(collect(c).status == MarkStatus::complete && heap.membership_capacity >= 514);
                const auto retired = reinterpret_cast<std::uintptr_t>(roots[0]);
                roots[0] = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                assert(!heap.owns(reinterpret_cast<Object*>(retired)) && heap.membership_size == 0);
                // Rebuild after reclamation; stale scratch entries cannot admit roots.
                roots[1] = bogus;
                assert(collect(c).status == MarkStatus::invalid && heap.count == 0);
                roots[1] = nullptr;
                roots[0] = allocate<{{type}}>(c, {{type}}_descriptor); assert(roots[0]);
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                assert(heap.membership_size == 0 && heap.owns(roots[0]));
                assert(retain_handle(heap, bogus).status == HandleStatus::invalid);
                assert(retain_handle(heap, alien).status == HandleStatus::invalid);
            }
            """;
        using var files = new ResolverFiles();
        string directory = files.DirectoryPath("membership-index");
        string cpp = Path.Combine(directory, "probe.cpp"), executable = Path.Combine(directory, "probe");
        File.WriteAllText(cpp, source, new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", optimized ? "-O2" : "-O0", "-DCILICA_VERIFY_INTERNAL_REFERENCES=" + (verify ? "1" : "0"),
             "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", "-fsanitize=address,undefined",
             "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stdout + build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr);
        Assert.Empty(run.Stderr);
    }
}
