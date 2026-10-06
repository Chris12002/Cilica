using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NullableLibraryTests
{
    private static readonly (string Name, Func<int, int, int> Oracle)[] Probes = [
        (nameof(NullableProbe.Scalars), NullableProbe.Scalars),
        (nameof(NullableProbe.WideAndNarrow), NullableProbe.WideAndNarrow),
        (nameof(NullableProbe.UnsignedAndSmall), NullableProbe.UnsignedAndSmall),
        (nameof(NullableProbe.BoxContainer), NullableProbe.BoxContainer),
        (nameof(NullableProbe.Enums), NullableProbe.Enums),
        (nameof(NullableProbe.IdsAndGenerics), NullableProbe.IdsAndGenerics),
        (nameof(NullableProbe.GuidValues), NullableProbe.GuidValues),
        (nameof(NullableProbe.ReferenceCopies), NullableProbe.ReferenceCopies),
        (nameof(NullableProbe.FieldsAndArrays), NullableProbe.FieldsAndArrays),
        (nameof(NullableProbe.ReferenceDefaults), NullableProbe.ReferenceDefaults),
        (nameof(NullableProbe.ValueFault), NullableProbe.ValueFault),
        (nameof(NullableProbe.ArgumentOrder), NullableProbe.ArgumentOrder),
        (nameof(NullableProbe.BoxLookalike), NullableProbe.BoxLookalike),
        (nameof(NullableProbe.NamedBoxRoundtrip), NullableProbe.NamedBoxRoundtrip),
        (nameof(NullableProbe.NamedBoxIdentity), NullableProbe.NamedBoxIdentity),
        (nameof(NullableProbe.ReferenceBoxRoundtrip), NullableProbe.ReferenceBoxRoundtrip),
        (nameof(NullableProbe.NullableBoxArray), NullableProbe.NullableBoxArray),
        (nameof(NullableProbe.EmptyBoxIdentity), NullableProbe.EmptyBoxIdentity),
        (nameof(NullableProbe.WideBoxRoundtrip), NullableProbe.WideBoxRoundtrip),
        (nameof(NullableProbe.WrongNullableBox), NullableProbe.WrongNullableBox),
        (nameof(NullableProbe.NullableGuidBox), NullableProbe.NullableGuidBox),
        (nameof(NullableProbe.LiftedConversions), NullableProbe.LiftedConversions),
        (nameof(NullableProbe.CheckedNullableConversion), NullableProbe.CheckedNullableConversion),
        (nameof(NullableProbe.MixedCastMessages), NullableProbe.MixedCastMessages),
        (nameof(NullableProbe.GenericNullableBox), NullableProbe.GenericNullableBox),
        (nameof(NullableProbe.NullableBoxBudget), NullableProbe.NullableBoxBudget),
        ("DirectOperators", (a, b) => unchecked(a + b))];
    private static readonly string[] BoxGaps = [nameof(NullableProbe.UnsupportedBox), nameof(NullableProbe.UnsupportedEmptyBox), nameof(NullableProbe.UnsupportedUnbox), nameof(NullableProbe.UnsupportedNullableEnumBox)];
    private static readonly string[] MemberGaps = [nameof(NullableProbe.UnsupportedText), nameof(NullableProbe.UnsupportedHash), nameof(NullableProbe.UnsupportedEquality)];
    private static readonly Lazy<Task<Dictionary<string, ManagedExceptionTranslationResult>>> Results = new(Translate);

    private static async Task<Dictionary<string, ManagedExceptionTranslationResult>> Translate()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(NullableProbe).Assembly.Location).Value!;
        var results = Probes.Select(p => p.Name).Where(n => n != "DirectOperators").Concat(BoxGaps).Concat(MemberGaps)
            .Append(nameof(NullableProbe.UnsupportedHeapReturn)).Append(nameof(NullableProbe.UnsupportedStaticInline))
            .Append(nameof(NullableProbe.UnsupportedEnumWidth)).Append(nameof(NullableProbe.UnsupportedEnumVisibility)).ToDictionary(name => name, name =>
            ValueTranslator.TranslateIntegerObjects(scope, input,
                (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(NullableProbe).GetMethod(name)!.MetadataToken), implementation));
        // C# normally lowers conversions to ctor/Value, so exercise the actual
        // operator declarations with authored IL as well.
        var builder = new ResolutionAssemblyBuilder(new("NullableOperators", new Version(1, 0, 0, 0)));
        var definition = builder.TypeReference("System", "Nullable`1", builder.Reference(implementation.Identity));
        byte coded = checked((byte)((MetadataTokens.GetRowNumber(definition) << 2) | 1));
        var integer = builder.Specification(0x15, 0x11, coded, 1, 8);
        var wrap = builder.Member(integer, "op_Implicit", 0, 1, 0x15, 0x11, coded, 1, 0x13, 0, 0x13, 0);
        var unwrap = builder.Member(integer, "op_Explicit", 0, 1, 0x13, 0, 0x15, 0x11, coded, 1, 0x13, 0);
        builder.Type("Test", "Operators");
        var root = builder.Method("Run", [0, 2, 8, 8, 8], emit: il => {
            il.LoadArgument(0); il.Call(wrap); il.Call(unwrap); il.LoadArgument(1); il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Ret);
        });
        var operators = scope.OpenInput(files.Write("nullable-operators.dll", builder.Build())).Value!;
        results.Add("DirectOperators", ValueTranslator.TranslateIntegerObjects(scope, operators, root, implementation));
        return results;
    }

    [Fact]
    public async Task Admitted_payloads_and_members_translate_through_ordinary_value_operations()
    {
        var results = await Results.Value;
        Assert.All(Probes, p => Assert.True(results[p.Name].Success, p.Name + ": " + string.Join("; ", results[p.Name].Diagnostics)));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Nullable_values_copies_roots_and_faults_match_dotnet(bool optimized)
    {
        var results = await Results.Value;
        var probes = Probes.Select(p => {
            Assert.True(results[p.Name].Success, p.Name + ": " + string.Join("; ", results[p.Name].Diagnostics));
            return (p.Name, results[p.Name].Program!, p.Oracle);
        }).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, probes);
    }

    [Fact]
    public async Task Nullable_boxing_and_unimplemented_members_never_fall_back_to_struct_semantics()
    {
        var results = await Results.Value;
        var heapReturn = results[nameof(NullableProbe.UnsupportedHeapReturn)];
        Assert.False(heapReturn.Success);
        Assert.Contains("Direct struct-return calls stored into heap fields", Assert.Single(heapReturn.Diagnostics).Message, StringComparison.Ordinal);
        var staticValue = results[nameof(NullableProbe.UnsupportedStaticInline)];
        Assert.False(staticValue.Success);
        Assert.Contains("Static fields currently require", Assert.Single(staticValue.Diagnostics).Message, StringComparison.Ordinal);
        var enumWidth = results[nameof(NullableProbe.UnsupportedEnumWidth)];
        Assert.False(enumWidth.Success);
        Assert.Contains("NullableSmallKind", Assert.Single(enumWidth.Diagnostics).Message, StringComparison.Ordinal);
        var enumVisibility = results[nameof(NullableProbe.UnsupportedEnumVisibility)];
        Assert.False(enumVisibility.Success);
        Assert.Contains("NullableInternalKind", Assert.Single(enumVisibility.Diagnostics).Message, StringComparison.Ordinal);
        foreach (string name in BoxGaps.Concat(MemberGaps))
        {
            var result = results[name];
            Assert.False(result.Success, name); Assert.Null(result.Program);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(DiagnosticIds.ExecutionUnsupported, diagnostic.Id);
            Assert.Contains(BoxGaps.Contains(name) ? "primitive/enum boxes need nominal payload descriptors" : "no implemented managed body", diagnostic.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Nullable_contract_matches_exact_members_and_rejects_invalid_arguments_and_raw_unbox()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var contract = resolver.ResolveAssembly(plan.Bindings[0].Contract.Assembly).Value!;
        var source = resolver.ResolveType(contract, "System", "Nullable`1").Value!;
        var target = resolver.ResolveType(implementation, "System", "Nullable`1").Value!;
        using var stream = File.OpenRead(LibraryBuildProducerTests.Request(files, CoreLibraryBuildTests.CorePath("CoreTypes.cs")).References.Single());
        using var image = new PEReader(stream); var reader = image.GetMetadataReader();
        foreach (var method in reader.GetTypeDefinition(source.Handle).GetMethods())
        {
            var result = resolver.ResolveLibraryMethod(plan, contract, method);
            Assert.True(result.Success, reader.GetString(reader.GetMethodDefinition(method).Name) + ": " + string.Join("; ", result.Diagnostics));
        }
        var integer = new TypeSignature.Primitive(PrimitiveTypeCode.Int32);
        var nested = new TypeSignature.Generic(new(target, SignatureTypeKind.ValueType), new([integer]));
        foreach (TypeSignature argument in new TypeSignature[] { new TypeSignature.Primitive(PrimitiveTypeCode.String), nested })
        {
            var read = resolver.ReadClosedTypeInput(new(target, new([argument])), implementation, new());
            Assert.False(read.Success); Assert.NotEmpty(read.Diagnostics);
        }
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var builder = new ResolutionAssemblyBuilder(new("NullableUnbox", new Version(1, 0, 0, 0)));
        var nullable = builder.TypeReference("System", "Nullable`1", builder.Reference(implementation.Identity));
        var operand = builder.Specification(0x15, 0x11, checked((byte)((MetadataTokens.GetRowNumber(nullable) << 2) | 1)), 1, 8);
        builder.Type("Test", "Caller");
        var root = builder.Method("Run", [0, 0, 8], emit: il => {
            il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Unbox); il.Token(operand); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        var input = scope.OpenInput(files.Write("nullable-unbox.dll", builder.Build())).Value!;
        var rejected = ValueTranslator.TranslateIntegerObjects(scope, input, root, implementation);
        Assert.False(rejected.Success);
        Assert.Contains("Nullable boxing and unboxing", Assert.Single(rejected.Diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nullable_key_null_check_cannot_assume_all_value_types_are_nonnull()
    {
        using var files = new ResolverFiles();
        string extra = files.Write("NullableKeyNull.cs", System.Text.Encoding.UTF8.GetBytes("""
            namespace Cilica.Runtime {
                public static class NullableKeyNull {
                    public static int Run(int a, int b) {
                        int? value = default(int?);
                        return KeyOperations<int?>.IsNull(value) ? a : b;
                    }
                }
            }
            """));
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files, extra);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var owner = scope.ResolveType(implementation, "Cilica.Runtime", "NullableKeyNull").Value!;
        var method = scope.ResolveClosedDeclaration(new(owner, SignatureList<TypeSignature>.Empty), "Run", 2,
            new MemberResolutionBudget(1000)).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, implementation, method.Handle, implementation);
        Assert.False(result.Success); Assert.Null(result.Program);
        Assert.Contains("Nullable key operations require", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Empty_nullable_box_needs_no_heap_slot_and_present_box_failure_recovers(bool optimized)
    {
        var result = (await Results.Value)[nameof(NullableProbe.NullableBoxBudget)];
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!;
        var flow = program.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string entry = "cilica_m_" + flow.Entry.ContentHash + "_" + flow.Entry.Token.ToString("x8", CultureInfo.InvariantCulture);
        string source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; heap.object_limit = 1;
                Context c(heap); LocationArgument args[2]; args[0].bits = 17; args[1].bits = 0;
                assert({{entry}}(c, args) == 17 && c.pending == 0);
                assert(heap.count == 1 && heap.emergency_exception != nullptr);
                args[1].bits = 1; (void){{entry}}(c, args);
                assert(c.pending == 5 && c.exception == heap.emergency_exception);
                assert(c.top == nullptr && c.depth == 0 && c.used == 0 && c.borrows == nullptr);
                c.pending = 0; c.exception = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                heap.object_limit = 2;
                assert({{entry}}(c, args) == ~17 && c.pending == 0);
                assert(c.top == nullptr && c.depth == 0 && c.used == 0 && c.borrows == nullptr);
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
            }
            """;
        using var files = new ResolverFiles();
        string dir = files.DirectoryPath("nullable-budget"), cpp = Path.Combine(dir, "budget.cpp"), exe = Path.Combine(dir, "budget");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], dir);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], dir);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }

}
