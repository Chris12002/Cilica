using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class KnownReceiverTests
{
    private static readonly string[] Names = ["Parameters", "References", "LocalsAndFields", "Abstract", "Generic", "TypeKeys"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(KnownReceiverProbe),Names));
    [Fact]
    public async Task Type_key_hash_does_not_discover_unrelated_comparer_hash()
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(KnownReceiverProbe),"TypeKeys"))[0];
        Assert.DoesNotContain(program.Flow.Flow.Flow.Methods,m => m.Source.Name == "System.OrdinalCaseSensitiveComparer::GetHashCode");
    }
    [Fact]
    public async Task Object_reference_element_cannot_be_narrowed_from_the_callers_allocation()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(KnownReceiverProbe).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(KnownReceiverProbe).GetMethod("UnprovenReference")!.MetadataToken), implementation);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,d => d.Message.Contains("proven ordinary class receiver",StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)]
    public async Task Known_base_receivers_preserve_subclass_dispatch_and_null_checks(bool optimized,int group)
    {
        var programs = await Programs.Value;
        foreach (var program in programs)
            Assert.DoesNotContain(program.Flow.Flow.Flow.Methods,m => m.Source.Name == "Cilica.Fixtures.Execution.UnrelatedReceiver::ToString");
        foreach (int index in Enumerable.Range(group*2,2))
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,
                [(Names[index],programs[index],typeof(KnownReceiverProbe).GetMethod(Names[index])!.CreateDelegate<Func<int,int,int>>())],
                expectedStaticRoots: Names[index] == "TypeKeys" ? 1 : 0);
    }
}
