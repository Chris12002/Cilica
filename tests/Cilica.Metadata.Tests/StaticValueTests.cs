using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class StaticValueTests
{
    private static readonly string[] Names = ["Numeric", "References", "Closed", "Arrays", "EmptyValues", "Initialized"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(StaticValueProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    public async Task Static_value_copies_and_initializers_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(StaticValueProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Former_static_inline_boundary_now_matches_dotnet(bool optimized)
    {
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(NullableProbe), "StaticInline");
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("StaticInline", programs[0], NullableProbe.StaticInline)]);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Failed_static_value_initializer_matches_dotnet(bool optimized)
    {
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(StaticValueProbe), "Failure");
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("FailedInitializer", programs[0], StaticValueProbe.Failure)], persistentRoots: true);
    }
    [Theory]
    [InlineData("missing")] [InlineData("type")] [InlineData("component")] [InlineData("flags")] [InlineData("owner")]
    public async Task Detached_scalar_static_leaves_must_match_the_logical_value_plan(string mutation)
    {
        var scalar = ValueScalarLowering.LowerExceptions((await Programs.Value)[1]);
        var boxes = scalar.Flow.Flow; var objects = boxes.Flow.Flow; var flow = objects.Flow;
        var field = flow.Types.Statics.First(f => f.Type.Kind == IrTypeKind.Reference);
        var changed = mutation switch {
            "type" => field with { Type = IrType.Int64 },
            "component" => field with { Id = field.Id with { Component = field.Id.Component + 1 } },
            "flags" => field with { Attributes = field.Attributes | System.Reflection.FieldAttributes.InitOnly },
            "owner" => field with { Id = field.Id with { Owner = field.Id.Owner with { Token = 0x0200ffff } } },
            _ => field
        };
        var statics = mutation == "missing" ? flow.Types.Statics.Remove(field) : flow.Types.Statics.Replace(field, changed);
        var invalid = scalar with { Flow = scalar.Flow with { Flow = boxes with { Flow = boxes.Flow with {
            Flow = objects with { Flow = flow with { Types = flow.Types with { Statics = statics } } } } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalid));
    }
    [Theory]
    [InlineData("unknown")] [InlineData("expanded")] [InlineData("readonly")]
    public async Task Detached_logical_static_values_validate_layout_and_declaration(string mutation)
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        var field = flow.Types.References.Statics.First(f => f.Type.Kind == IrTypeKind.Value);
        var changed = mutation switch {
            "unknown" => field with { Type = IrType.Value(field.Type.Class! with { Token = 0x0200ffff }) },
            "expanded" => field with { Id = field.Id with { Component = 0 } },
            _ => field with { Attributes = field.Attributes | System.Reflection.FieldAttributes.InitOnly }
        };
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with {
            Types = flow.Types with { References = flow.Types.References with { Statics = flow.Types.References.Statics.Replace(field, changed) } } } } } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(invalid));
    }
    [Fact]
    public void Static_value_addresses_still_require_a_borrow_carrier()
    {
        var result = IntegerObjectTranslationTests.Translate("UnsupportedAddress", fixture: typeof(StaticValueProbe));
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("ldsflda", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Static_reference_leaves_survive_collection_and_release_on_overwrite(bool optimized)
    {
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(StaticValueProbe), "Persistent", "Failure");
        var source = new System.Text.StringBuilder();
        for (int index = 0; index < programs.Length; ++index)
        {
            var program = programs[index]; var flow = program.Flow.Flow.Flow;
            var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
            string symbol = $"cilica_m_{flow.Entry.ContentHash}_{flow.Entry.Token:x8}";
            string checks = index == 0 ? $$"""
                args[0].bits = 10; args[1].bits = 0;
                assert({{symbol}}(c, args) == 10 && c.pending == 0);
                assert(collect(c).status == MarkStatus::complete && heap.count == 3);
                Instance other; other.collect_before_allocation = true; Context d(other);
                args[0].bits = 20;
                assert({{symbol}}(d, args) == 20 && d.pending == 0);
                args[0].bits = 11; args[1].bits = 1;
                assert({{symbol}}(c, args) == 11 && c.pending == 0);
                assert(collect(c).status == MarkStatus::complete && heap.count == 2);
                args[1].bits = 2;
                assert({{symbol}}(c, args) == 0 && c.pending == 0);
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                assert(collect(d).status == MarkStatus::complete && other.count == 3);
                """ : $$"""
                assert({{symbol}}(c, args) == 31 && c.pending == 0);
                assert(collect(c).status == MarkStatus::complete);
                const auto retained = heap.count;
                assert({{symbol}}(c, args) == 31 && c.pending == 0);
                assert(collect(c).status == MarkStatus::complete && heap.count == retained);
                std::size_t occupied = 0;
                for (std::size_t i = 0; i < static_field_count; ++i) {
                    if (heap.statics[i].reference != nullptr) ++occupied;
                    heap.statics[i].reference = nullptr;
                }
                assert(occupied == 2);
                assert(collect(c).status == MarkStatus::complete && heap.count == retained - 2);
                """;
            source.Append(artifact.Text).Append(System.Globalization.CultureInfo.InvariantCulture, $$"""
                #include <cassert>
                [[gnu::noinline]] static void check{{index}}() {
                    using namespace {{artifact.EntryPoint[..^4]}}_closure;
                    Instance heap; heap.collect_before_allocation = true; Context c(heap);
                    LocationArgument args[2]; args[0].bits = 0; args[1].bits = 0;
                    {{checks}}
                    assert(c.used == 0 && c.top == nullptr && c.borrows == nullptr);
                }
                """);
        }
        source.Append("int main() { check0(); check1(); }");
        using var files = new ResolverFiles(); string dir = files.DirectoryPath("static-values"), cpp = Path.Combine(dir, "test.cpp"), exe = Path.Combine(dir, "test");
        File.WriteAllText(cpp, source.ToString());
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], dir);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], dir); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
