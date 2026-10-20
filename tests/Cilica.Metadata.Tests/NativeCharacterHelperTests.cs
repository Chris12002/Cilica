using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeCharacterHelperTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(CharacterConstructionProbe), ["Identity"]));
    private static string Symbol(IrMethod method) => "cilica_m_" + method.Source.ContentHash + "_" + method.Source.Token.ToString("x8", CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Allocating_helpers_keep_roots_borrow_checks_depth_and_fault_origins(bool optimized)
    {
        var scalar = ValueScalarLowering.LowerExceptions((await Programs.Value)[0]);
        var flow = scalar.Flow.Flow.Flow.Flow.Flow;
        var factory = flow.Methods.Single(m => m.Blocks.Any(b => b.Values.Any(v => v.Expression is IrExpression.StringFromCharacter)));
        var borrowed = flow.Methods.Single(m => m.Parameters.Length == 1 && m.Parameters[0].Kind == IrTypeKind.ManagedLocation
            && m.Blocks.Any(b => b.Values.Any(v => v.Expression is IrExpression.Call call && call.Target == IrMethodId.From(factory.Source))));
        int allocationOffset = factory.Blocks.SelectMany(b => b.Values).Single(v => v.Expression is IrExpression.StringFromCharacter).ILOffset;
        var artifact = ObjectCppEmitter.EmitExceptions(scalar);
        Assert.Equal(2, artifact.Text.Split("// Proven character construction:", StringSplitOptions.None).Length - 1);
        string source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            static std::size_t expected_depth = 0, observations = 0;
            static void observe(Context& c) noexcept {
                ++observations;
                assert(valid_roots(c) && c.depth == expected_depth && c.used == 0);
            }
            static void clear_fault(Context& c) {
                c.pending = 0; c.exception = nullptr; c.image = nullptr; c.method = 0; c.il = 0;
            }
            static void reject(Context& c, Location location) {
                LocationArgument arg{}; arg.location = location;
                assert({{Symbol(borrowed)}}(c, &arg) == nullptr && c.pending == 6);
                assert(c.used == 0 && c.depth == 1);
                clear_fault(c);
            }
            [[gnu::noinline]] static void depth(Context& c, std::size_t target, LocationArgument* arg) {
                if (c.depth < target) { Frame frame; RootScope roots(c, frame, nullptr, 0); depth(c, target, arg); return; }
                if (target == 62) {
                    assert({{Symbol(borrowed)}}(c, arg) != nullptr && c.pending == 0);
                    assert(c.depth == target && c.used == 0); return;
                }
                assert({{Symbol(borrowed)}}(c, arg) == nullptr && c.pending == 8);
                assert(c.method == (target == 63 ? {{factory.Source.Token}}u : {{borrowed.Source.Token}}u));
                assert(c.depth == target && c.used == 0);
            }
            int main() {
                Instance heap; Context c(heap); heap.collect_before_allocation = true;
                assert(prepare_exceptions(c) == 0);
                Location saved;
                {
                    Frame frame; RootScope roots(c, frame, nullptr, 0); std::uint32_t unit = 0xd800;
                    BorrowScope borrow(c, frame, &unit, 1, nullptr);
                    assert(borrow.status == LocationStatus::complete);
                    saved = frame_location(c, borrow.frame.token, LocationOrigin::frame_integer, 0, true, LocationNarrow::UInt16).value;
                    LocationArgument arg{}; arg.location = saved; c.observe = observe; expected_depth = 3;
                    auto* text = {{Symbol(borrowed)}}(c, &arg);
                    assert(text && string_code_unit(text, 0) == 0xd800 && observations == 1);
                    assert(c.pending == 0 && c.used == 0 && c.depth == 1 && c.top == &frame);
                    unit = 0x1ffff;
                    text = {{Symbol(borrowed)}}(c, &arg);
                    assert(text && string_code_unit(text, 0) == 0xffff && observations == 2);
                    expected_depth = 2; LocationArgument direct{}; direct.bits = 0x10000;
                    text = {{Symbol(factory)}}(c, &direct);
                    assert(text && string_code_unit(text, 0) == 0 && observations == 3);
                    c.observe = nullptr;
                    auto bad = saved; bad.slot = 1; reject(c, bad);
                    bad = saved; bad.narrow = LocationNarrow::Int16; reject(c, bad);
                    bad = saved; bad.frame += 64; reject(c, bad);
                    { Instance other; Context foreign(other); assert(prepare_exceptions(foreign) == 0);
                      Frame f; RootScope r(foreign, f, nullptr, 0); reject(foreign, saved); }
                    depth(c, 62, &arg);
                    depth(c, 63, &arg); clear_fault(c);
                    depth(c, 64, &arg); clear_fault(c);
                    heap.object_limit = 1; expected_depth = 3; c.observe = observe; observations = 0;
                    assert({{Symbol(borrowed)}}(c, &arg) == nullptr && c.pending == 5);
                    assert(c.method == {{factory.Source.Token}}u && c.il == {{allocationOffset}}u && observations == 1);
                    assert(c.exception != nullptr && c.used == 0 && c.depth == 1);
                    c.observe = nullptr; clear_fault(c); heap.object_limit = 4096;
                    assert({{Symbol(borrowed)}}(c, &arg) != nullptr && c.pending == 0);
                }
                assert(c.top == nullptr && c.depth == 0 && c.used == 0 && c.borrows == nullptr);
                { Frame f; RootScope r(c, f, nullptr, 0); reject(c, saved); }
                // Cold entry retains exception preparation and the ordinary framed path.
                Instance cold_heap; Context cold(cold_heap); LocationArgument arg{}; arg.bits = 65;
                assert({{Symbol(factory)}}(cold, &arg) != nullptr && cold.pending == 0 && cold.peak > 0);
                assert(cold.top == nullptr && cold.depth == 0 && cold.used == 0);
            }
            """;
        using var files = new ResolverFiles();
        string dir = files.DirectoryPath("character-helpers"), cpp = Path.Combine(dir, "probe.cpp"), exe = Path.Combine(dir, "probe");
        File.WriteAllText(cpp, source);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", optimized ? "-O2" : "-O0", "-DCILICA_TYPED_INTERNALS=1",
             "-DCILICA_VERIFY_INTERNAL_REFERENCES=1", "-DCILICA_VERIFY_BORROW_LIFETIMES=1", "-DCILICA_VERIFY_ALLOCATION_ROOTS=1",
             "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", "-Wframe-larger-than=16384",
             "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], dir);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], dir);
        Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr);
    }

    [Fact]
    public async Task Character_helpers_link_across_translation_units()
    {
        var module = ObjectCppEmitter.EmitExceptionModule(ValueScalarLowering.LowerExceptions((await Programs.Value)[0]), 2);
        using var files = new ResolverFiles(); string dir = files.DirectoryPath("character-module");
        foreach (var f in module.Sources.Prepend(module.Header)) File.WriteAllText(Path.Combine(dir, f.Name), f.Text);
        string main = Path.Combine(dir, "main.cpp"), entry = "cilica_m_" + module.Source.ContentHash + "_" + module.Source.Token.ToString("x8", CultureInfo.InvariantCulture);
        File.WriteAllText(main, $$"""
            #include "{{module.Header.Name}}"
            #include <cassert>
            using namespace {{module.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; Context c(heap); heap.collect_before_allocation = true; LocationArgument args[2]{};
                args[0].bits = 0xd800; args[1].bits = 3;
                assert({{entry}}(c, args) == {{CharacterConstructionProbe.Identity(0xd800, 3)}} && c.pending == 0);
                assert(c.depth == 0 && c.used == 0 && c.top == nullptr && c.borrows == nullptr);
            }
            """);
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++"; var objects = new List<string>();
        foreach (string cpp in module.Sources.Select(f => Path.Combine(dir, f.Name)).Append(main))
        {
            string obj = cpp + ".o";
            var build = await NativeProcess.Run(compiler, ["-std=c++20", "-O2", "-DCILICA_TYPED_INTERNALS=1",
                "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", "-fsanitize=address,undefined",
                "-fno-sanitize-recover=all", "-c", cpp, "-o", obj], dir);
            Assert.True(build.ExitCode == 0, build.Stderr); objects.Add(obj);
        }
        string exe = Path.Combine(dir, "probe");
        var link = await NativeProcess.Run(compiler, ["-fsanitize=address,undefined", .. objects, "-o", exe], dir);
        Assert.True(link.ExitCode == 0, link.Stderr);
        var run = await NativeProcess.Run(exe, [], dir);
        Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr);
    }
}
