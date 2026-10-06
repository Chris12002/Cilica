using System;
using System.Collections.Generic;
using System.IO;
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
        (nameof(NullableProbe.BoxLookalike), NullableProbe.BoxLookalike)];
    private static readonly string[] BoxGaps = [nameof(NullableProbe.UnsupportedBox), nameof(NullableProbe.UnsupportedEmptyBox), nameof(NullableProbe.UnsupportedUnbox)];
    private static readonly string[] MemberGaps = [nameof(NullableProbe.UnsupportedText), nameof(NullableProbe.UnsupportedHash), nameof(NullableProbe.UnsupportedEquality)];
    private static readonly Lazy<Task<Dictionary<string, ManagedExceptionTranslationResult>>> Results = new(Translate);

    private static async Task<Dictionary<string, ManagedExceptionTranslationResult>> Translate()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(NullableProbe).Assembly.Location).Value!;
        return Probes.Select(p => p.Name).Concat(BoxGaps).Concat(MemberGaps)
            .Append(nameof(NullableProbe.UnsupportedHeapReturn)).Append(nameof(NullableProbe.UnsupportedStaticInline))
            .Append(nameof(NullableProbe.UnsupportedEnumWidth)).Append(nameof(NullableProbe.UnsupportedEnumVisibility)).ToDictionary(name => name, name =>
            ValueTranslator.TranslateIntegerObjects(scope, input,
                (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(NullableProbe).GetMethod(name)!.MetadataToken), implementation));
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
            Assert.Contains(BoxGaps.Contains(name) ? "Nullable boxing and unboxing" : "no implemented managed body", diagnostic.Message, StringComparison.Ordinal);
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

}
