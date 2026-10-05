using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class InvariantCultureTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram>> Fixture = new(() => CoreLibraryBuildTests.TranslateFixture(nameof(CoreLibraryProbe.InvariantIdentity)));

    [Fact]
    public async Task Selected_runtime_provider_requires_explicit_integer_profile_and_supported_member()
    {
        ManagedExceptionIrValidator.Validate(await Fixture.Value);
        // Strict platform layout retains its existing shared String/interface gate.
        Assert.False(IntegerObjectTranslationTests.Translate(nameof(CoreLibraryProbe.InvariantIdentity), fixture: typeof(CoreLibraryProbe)).Success);
        Assert.False(IntegerObjectTranslationTests.Translate(nameof(CoreLibraryProbe.InvariantIdentity), enabled: false, fixture: typeof(CoreLibraryProbe)).Success);
        var unsupported = IntegerObjectTranslationTests.Translate(nameof(CoreLibraryProbe.UnsupportedCulture), fixture: typeof(CoreLibraryProbe));
        Assert.False(unsupported.Success); Assert.Contains(unsupported.Diagnostics, d => d.Message.Contains("Only the selected CultureInfo.InvariantCulture", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("name")] [InlineData("layout")] [InlineData("profile")] [InlineData("identity")] [InlineData("fault")]
    public async Task Detached_IR_rejects_invalid_provider_contract(string corruption)
    {
        var program = await Fixture.Value; var flow = program.Flow.Flow.Flow; var references = flow.Types.References;
        var culture = references.Types.Single(t => t.Source.Name == "System.Globalization.CultureInfo");
        var id = ObjectTypeId.From(culture.Source);
        if (corruption == "name") references = references with { Types = references.Types.Replace(culture, culture with { Source = culture.Source with { Name = "Other.CultureInfo" } }) };
        if (corruption == "layout") references = references with { Types = references.Types.Replace(culture, culture with { Attributes = culture.Attributes | TypeAttributes.Sealed }) };
        if (corruption == "profile") references = references with { Integer64 = false };
        if (corruption is "identity" or "fault") flow = flow with { Methods = [.. flow.Methods.Select(m => m with {
            Blocks = [.. m.Blocks.Select(b => b with { Statements = [.. b.Statements.Select(s => s is IrStatement.Define { Value.Expression: IrExpression.InvariantCulture } d
                ? corruption == "fault" ? d with { Exceptional = null } : d with { Value = d.Value with { Expression = new IrExpression.InvariantCulture(id with { ContentHash = new string('a', 64) }) } } : s)] })] })] };
        flow = flow with { Types = flow.Types with { References = references } };
        program = program with { Flow = program.Flow with { Flow = new(flow) } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Provider_cache_survives_collection_rejects_foreign_pointers_and_obeys_heap_limits(bool optimized)
    {
        var program = await Fixture.Value; var flow = program.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        var culture = flow.Types.References.Types.Single(t => t.Source.Name == "System.Globalization.CultureInfo");
        string type = "t_" + culture.Source.ContentHash + "_" + culture.Source.Token.ToString("x8", CultureInfo.InvariantCulture);
        var source = new StringBuilder(artifact.Text).Append(CultureInfo.InvariantCulture, $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            static bool count_provider(void* state, const ReferenceEdge& edge) noexcept {
                if (edge.kind == ReferenceKind::invariant_culture) ++*static_cast<unsigned*>(state);
                return true;
            }
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                heap.object_limit = 0;
                assert(invariant_culture<{{type}}>(c, {{type}}_descriptor) == nullptr);
                assert(heap.invariant_culture == nullptr && heap.count == 0);
                heap.object_limit = 1; c.pending = 0;
                auto* first = invariant_culture<{{type}}>(c, {{type}}_descriptor);
                assert(first != nullptr && c.pending == 0 && heap.count == 1);
                assert(collect(c).status == MarkStatus::complete && heap.owns(first));
                heap.object_limit = 0;
                assert(invariant_culture<{{type}}>(c, {{type}}_descriptor) == first);
                unsigned roots = 0;
                assert(visit_roots(c, count_provider, &roots) == TraceResult::complete && roots == 1);
                Instance other; Context foreign(other);
                auto* second = invariant_culture<{{type}}>(foreign, {{type}}_descriptor);
                assert(second != nullptr && second != first);
                heap.invariant_culture = second;
                assert(invariant_culture<{{type}}>(c, {{type}}_descriptor) == nullptr && c.pending == 6);
                assert(collect(c).status == MarkStatus::invalid && heap.owns(first));
                heap.invariant_culture = reinterpret_cast<Object*>(static_cast<std::uintptr_t>(1));
                assert(invariant_culture<{{type}}>(c, {{type}}_descriptor) == nullptr && c.pending == 6);
                assert(collect(c).status == MarkStatus::invalid && heap.owns(first));
                heap.invariant_culture = first; c.pending = 0;
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
            }
            """);
        using var files = new ResolverFiles(); string dir = files.DirectoryPath("culture"), cpp = Path.Combine(dir, "culture.cpp"), exe = Path.Combine(dir, "culture");
        File.WriteAllText(cpp, source.ToString());
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048",
                "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], dir);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], dir); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
