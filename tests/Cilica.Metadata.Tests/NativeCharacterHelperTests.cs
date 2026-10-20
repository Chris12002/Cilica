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
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(true, 0)] [InlineData(true, 1)]
    public async Task Local_call_sites_preserve_utf16_and_identity_in_dispatcher_and_direct_execution(bool optimized, int verify)
    {
        int[] units = [int.MinValue, -1, 0, 1, 0xd7ff, 0xd800, 0xdbff, 0xdc00, 0xdfff, 0xffff, 0x10000, int.MaxValue];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("Identity", (await Programs.Value)[0], CharacterConstructionProbe.Identity)],
            units.Select(a => (a, 0)).ToArray(),
            nativeFlags: ["-DCILICA_TYPED_INTERNALS=1", "-DCILICA_VERIFY_BORROW_LIFETIMES=" + verify, "-Wframe-larger-than=16384"]);
    }

    [Theory]
    [InlineData("reuse")] [InlineData("escape")] [InlineData("intervening")] [InlineData("parameter")]
    public async Task Local_read_proof_rejects_reuse_escape_intervening_work_and_parameter_addresses(string mutation)
    {
        var scalar = ValueScalarLowering.LowerExceptions((await Programs.Value)[0]);
        var flow = scalar.Flow.Flow.Flow.Flow.Flow;
        var method = flow.Methods.Single(m => m.Source.Token == typeof(CharacterConstructionProbe).GetMethod("Identity")!.MetadataToken
            && m.Source.ContentHash == flow.Entry.ContentHash);
        var block = method.Blocks.Single(b => b.Statements.Any(s => s is IrStatement.Define { Value.Expression: IrExpression.AddressLocal }));
        int index = Enumerable.Range(0, block.Statements.Length).Single(i =>
            block.Statements[i] is IrStatement.Define { Value.Expression: IrExpression.AddressLocal });
        var address = ((IrStatement.Define)block.Statements[index]).Value;
        var call = ((IrStatement.Define)block.Statements[index + 1]).Value;
        Assert.IsType<IrExpression.Call>(call.Expression);
        Assert.Contains("_local_character(context,", ObjectCppEmitter.EmitExceptions(scalar).Text, StringComparison.Ordinal);
        var statements = block.Statements;
        var locals = method.Locals;
        if (mutation == "reuse")
            statements = statements.Insert(index + 2, new IrStatement.InitializeLocation(call.ILOffset, address.Id));
        else if (mutation == "escape")
        {
            statements = statements.Insert(index + 2, new IrStatement.StoreLocal(call.ILOffset, locals.Length, address.Id));
            locals = locals.Add(address.Type);
        }
        else if (mutation == "intervening")
        {
            int earlier = block.Values.First(v => v.Id < address.Id && v.Type == IrType.Int32).Id;
            statements = statements.Insert(index + 1, new IrStatement.StoreLocal(call.ILOffset, ((IrExpression.AddressLocal)address.Expression).Index, earlier));
        }
        else
            statements = statements.SetItem(index, ((IrStatement.Define)statements[index]) with {
                Value = address with { Expression = new IrExpression.AddressParameter(0) } });
        var changed = method with { Locals = locals, Blocks = method.Blocks.Replace(block, block with { Statements = statements }) };
        var objects = scalar.Flow.Flow.Flow.Flow with { Flow = flow with { Methods = flow.Methods.Replace(method, changed) } };
        var locations = scalar.Flow.Flow.Flow with { Flow = objects };
        var boxes = scalar.Flow.Flow with { Flow = locations };
        var delegates = scalar.Flow with { Flow = boxes };
        var artifact = ObjectCppEmitter.EmitExceptions(scalar with { Flow = delegates });
        Assert.DoesNotContain("_local_character(context,", artifact.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task Proven_local_values_keep_gc_frames_diagnostics_depth_and_allocation_faults(bool optimized, bool verify)
    {
        var scalar = ValueScalarLowering.LowerExceptions((await Programs.Value)[0]);
        var flow = scalar.Flow.Flow.Flow.Flow.Flow;
        var factory = flow.Methods.Single(m => m.Blocks.Any(b => b.Values.Any(v => v.Expression is IrExpression.StringFromCharacter)));
        var borrowed = flow.Methods.Single(m => m.Parameters.Length == 1 && m.Parameters[0].Kind == IrTypeKind.ManagedLocation
            && m.Blocks.Any(b => b.Values.Any(v => v.Expression is IrExpression.Call call && call.Target == IrMethodId.From(factory.Source))));
        int offset = factory.Blocks.SelectMany(b => b.Values).Single(v => v.Expression is IrExpression.StringFromCharacter).ILOffset;
        var artifact = ObjectCppEmitter.EmitExceptions(scalar);
        string source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            static unsigned observations = 0;
            static void observe(Context& c) noexcept {
                ++observations; assert(c.depth == 3 && c.used == 0 && valid_roots(c));
            }
            static void clear_fault(Context& c) {
                c.pending = 0; c.exception = nullptr; c.image = nullptr; c.method = 0; c.il = 0;
            }
            [[gnu::noinline]] static void depth(Context& c, std::size_t target, const Location& location) {
                if (c.depth < target) { Frame f; RootScope r(c,f,nullptr,0); depth(c,target,location); return; }
                auto* result = {{Symbol(borrowed)}}_local_character(c,65,location);
                if (target == 62) assert(result && c.pending == 0);
                else {
                    assert(result == nullptr && c.pending == 8);
                    assert(c.method == (target == 63 ? {{factory.Source.Token}}u : {{borrowed.Source.Token}}u));
                }
                assert(c.depth == target && c.used == 0);
            }
            int main() {
                Instance heap; Context c(heap); heap.collect_before_allocation = true;
                assert(prepare_exceptions(c) == 0);
                Frame f; Object* retained[2]{}; RootScope roots(c,f,retained,2); std::uint32_t unit=65;
                ReferencePredicate accepts[2]{known_type,known_type};
                BorrowScope borrow(c,f,&unit,1,accepts); assert(borrow.status == LocationStatus::complete);
                auto location=frame_location(c,borrow.frame.token,LocationOrigin::frame_integer,0,true,LocationNarrow::UInt16).value;
                c.observe=observe;
                for (std::uint32_t bits : {0u,1u,0xd7ffu,0xd800u,0xdc00u,0xffffu,0x10000u,0xffffffffu}) {
                    retained[0]={{Symbol(borrowed)}}_local_character(c,bits,location);
                    const auto expected=CILICA_VERIFY_BORROW_LIFETIMES ? unit : bits & 0xffffu;
                    assert(retained[0] && string_length(retained[0]) == 1 && string_code_unit(retained[0],0) == expected);
                    retained[1]={{Symbol(borrowed)}}_local_character(c,bits,location);
                    assert(retained[1] && retained[1] != retained[0]);
                    assert(string_code_unit(retained[0],0) == expected && c.pending == 0 && c.used == 0);
                }
                assert(observations == 16); retained[0]=nullptr; retained[1]=nullptr; c.observe=nullptr;
                heap.marking=true;
                assert(!direct_character_local(c));
                assert({{Symbol(borrowed)}}_local_character(c,65,location) == nullptr && c.pending == 6);
                heap.marking=false; clear_fault(c);
                c.pending=2; c.method=123; c.il=456;
                assert({{Symbol(borrowed)}}_local_character(c,65,location) == nullptr && c.pending == 2 && c.method == 123 && c.il == 456);
                clear_fault(c);
                depth(c,62,location); depth(c,63,location); clear_fault(c); depth(c,64,location); clear_fault(c);
                heap.object_limit=1; observations=0; c.observe=observe;
                assert({{Symbol(borrowed)}}_local_character(c,65,location) == nullptr && c.pending == 5);
                assert(c.method == {{factory.Source.Token}}u && c.il == {{offset}}u && c.exception != nullptr && observations == 1);
                c.observe=nullptr; clear_fault(c); heap.object_limit=4096;
                assert({{Symbol(borrowed)}}_local_character(c,65,location) != nullptr && c.pending == 0);
                assert(c.depth == 1 && c.top == &f && c.used == 0);
            }
            """;
        using var files = new ResolverFiles();
        string dir=files.DirectoryPath("local-character"),cpp=Path.Combine(dir,"probe.cpp"),exe=Path.Combine(dir,"probe");
        File.WriteAllText(cpp,source);
        var build=await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX")??"clang++",
            ["-std=c++20",optimized?"-O2":"-O0","-DCILICA_TYPED_INTERNALS=1","-DCILICA_VERIFY_BORROW_LIFETIMES="+(verify?"1":"0"),
             "-DCILICA_VERIFY_INTERNAL_REFERENCES=1","-DCILICA_VERIFY_ALLOCATION_ROOTS=1","-Wframe-larger-than=16384",
             "-fno-exceptions","-fno-rtti","-Wall","-Wextra","-Werror","-fsanitize=address,undefined","-fno-sanitize-recover=all",cpp,"-o",exe],dir);
        Assert.True(build.ExitCode==0,build.Stderr);
        var run=await NativeProcess.Run(exe,[],dir);Assert.True(run.ExitCode==0,run.Stderr);Assert.Empty(run.Stderr);
    }

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
