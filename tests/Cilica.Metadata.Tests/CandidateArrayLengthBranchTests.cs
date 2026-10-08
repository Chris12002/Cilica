using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class CandidateArrayLengthBranchTests
{
    [Fact]
    public async Task Direct_array_length_truth_tests_remain_an_explicit_gap()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(CandidateArrayLengthBranchProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CandidateArrayLengthBranchProbe).GetMethod("EmptyPair")!.MetadataToken);
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Id == "CIL2001" && d.Message.Contains("Array length requires an adjacent conv.i4", System.StringComparison.Ordinal));
    }
}
