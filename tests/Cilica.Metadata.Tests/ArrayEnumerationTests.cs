using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cilica.Metadata.Resolution;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ArrayEnumerationTests
{
    private static readonly string[] Names = ["Integers", "States", "Empty", "Reference", "Value", "Covariance", "FreshAndNull", "VariantClosure"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ArrayEnumerationProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Generic_array_enumeration_matches_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ArrayEnumerationProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray(), persistentRoots: true);
    }

    [Fact]
    public async Task Exact_grant_and_unsupported_boundaries_are_preserved()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var input = resolver.OpenInput(typeof(ArrayEnumerationProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name) => (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ArrayEnumerationProbe).GetMethod(name)!.MetadataToken);
        var narrowed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != LibraryIntrinsicCapability.ArrayInterfaceEnumerate)] }] };
        var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
        using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(denied, input, Method("Integers"), implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        foreach (var (name, message) in new[] { ("UnsupportedAlias", "scalar aliases") })
        {
            var unsupported = ValueTranslator.TranslateIntegerObjects(scope, input, Method(name), implementation);
            Assert.False(unsupported.Success);
            Assert.Contains(unsupported.Diagnostics, d => d.Message.Contains(message, StringComparison.OrdinalIgnoreCase));
        }
    }
    [Fact]
    public async Task Detached_enumeration_packets_preserve_exact_result_and_receiver()
    {
        var programs = await Programs.Value;
        foreach (int index in new[] { 0, 4, 5 })
            foreach (var original in new[] { programs[index].Flow.InterfaceDispatch!, ValueScalarLowering.LowerExceptions(programs[index]).Flow.Flow.Flow.Flow.InterfaceDispatch! })
            {
                var plan = original with { Methods = [.. original.Methods], Declarations = [.. original.Declarations], Dispatch = [.. original.Dispatch] };
                ObjectInterfacePlanValidator.Validate(plan);
                foreach (var fact in plan.Methods.Where(m => m.ArrayHelper && m.Name == "Enumerate"))
                    foreach (var changed in new[] {
                        fact with { ReturnType = IrType.Reference(plan.Types.ObjectBase) },
                        fact with { Parameters = fact.Parameters.SetItem(0, IrType.Reference(plan.Types.ObjectBase)) },
                        fact with { Parameters = fact.Parameters.Add(IrType.Int32) },
                        fact with { Name = "Identity" }, fact with { ArrayHelper = false } })
                        Assert.Throws<ArgumentException>(() => ObjectInterfacePlanValidator.Validate(plan with { Methods = plan.Methods.Replace(fact, changed) }));
            }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Enumerators_retain_nonempty_arrays_but_empty_cache_does_not(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(ArrayEnumerationProbe), ["Tracked"]))[0];
        var flow = program.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string Symbol(string name) { var method = flow.Methods.Single(m => m.Source.Name == "Cilica.Fixtures.Execution.ArrayEnumerationProbe::" + name); return $"cilica_m_{method.Source.ContentHash}_{method.Source.Token:x8}"; }
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                assert(prepare_exceptions(c) == 0);
                Object* enumerator = nullptr; Frame frame; RootScope roots(c, frame, &enumerator, 1);
                enumerator = {{Symbol("CreateTracked")}}(c, nullptr);
                assert(c.pending == 0 && enumerator != nullptr);
                assert(collect(c).status == MarkStatus::complete && heap.count == 4);
                LocationArgument args[1]; args[0].reference = enumerator;
                {{Symbol("EndTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == 4);
                enumerator = nullptr; args[0].reference = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                assert(c.used == 0 && c.borrows == nullptr);
            }
            """;
        await RunNative(source, optimized);
        program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(ArrayEnumerationProbe), ["Cache"]))[0]; flow = program.Flow.Flow.Flow;
        artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                Instance other; other.collect_before_allocation = true; Context d(other);
                LocationArgument args[2]; args[0].bits = 0; args[1].bits = 0;
                assert(cilica_m_{{flow.Entry.ContentHash}}_{{flow.Entry.Token.ToString("x8", System.Globalization.CultureInfo.InvariantCulture)}}(c, args) == 11 && c.pending == 0);
                assert(cilica_m_{{flow.Entry.ContentHash}}_{{flow.Entry.Token.ToString("x8", System.Globalization.CultureInfo.InvariantCulture)}}(d, args) == 11 && d.pending == 0);
                assert(static_field_count == 2);
                // Two closed empty enumerators and the emergency exception; no caller arrays survive.
                assert(collect(c).status == MarkStatus::complete && heap.count == 3);
                assert(collect(d).status == MarkStatus::complete && other.count == 3);
                for (std::size_t i = 0; i < static_field_count; ++i) {
                    assert(heap.statics[i].reference != nullptr && other.statics[i].reference != nullptr);
                    assert(heap.statics[i].reference != other.statics[i].reference);
                    heap.statics[i].reference = nullptr;
                }
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                assert(collect(d).status == MarkStatus::complete && other.count == 3);
                assert(c.used == 0 && c.top == nullptr && c.borrows == nullptr);
            }
            """;
        await RunNative(source, optimized);
    }
    private static async Task RunNative(string source, bool optimized)
    {
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("array-enumerator-gc"), cpp = Path.Combine(directory, "enumerator.cpp"), executable = Path.Combine(directory, "enumerator");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
