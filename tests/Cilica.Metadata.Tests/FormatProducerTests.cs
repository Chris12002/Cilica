using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class FormatProducerTests
{
    private static readonly string[] Names = ["Temporary", "Several", "AddressedParameter", "AddressedLocal", "FaultOrder", "NullReceiver"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(FormatProducerProbe), Names));
    [Fact]
    public async Task Temporary_and_addressed_producers_validate_at_both_stages()
    { foreach (var p in await Programs.Value) { ManagedExceptionIrValidator.Validate(p); ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(p)); } }
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)] [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Temporary_values_preserve_bits_order_and_faults(bool optimized, int group)
    {
        var programs = await Programs.Value;
        var pairs = (from a in new[] { int.MinValue, -1, 0, 1, 2, 3, int.MaxValue }
                     from b in new[] { int.MinValue, -1, 0, 1, 2, 3, int.MaxValue } select (a, b)).ToArray();
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
                (name, programs[i], typeof(FormatProducerProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 2).Take(2).ToArray(), pairs);
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }
    [Fact]
    public async Task Aliases_and_dynamic_formats_stay_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(FormatProducerProbe).Assembly.Location).Value!;
        foreach (string name in new[] { "UnsupportedLiteralAlias", "UnsupportedBoxAlias", "UnsupportedDynamic" })
        {
            var result = ValueTranslator.TranslateIntegerObjects(scope, input, (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(FormatProducerProbe).GetMethod(name)!.MetadataToken), implementation);
            Assert.False(result.Success, name); Assert.Null(result.Program);
            Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.ExecutionUnsupported);
        }
    }
    [Fact]
    public async Task A_branch_target_store_cannot_supply_format_proof_from_a_predecessor()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var builder = new ResolutionAssemblyBuilder(new("JoinedFormatStore", new Version(1, 0, 0, 0)));
        var reference = builder.Reference(implementation.Identity);
        var text = builder.TypeReference("System", "String", reference);
        var integer = builder.TypeReference("System", "Int32", reference);
        var format = builder.Member(text, "Format", 0, 2, 0x0e, 0x0e, 0x1c);
        var literal = builder.String("{0}"); builder.Type("Test", "Caller");
        var root = builder.Method("Run", [0, 2, 8, 8, 8], locals: [7, 1, 8], emit: il =>
        {
            var other = il.DefineLabel(); var store = il.DefineLabel();
            il.LoadString(literal); il.LoadArgument(0); il.Branch(ILOpCode.Brfalse, other);
            il.LoadArgument(1); il.Branch(ILOpCode.Br, store);
            il.MarkLabel(other); il.LoadConstantI4(1);
            il.MarkLabel(store); il.StoreLocal(0); il.LoadLocal(0); il.OpCode(ILOpCode.Box); il.Token(integer);
            il.Call(format); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        var input = scope.OpenInput(files.Write("joined-format-store.dll", builder.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, root, implementation);
        Assert.False(result.Success); Assert.Null(result.Program);
        Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.ExecutionUnsupported);
    }
}
