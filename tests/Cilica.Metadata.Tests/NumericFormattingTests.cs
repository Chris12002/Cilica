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

public sealed class NumericFormattingTests
{
    private static readonly string[] Names = ["FormatSigned32", "FormatUnsigned32", "FormatSigned64", "FormatUnsigned64", "IntegerTextIdentity"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Fixture = new(() => CoreLibraryBuildTests.TranslateFixtures(Names));

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Mapped_integer_APIs_match_every_oracle_code_unit_and_cache_boundary(bool optimized)
    {
        var programs = await Fixture.Value;
        var probes = Names.Take(4).Select((name, i) => {
            Func<int, int, int> oracle = (index, position) => {
                string text = i switch {
                    0 => unchecked((int)LibraryFormattingProbe.SignedCase(index)).ToString(CultureInfo.InvariantCulture),
                    1 => unchecked((uint)LibraryFormattingProbe.UnsignedCase(index)).ToString(CultureInfo.InvariantCulture),
                    2 => LibraryFormattingProbe.SignedCase(index).ToString(CultureInfo.InvariantCulture),
                    _ => LibraryFormattingProbe.UnsignedCase(index).ToString(CultureInfo.InvariantCulture) };
                return position < 0 ? text.Length : position < text.Length ? text[position] : -1;
            };
            return (name, programs[i], oracle);
        }).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, probes,
            (from index in Enumerable.Range(0, 17) from position in Enumerable.Range(-1, 22) select (index, position)).ToArray());
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [(Names[4], programs[4], CoreLibraryProbe.IntegerTextIdentity)],
            [.. new[] { 0, 1, 9, 10, 99, 100, 255, 299, 300, -1 }.Select(n => (n, 0))]);
    }

    [Theory]
    [InlineData("provider")] [InlineData("contract")] [InlineData("cache-index")] [InlineData("cache-text")] [InlineData("cache-fault")]
    public async Task Detached_IR_enforces_provider_provenance_and_cache_operand_contracts(string corruption)
    {
        var program = (await Fixture.Value)[1]; var flow = program.Flow.Flow.Flow;
        if (corruption == "contract")
        {
            var entry = program.Flow.Bindings.Single(b => b.Method == flow.Entry);
            program = program with { Flow = program.Flow with { Bindings = program.Flow.Bindings.Replace(entry, entry with { InvariantProviderOnly = true }) } };
        }
        else
        {
            flow = flow with { Methods = [.. flow.Methods.Select(m => m with { Blocks = [.. m.Blocks.Select(b => b with {
                Statements = [.. b.Statements.Select(s => s is IrStatement.Define d ? Corrupt(d) : s)] })] })] };
            program = program with { Flow = program.Flow with { Flow = new(flow) } };
        }
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program));
        IrStatement.Define Corrupt(IrStatement.Define d) => (corruption, d.Value.Expression) switch {
            ("provider", IrExpression.InvariantCulture) => d with { Value = d.Value with { Type = IrType.NullReference, Expression = new IrExpression.Null() } },
            ("cache-index", IrExpression.IntegerTextFind c) => d with { Value = d.Value with { Expression = c with { Index = -1 } } },
            ("cache-text", IrExpression.IntegerTextPublish c) => d with { Value = d.Value with { Expression = c with { Text = c.Index } } },
            ("cache-fault", IrExpression.IntegerTextPublish) => d with { Exceptional = null },
            _ => d,
        };
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Native_cache_retains_identity_checks_ownership_and_recovers_after_limits(bool optimized)
    {
        var program = (await Fixture.Value)[1]; var flow = program.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        var text = flow.Types.References.String!;
        string descriptor = "t_" + text.ContentHash + "_" + text.Token.ToString("x8", CultureInfo.InvariantCulture) + "_descriptor";
        var source = new StringBuilder(artifact.Text).Append(CultureInfo.InvariantCulture, $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            static bool count_numbers(void* state, const ReferenceEdge& edge) noexcept {
                if (edge.kind == ReferenceKind::integer_text) ++*static_cast<unsigned*>(state);
                return true;
            }
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                Object* roots[1]{}; Frame frame{}; RootScope scope(c, frame, roots, 1);
                const std::uint16_t unit[] = {49};
                roots[0] = string_literal(c, {{descriptor}}, 1, unit, 1);
                assert(roots[0] != nullptr && integer_text_find(c, {{descriptor}}, 1) == nullptr && c.pending == 0);
                heap.object_limit = heap.count;
                assert(integer_text_publish(c, {{descriptor}}, 1, roots[0]) == nullptr && heap.integer_texts == nullptr);
                heap.object_limit = 4096; c.pending = 0;
                auto* first = integer_text_publish(c, {{descriptor}}, 1, roots[0]);
                assert(first != nullptr && first != roots[0] && string_equals(first, roots[0]));
                assert(collect(c).status == MarkStatus::complete && heap.owns(first));
                heap.object_limit = heap.count;
                assert(integer_text_publish(c, {{descriptor}}, 1, roots[0]) == first && c.pending == 0);
                assert(integer_text_find(c, {{descriptor}}, 300) == nullptr && c.pending == 6);
                c.pending = 0;
                unsigned count = 0;
                assert(visit_roots(c, count_numbers, &count) == TraceResult::complete && count == 1);
                Instance other; Context foreign(other);
                Object* outsider = string_literal(foreign, {{descriptor}}, 1, unit, 1);
                assert(integer_text_publish(c, {{descriptor}}, 2, outsider) == nullptr && c.pending == 6);
                auto* saved = heap.integer_texts; heap.integer_texts = reinterpret_cast<StringObject*>(outsider);
                assert(integer_text_find(c, {{descriptor}}, 1) == nullptr && c.pending == 6);
                assert(collect(c).status == MarkStatus::invalid && heap.owns(first));
                heap.integer_texts = reinterpret_cast<StringObject*>(static_cast<std::uintptr_t>(1));
                assert(integer_text_find(c, {{descriptor}}, 1) == nullptr && c.pending == 6);
                assert(collect(c).status == MarkStatus::invalid);
                heap.integer_texts = saved; c.pending = 0;
                assert(collect(c).status == MarkStatus::complete && integer_text_find(c, {{descriptor}}, 1) == first);
            }
            """);
        using var files = new ResolverFiles(); string dir = files.DirectoryPath("numeric-cache"), cpp = Path.Combine(dir, "cache.cpp"), exe = Path.Combine(dir, "cache");
        File.WriteAllText(cpp, source.ToString());
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048",
                "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], dir);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], dir); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
