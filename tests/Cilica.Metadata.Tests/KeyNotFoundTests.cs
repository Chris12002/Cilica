using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class KeyNotFoundTests
{
    private static readonly string[] Names = ["Default", "Message", "Catch", "ConstructedMessage"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(KeyNotFoundProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    public async Task Selected_key_fault_messages_catches_and_identity_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(KeyNotFoundProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }
    [Theory]
    [InlineData("name")] [InlineData("parent")] [InlineData("resource")]
    public async Task Key_resource_requires_the_exact_framework_name_and_ancestry(string mutation)
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        var resource = program.Exceptions.Resources.Single(r => r.Kind == ExceptionResourceKind.KeyNotFound);
        var layout = flow.Types.References.Types.Single(t => ObjectTypeId.From(t.Source) == resource.Type);
        var changed = mutation == "parent" ? layout with { BaseType = program.Exceptions.Base }
            : layout with { Source = layout.Source with { Name = "Other.KeyNotFoundException" } };
        var invalid = mutation == "resource" ? program with { Exceptions = program.Exceptions with { Resources = program.Exceptions.Resources.Replace(resource, resource with { Type = program.Exceptions.Base }) } }
            : program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Types = flow.Types with { References = flow.Types.References with { Types = flow.Types.References.Types.Replace(layout, changed) } } } } } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program);
        var boxes = scalar.Flow.Flow;
        var invalidScalar = mutation == "resource" ? scalar with { Exceptions = invalid.Exceptions }
            : scalar with { Flow = scalar.Flow with { Flow = boxes with { Values = boxes.Values with { References = invalid.Flow.Flow.Flow.Types.References } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar));
    }
    [Fact]
    public async Task Layout_and_each_constructor_require_exact_grants_and_inner_ctor_stays_unsupported()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var input = resolver.OpenInput(typeof(KeyNotFoundProbe).Assembly.Location).Value!;
        var grants = artifact.Grants.Where(g => g.Owner == "System.Collections.Generic.KeyNotFoundException").ToArray(); Assert.Equal(3, grants.Length);
        foreach (var grant in grants)
        {
            var changed = manifest with { Artifacts = [artifact with { Grants = artifact.Grants.Remove(grant) }] };
            var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(changed);
            using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
            string name = grant.Capability == LibraryIntrinsicCapability.ExceptionLayout || resolver.ReadMethodDeclarationInput(implementation, (MethodDefinitionHandle)MetadataTokens.EntityHandle(grant.Token), new()).Value!.Signature.Parameters.Count == 0 ? "Default" : "Message";
            var result = ValueTranslator.TranslateIntegerObjects(denied, input, Method(name), implementation);
            Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        }
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        Assert.False(ValueTranslator.TranslateIntegerObjects(scope, input, Method("UnsupportedInner"), implementation).Success);
        MethodDefinitionHandle Method(string name) => (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(KeyNotFoundProbe).GetMethod(name)!.MetadataToken);
    }
}
