using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class StaticStorageTests
{
    private static readonly (string Name, Func<int, int, int> Oracle)[] Probes = [
        ("References", StaticStorageProbe.References), ("Closed", StaticStorageProbe.Closed), ("Faults", StaticStorageProbe.Faults)];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(StaticStorageProbe), Probes.Select(p => p.Name).ToArray()));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Plain_static_storage_roots_and_closed_identities_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Probes.Select((p, i) => (p.Name, programs[i], p.Oracle)).ToArray());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Runtime_instances_isolate_statics_and_trace_persistent_references(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(StaticStorageProbe), "Persistent"))[0];
        var flow = program.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string symbol = $"cilica_m_{flow.Entry.ContentHash}_{flow.Entry.Token:x8}";
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            [[gnu::noinline]] static void persistent() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                LocationArgument args[2]; args[0].bits = 10; args[1].bits = 2;
                assert({{symbol}}(c, args) == 12 && c.pending == 0);
                assert(collect(c).status == MarkStatus::complete && heap.count == 3);
                assert({{symbol}}(c, args) == 14 && c.pending == 0);
                Context again(heap);
                assert({{symbol}}(again, args) == 16 && again.pending == 0);
                Instance other; Context d(other); args[0].bits = 20; args[1].bits = 3;
                assert({{symbol}}(d, args) == 23 && d.pending == 0);
                for (std::size_t i = 0; i < static_field_count; ++i) heap.statics[i].reference = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                heap.statics[0].reference = reinterpret_cast<Object*>(std::uintptr_t{1});
                assert(collect(c).status == MarkStatus::invalid);
                heap.statics[0].reference = nullptr;
                assert(collect(c).status == MarkStatus::complete);
            }
            [[gnu::noinline]] static void quota() {
                Instance heap; Context c(heap);
                heap.limit = static_field_count * sizeof(StaticSlot) - 1;
                assert(!ensure_static_storage(c) && heap.statics == nullptr && heap.bytes == 0);
                ++heap.limit;
                assert(ensure_static_storage(c) && heap.statics != nullptr && heap.bytes == heap.limit && heap.count == 0);
                assert(collect(c).status == MarkStatus::complete);
                auto* slots = heap.statics;
                assert(ensure_static_storage(c) && heap.statics == slots);
                heap.marking = true;
                assert(!ensure_static_storage(c) && c.pending == 6);
                heap.marking = false;
            }
            int main() { persistent(); quota(); }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("static-runtime");
        string cpp = Path.Combine(directory, "static.cpp"), exe = Path.Combine(directory, "static");
        File.WriteAllText(cpp, source);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
    [Theory]
    [InlineData("readonly")] [InlineData("missing")] [InlineData("wrongtype")] [InlineData("fault")] [InlineData("duplicate")] [InlineData("instancecollision")]
    public async Task Detached_static_stores_preserve_declarations_types_access_and_faults(string mutation)
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        var method = flow.Methods.First(m => m.Blocks.Any(b => b.Statements.Any(s => s is IrStatement.StoreStatic)));
        var block = method.Blocks.First(b => b.Statements.Any(s => s is IrStatement.StoreStatic));
        var store = (IrStatement.StoreStatic)block.Statements.First(s => s is IrStatement.StoreStatic);
        var field = flow.Types.References.Statics.Single(f => f.Id == store.Field);
        var statics = flow.Types.References.Statics;
        if (mutation == "readonly") statics = statics.Replace(field, field with { Attributes = field.Attributes | FieldAttributes.InitOnly });
        if (mutation == "missing") statics = statics.Remove(field);
        if (mutation == "duplicate") statics = statics.Add(field with { Id = field.Id with { Owner = field.Id.Owner with { Token = 0x0200ffff } } });
        if (mutation == "instancecollision") statics = statics.Add(field with { Id = flow.Types.References.Types.SelectMany(t => t.Fields).First().Id });
        if (mutation == "wrongtype") statics = statics.Replace(field, field with { Type = field.Type == IrType.Int32 ? IrType.Reference(flow.Types.References.ObjectBase) : IrType.Int32 });
        var badMethod = method with { Blocks = method.Blocks.Replace(block, block with { Statements = block.Statements.Replace(store, mutation == "fault" ? store with { Exceptional = (IrExceptionTarget)99 } : store) }) };
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with {
            Types = flow.Types with { References = flow.Types.References with { Statics = statics } }, Methods = flow.Methods.Replace(method, badMethod) } } } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(invalid));
    }
}
