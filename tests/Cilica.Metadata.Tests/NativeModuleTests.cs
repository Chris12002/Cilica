using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class NativeModuleTests
{
    [Theory] [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task Shared_runtime_identity_and_managed_calls_survive_separate_translation_units(bool optimized, bool integers)
    {
        var translated = IntegerObjectTranslationTests.Translate(integers ? "Run" : "Legacy", enabled: integers, fixture: typeof(NativeModuleProbe));
        Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
        var scalar = ValueScalarLowering.LowerExceptions(translated.Program!);
        // Every method lives in a different source file, forcing calls, delegates,
        // field descriptors, initialization, borrows and cleanup across boundaries.
        var module = ObjectCppEmitter.EmitExceptionModule(scalar, methodsPerUnit: 1);
        int count = scalar.Flow.Flow.Flow.Flow.Flow.Methods.Length;
        Assert.Equal(count + 1, module.Sources.Length);
        using var files = new ResolverFiles(); string dir = files.DirectoryPath("native-module");
        foreach (var file in module.Sources.Prepend(module.Header))
            await File.WriteAllTextAsync(Path.Combine(dir, file.Name), file.Text, TestContext.Current.CancellationToken);
        string scope = module.EntryPoint[..^4] + "_closure";
        string symbol = "cilica_m_" + module.Source.ContentHash + "_" + module.Source.Token.ToString("x8", CultureInfo.InvariantCulture);
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $$"""
            #include "{{module.Header.Name}}"
            #include <cassert>
            #include <cstdio>
            using namespace {{scope}};
            extern const Descriptor* peer_type();
            extern std::uint64_t peer_handle_token();
            extern std::uint64_t peer_borrow_token();
            [[gnu::noinline]] static void check(int a, int b, int expected) {
                for (bool forced : {false, true}) {
                    Instance heap; heap.collect_before_allocation = forced; Context c(heap);
                    LocationArgument args[2]{}; args[0].bits=std::bit_cast<std::uint32_t>(a); args[1].bits=std::bit_cast<std::uint32_t>(b);
                    for (int repeat=0;repeat<3;++repeat) {
                        int actual={{symbol}}(c,args);
                        if(c.pending!=0 || actual!=expected) std::fprintf(stderr,"%d,%d status %u got %d expected %d\n",a,b,c.pending,actual,expected);
                        assert(c.pending==0 && actual==expected && c.exception==nullptr);
                        assert(c.top==nullptr && c.depth==0 && c.used==0 && c.borrows==nullptr);
                        assert(collect(c).status==MarkStatus::complete);
                    }
                    auto peak=c.peak;
                    Context exact(heap,peak); assert({{symbol}}(exact,args)==expected && exact.pending==0 && exact.used==0);
                    Context short_frame(heap,peak-1); (void){{symbol}}(short_frame,args);
                    assert(short_frame.pending==9 && short_frame.top==nullptr && short_frame.borrows==nullptr && short_frame.used==0);
                }
            }
            int main() {
                assert(peer_type()==runtime_exception(4));
                auto h=issue_handle_token(); assert(peer_handle_token()==h+1 && issue_handle_token()==h+2);
                auto b=issue_borrow_token(); assert(peer_borrow_token()==b+1 && issue_borrow_token()==b+2);
            """);
        foreach (int a in new[] { 0, 1, -1, 127, 32768, int.MinValue, int.MaxValue })
            foreach (int b in new[] { 0, 1, 2, 3, -1, int.MinValue, int.MaxValue })
                text.Append(CultureInfo.InvariantCulture, $"check({a},{b},{(integers ? NativeModuleProbe.Run(a,b) : NativeModuleProbe.Legacy(a,b))});\n");
        text.Append("}\n");
        string main = Path.Combine(dir, "main.cpp"), peer = Path.Combine(dir, "peer.cpp"), exe = Path.Combine(dir, "module");
        await File.WriteAllTextAsync(main, text.ToString(), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(peer, $$"""
            #include "{{module.Header.Name}}"
            using namespace {{scope}};
            const Descriptor* peer_type() { return runtime_exception(4); }
            std::uint64_t peer_handle_token() { return issue_handle_token(); }
            std::uint64_t peer_borrow_token() { return issue_borrow_token(); }
            """, TestContext.Current.CancellationToken);
        var objects = new List<string>();
        foreach (string source in module.Sources.Select(s => Path.Combine(dir, s.Name)).Append(main).Append(peer))
        {
            string obj = source + ".o"; NativeEvidence.Record(source, optimized);
            var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
                ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                 "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", "-c", source, "-o", obj], dir);
            Assert.True(build.ExitCode == 0, build.Stderr); objects.Add(obj);
        }
        var link = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-fsanitize=address,undefined", .. objects, "-o", exe], dir);
        Assert.True(link.ExitCode == 0, link.Stderr); NativeEvidence.Record(main, optimized, exe);
        var run = await NativeProcess.Run(exe, [], dir); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
    [Fact]
    public void Partitioning_keeps_detached_program_validation()
    {
        var translated = IntegerObjectTranslationTests.Translate("Run", fixture: typeof(NativeModuleProbe));
        Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
        var scalar = ValueScalarLowering.LowerExceptions(translated.Program!);
        var objects = scalar.Flow.Flow.Flow.Flow;
        var invalid = scalar with { Flow = scalar.Flow with { Flow = scalar.Flow.Flow with {
            Flow = new(objects with { Flow = objects.Flow with { Methods = [] } }) } } };
        Assert.Throws<ArgumentException>(() => ObjectCppEmitter.EmitExceptionModule(invalid));
    }
    [Theory] [InlineData(0)] [InlineData(257)]
    public void Invalid_native_partition_limits_are_rejected_before_emission(int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ObjectCppEmitter.EmitExceptionModule(null!, size));
    }
}
