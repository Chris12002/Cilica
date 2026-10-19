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

public sealed class NativeInternalReferenceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Internal_membership_is_diagnostic_but_handles_and_collection_remain_checked(bool optimized, bool verify)
    {
        var program = NativeObjectExecutionTests.Translate(nameof(ObjectProbe.Run));
        var artifact = ObjectCppEmitter.EmitProgram(program);
        var node = program.Flow.Types.Types.Single(t => t.Source.Name.EndsWith("ProbeNode", StringComparison.Ordinal));
        string type = "t_" + node.Source.ContentHash.ToLowerInvariant() + "_" + node.Source.Token.ToString("x8", CultureInfo.InvariantCulture);
        var method = program.Flow.Methods.Single(m => m.Source.Name.Contains("::Change", StringComparison.Ordinal));
        string change = "cilica_m_" + method.Source.ContentHash.ToLowerInvariant() + "_" + method.Source.Token.ToString("x8", CultureInfo.InvariantCulture);
        int next = node.Fields.Select((f, i) => (f, i)).Single(pair => pair.f.Name == "Next").i;
        string source = artifact.Text + NativeObjectAdapters.Emit(program, artifact) + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap, foreign; Context c(heap), other(foreign);
                Object* roots[] = {nullptr, nullptr}; Frame frame; RootScope scope(c, frame, roots, 2);
                roots[0] = allocate<{{type}}>(c, {{type}}_descriptor);
                heap.collect_before_allocation = true;
                roots[1] = allocate<{{type}}>(c, {{type}}_descriptor);
                assert(roots[0] && roots[1] && c.pending == 0);
                assert({{change}}(c, roots[0], 7) == 7 && c.pending == 0);
                auto location = heap_location(c, roots[0], &{{type}}_descriptor, {{next}});
                assert(location.status == LocationStatus::complete);
                assert(store_location_reference(c, location.value, roots[1]) == LocationStatus::complete);
                roots[1] = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 2);
                Object* child = nullptr;
                assert(load_location_reference(c, location.value, child) == LocationStatus::complete && heap.owns(child));
                Object* alien = allocate<{{type}}>(other, {{type}}_descriptor);
                auto* forged = reinterpret_cast<Object*>(std::uintptr_t{1});
                // Boundary validation compares membership before reading any header,
                // including when ordinary execution omits membership scans.
                for (Object* bad : {alien, forged}) {
                    assert(retain_handle(heap, bad).status == HandleStatus::invalid);
                    assert(!traceable_reference(heap, bad));
                    roots[1] = bad;
                    assert(collect(c).status == MarkStatus::invalid && heap.count == 2);
                    roots[1] = nullptr;
                }
                const auto retained = retain_handle(heap, roots[0]);
                assert(retained.status == HandleStatus::complete);
                assert(resolve_handle(foreign, retained.handle).status == HandleStatus::invalid);
                // A corrupt private slot cannot bypass checked handle resolution.
                heap.handles[0].value = forged;
                assert(resolve_handle(heap, retained.handle).status == HandleStatus::invalid);
                heap.handles[0].value = roots[0];
                assert(release_handle(heap, retained.handle) == HandleStatus::complete);
                assert(resolve_handle(heap, retained.handle).status == HandleStatus::invalid);
                // Temporarily detach readable storage to distinguish an actual
                // membership scan from a header check. No allocation/GC while detached.
                Object* saved = heap.head; heap.head = nullptr;
                c.pending = 0;
                const auto changed = {{change}}(c, roots[0], 1);
                if (CILICA_VERIFY_INTERNAL_REFERENCES) {
                    assert(changed == 0 && c.pending == 6);
                    assert(heap_location(c, roots[0], &{{type}}_descriptor, {{next}}).status == LocationStatus::invalid);
                    c.pending = 0; (void){{change}}(c, forged, 1); assert(c.pending == 6);
                } else {
                    assert(changed == 8 && c.pending == 0);
                    assert(heap_location(c, roots[0], &{{type}}_descriptor, {{next}}).status == LocationStatus::complete);
                }
                heap.head = saved; c.pending = 0;
                assert(collect(c).status == MarkStatus::complete && heap.count == 2);
                (void){{change}}(c, nullptr, 1); assert(c.pending == 4);
            }
            """;
        using var files = new ResolverFiles();
        string directory = files.DirectoryPath("internal-reference-verification");
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
