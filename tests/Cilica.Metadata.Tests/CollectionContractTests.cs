using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class CollectionContractTests
{
    [Fact]
    public async Task Pinned_contract_members_and_inherited_interfaces_have_exact_mappings()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var resolverLifetime = resolver;
        var contract = resolver.ResolveAssembly(plan.Bindings[0].Contract.Assembly).Value!;
        using var sourceStream = File.OpenRead(LibraryBuildProducerTests.Request(files, CoreLibraryBuildTests.CorePath("CoreTypes.cs")).References.Single());
        using var targetStream = File.OpenRead(built.AssemblyPath);
        using var sourceImage = new PEReader(sourceStream);
        using var targetImage = new PEReader(targetStream);
        var sourceReader = sourceImage.GetMetadataReader();
        var targetReader = targetImage.GetMetadataReader();
        foreach (var name in CoreLibraryBuildTests.CollectionContractNames)
        {
            var source = resolver.ResolveType(contract, name.Namespace, name.Name).Value!;
            var target = resolver.ResolveType(implementation, name.Namespace, name.Name).Value!;
            var sourceDefinition = sourceReader.GetTypeDefinition(source.Handle);
            var targetDefinition = targetReader.GetTypeDefinition(target.Handle);
            var expectedInterfaces = sourceDefinition.GetInterfaceImplementations().Select(h =>
                resolver.ResolveLibrarySignatureType(plan, contract, sourceReader.GetInterfaceImplementation(h).Interface).Value).ToHashSet();
            var actualInterfaces = targetDefinition.GetInterfaceImplementations().Select(h =>
                resolver.ResolveSignatureType(implementation, targetReader.GetInterfaceImplementation(h).Interface).Value).ToHashSet();
            Assert.DoesNotContain(null, expectedInterfaces); Assert.DoesNotContain(null, actualInterfaces);
            Assert.True(expectedInterfaces.SetEquals(actualInterfaces), name.Name);
            Assert.Equal(sourceDefinition.GetMethods().Count, targetDefinition.GetMethods().Count);
            foreach (var method in sourceDefinition.GetMethods())
            {
                var mapped = resolver.ResolveLibraryMethod(plan, contract, method);
                Assert.True(mapped.Success, string.Join("; ", mapped.Diagnostics));
                Assert.Equal(target, mapped.Value!.Implementation.DeclaringType.Definition);
            }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Mapped_read_only_collection_calls_match_dotnet(bool optimized)
    {
        var probes = new (string Name, Func<int, int, int> Oracle)[] {
            (nameof(CollectionContractProbe.Wide), CollectionContractProbe.Wide),
            (nameof(CollectionContractProbe.References), CollectionContractProbe.References),
            (nameof(CollectionContractProbe.Enumeration), CollectionContractProbe.Enumeration),
            (nameof(CollectionContractProbe.NonGenericEnumeration), CollectionContractProbe.NonGenericEnumeration),
            (nameof(CollectionContractProbe.Faults), CollectionContractProbe.Faults),
            (nameof(CollectionContractProbe.EnumeratorFaults), CollectionContractProbe.EnumeratorFaults) };
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(CollectionContractProbe), probes.Select(p => p.Name).ToArray());
        foreach (var program in programs)
        {
            Assert.NotNull(program.Flow.InterfaceDispatch);
            ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(program));
        }
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            probes.Select((p, i) => (p.Name, programs[i], p.Oracle)).ToArray());
    }

    [Fact]
    public async Task Class_variance_only_dispatch_stays_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var resolverLifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await System.IO.File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(CollectionContractProbe).Assembly.Location).Value!;
        foreach (string name in new[] { "VariantCall" })
        {
            var result = ValueTranslator.TranslateIntegerObjects(scope, input,
                (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CollectionContractProbe).GetMethod(name)!.MetadataToken), implementation);
            Assert.False(result.Success); Assert.Null(result.Program); Assert.NotEmpty(result.Diagnostics);
        }
    }
}
