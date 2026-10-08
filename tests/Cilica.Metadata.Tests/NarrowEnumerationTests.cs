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
using Cilica.Fixtures.Execution;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class NarrowEnumerationTests
{
    private static readonly string[] Names = ["Bytes", "Characters", "Booleans", "FreshBoxes", "CursorState", "EmptyAndNull", "Mixed", "QueryCount"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(NarrowEnumerationProbe), Names));
    [Fact]
    public async Task Exact_narrow_cursors_validate_before_and_after_lowering()
    {
        foreach (var program in await Programs.Value)
        {
            ManagedExceptionIrValidator.Validate(program);
            ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(program));
        }
    }
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    public async Task Enumeration_box_identity_state_and_queries_match_dotnet(bool optimized,int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name,i) =>
            (name,programs[i],typeof(NarrowEnumerationProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(2).ToArray());
    }
    [Fact]
    public async Task Nongeneric_enumeration_still_requires_its_exact_grant()
    {
        using var files = new ResolverFiles(); var(built,_,resolver,implementation,plan) = await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime = resolver;
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),new JsonSerializerOptions{Converters={new JsonStringEnumConverter<LibraryIntrinsicCapability>()}})!;
        var artifact = Assert.Single(manifest.Artifacts);
        var bytes = AssemblyResolver.SerializeLibraryBuildManifest(manifest with{Artifacts=[artifact with{Grants=[..artifact.Grants.Where(g=>g.Capability!=LibraryIntrinsicCapability.ArrayInterfaceEnumerateObjects)]}]});
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,bytes,Convert.ToHexStringLower(SHA256.HashData(bytes))).Value!;
        var input = scope.OpenInput(typeof(NarrowEnumerationProbe).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(NarrowEnumerationProbe).GetMethod("Bytes")!.MetadataToken),implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.LibraryIntrinsicDenied);
    }
    [Theory]
    [InlineData("UnsupportedTypedQuery")] [InlineData("UnsupportedSignedSource")] [InlineData("UnsupportedUInt16Source")]
    public async Task Other_narrow_storage_and_query_boundaries_remain_diagnostic(string name)
    {
        using var files = new ResolverFiles(); var(built,_,resolver,implementation,plan) = await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(NarrowEnumerationProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(NarrowEnumerationProbe).GetMethod(name)!.MetadataToken),implementation);
        Assert.False(result.Success); Assert.NotEmpty(result.Diagnostics);
        if(name=="UnsupportedTypedQuery") Assert.Contains(result.Diagnostics,d=>d.Message.Contains("Narrow memory operands",StringComparison.Ordinal));
    }
    [Fact]
    public async Task Detached_enumeration_cannot_substitute_another_narrow_box_identity()
    {
        var program=(await Programs.Value)[6];
        foreach(var plan in new[]{program.Flow.InterfaceDispatch!,ValueScalarLowering.LowerExceptions(program).Flow.Flow.Flow.Flow.InterfaceDispatch!})
        {
            var dispatch=plan.Dispatch.First(m=>m.Targets.Any(t=>t.Receiver.ArrayElement?.Kind==ObjectArrayElementKind.Byte&&plan.Methods.Any(h=>h.Binding.Method==t.Mapping&&h.Name=="EnumerateObjects")));
            var target=dispatch.Targets.First(t=>t.Receiver.ArrayElement?.Kind==ObjectArrayElementKind.Byte);
            var wrong=plan.Methods.First(m=>m.ArrayHelper&&m.Name=="EnumerateObjects"&&m.Parameters[0].Class?.ArrayElement?.Kind==ObjectArrayElementKind.Char);
            var changed=target with{Mapping=wrong.Binding.Method,Implementation=wrong.Binding.Method};
            Assert.Throws<ArgumentException>(()=>ObjectInterfacePlanValidator.Validate(plan with{Dispatch=plan.Dispatch.Replace(dispatch,dispatch with{Targets=dispatch.Targets.Replace(target,changed)})}));
        }
    }
}
