using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class SegmentedCastMessageTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Many_cast_targets_fit_without_a_cartesian_text_table(bool optimized)
    {
        var result = Many(64, 180);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(result.Program!));
        Assert.True(artifact.Text.Length < 1_500_000, $"Unexpected native source growth: {artifact.Text.Length}");
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("Many", result.Program!, (a, b) => unchecked(a + b))]);
    }
    [Fact]
    public void Independent_text_parts_still_obey_the_total_utf16_budget()
    {
        var result = Many(64, 2600);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Cast exception message resources exceed the UTF-16 budget", StringComparison.Ordinal));
    }
    private static ManagedExceptionTranslationResult Many(int count, int nameLength)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("ManyCastTargets", new Version(1, 0, 0, 0)));
        var obj = b.TypeReference("System", "Object", b.Reference(library.Identity));
        var types = new List<TypeDefinitionHandle>();
        for (int i = 0; i < count; i++) types.Add(b.Type("Test", new string('T', nameLength) + i, baseType: obj));
        b.Type("Test", "Root");
        var root = b.Method("Run", [0, 2, 8, 8, 8], emit: il => {
            foreach (var type in types) { il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Castclass); il.Token(type); il.OpCode(ILOpCode.Pop); }
            il.LoadArgument(0); il.LoadArgument(1); il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Ret);
        });
        var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        return ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Lazy_cast_text_is_exact_cached_and_transactional(bool optimized)
    {
        var translated = IntegerObjectTranslationTests.Translate("Message", fixture: typeof(SegmentedCastProbe));
        Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
        var program = translated.Program!; var methods = program.Flow.Flow.Flow.Methods;
        var create = methods.Single(m => m.Source.Name.EndsWith("SegmentedCastProbe::Create", StringComparison.Ordinal));
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string symbol = $"cilica_m_{create.Source.ContentHash}_{create.Source.Token:x8}";
        string checks = string.Join("\n", new[] { (7, 1), (-3, 1) }.Select(pair => {
            string expected = SegmentedCastProbe.Create(pair.Item1, pair.Item2).Message;
            return $$"""
              { static constexpr std::uint16_t expected[] = { {{string.Join(", ", expected.Select(ch => ((ushort)ch).ToString(CultureInfo.InvariantCulture)))}} };
                check({{pair.Item1}}, {{pair.Item2}}, expected, {{expected.Length}}); }
              """;
        }));
        string cpp = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            [[gnu::noinline]] static void check(std::int32_t a, std::int32_t b, const std::uint16_t* expected, std::uint32_t length) {
                Instance heap; heap.collect_before_allocation = true; Context c(heap); assert(prepare_exceptions(c) == 0);
                Object* root = nullptr; Frame frame; RootScope scope(c, frame, &root, 1);
                LocationArgument args[2]; args[0].bits = std::bit_cast<std::uint32_t>(a); args[1].bits = std::bit_cast<std::uint32_t>(b);
                root = {{symbol}}(c, args); assert(root != nullptr && c.pending == 0);
                assert(collect(c).status == MarkStatus::complete);
                auto* payload = reinterpret_cast<ExceptionObject*>(root); assert(payload->message == nullptr);
                const auto* from = payload->cast_from; const auto* to = payload->cast_to;
                assert(from != nullptr && to != nullptr && !payload->cast_nullable);
                const ExceptionOrigin saved{7, "origin", 17, 23}; set_exception_origin(c, saved);
                const auto count = heap.count;
                const auto* unknown = reinterpret_cast<const Descriptor*>(std::uintptr_t{1});
                payload->cast_from = unknown; assert(exception_message(c, root).status == 6); payload->cast_from = from;
                payload->cast_to = unknown; assert(exception_message(c, root).status == 6); payload->cast_to = to;
                payload->cast_nullable = true; assert(exception_message(c, root).status == 6); payload->cast_nullable = false;
                assert(heap.count == count && payload->message == nullptr && c.pending == saved.status);
                heap.object_limit = count;
                assert(exception_message(c, root).status == 5);
                assert(heap.count == count && payload->message == nullptr && c.pending == saved.status && c.image == saved.image && c.method == 17 && c.il == 23);
                heap.object_limit = count + 1;
                auto first = exception_message(c, root); assert(first.status == 0 && first.value != nullptr && heap.count == count + 1);
                assert(string_length(first.value) == length);
                for (std::uint32_t i = 0; i < length; ++i) assert(string_code_unit(first.value, i) == expected[i]);
                assert(payload->message == first.value && c.pending == saved.status && c.image == saved.image && c.method == 17 && c.il == 23);
                auto again = exception_message(c, root); assert(again.status == 0 && again.value == first.value && heap.count == count + 1);
                root = nullptr; set_exception_origin(c, {});
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
            }
            int main() { {{checks}} }
            """;
        using var files = new ResolverFiles(); string dir = files.DirectoryPath("cast-text"), path = Path.Combine(dir, "cast.cpp"), exe = Path.Combine(dir, "cast");
        await File.WriteAllTextAsync(path, cpp, TestContext.Current.CancellationToken); NativeEvidence.Record(path, optimized);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", path, "-o", exe], dir);
        Assert.True(build.ExitCode == 0, build.Stderr); NativeEvidence.Record(path, optimized, exe);
        var run = await NativeProcess.Run(exe, [], dir); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
