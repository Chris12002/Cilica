using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class CandidateScalarArrayReceiverTests
{
    [Theory]
    [InlineData("EqualsElement")] [InlineData("EqualsAfterArgumentMutation")]
    public async Task Scalar_array_element_receivers_remain_an_explicit_gap(string name)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(CandidateScalarArrayReceiverProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CandidateScalarArrayReceiverProbe).GetMethod(name)!.MetadataToken);
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Id == "CIL2001" && d.Message.Contains("Element addresses currently require a closed inline value or reference array", StringComparison.Ordinal));
        // A future lowering must read the receiver after evaluating the argument.
        Assert.Equal(1, CandidateScalarArrayReceiverProbe.EqualsAfterArgumentMutation(1, 2));
    }
}
