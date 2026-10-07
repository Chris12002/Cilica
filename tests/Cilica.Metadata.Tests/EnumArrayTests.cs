using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class EnumArrayTests
{
    private static readonly string[] Names = ["Direct", "Alias", "Empty", "Constant", "Field", "Faults", "Identity", "Hidden", "Static", "CastFault", "Views"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(EnumArrayProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 4)] [InlineData(true, 4)] [InlineData(false, 9)] [InlineData(true, 9)]
    public async Task Nominal_storage_aliases_and_faults_match_dotnet(bool optimized, int first)
    {
        var programs=await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name,i)=>(name,programs[i],typeof(EnumArrayProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(first).Take(first == 9 ? 2 : 4).ToArray());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Constant_enum_static_array_is_retained_exactly(bool optimized) =>
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("Static",(await Programs.Value)[8],EnumArrayProbe.Static)], expectedStaticRoots: 1);
    [Theory]
    [InlineData("missing")] [InlineData("duplicate")] [InlineData("kind")] [InlineData("identity")] [InlineData("layout")] [InlineData("lexical")]
    public async Task Both_stages_reject_forged_enum_vector_facts(string damage)
    {
        var program=(await Programs.Value)[0]; var logical=program.Flow.Flow.Flow.Types.References;
        var scalar=ValueScalarLowering.LowerExceptions(program);var objects=scalar.Flow.Flow.Flow.Flow;
        var fact=logical.EnumArrays.Single(); var array=logical.Arrays.Single(a=>a.ArrayElement!.Kind==ObjectArrayElementKind.Enum32);
        var changed=damage switch {
            "missing"=>logical with {EnumArrays=[]},
            "duplicate"=>logical with {EnumArrays=logical.EnumArrays.Add(fact)},
            "kind"=>logical with {Arrays=logical.Arrays.Replace(array,array with {ArrayElement=ObjectArrayElement.Int32})},
            "identity"=>logical with {EnumArrays=[fact with {Source=fact.Source with {Token=0x0200ffff}}]},
            "layout"=>logical with {EnumArrays=[fact with {Attributes=fact.Attributes | TypeAttributes.ExplicitLayout}]},
            _=>logical with {EnumArrays=[fact with {LexicalOwners=[]}]}
        };
        Assert.Throws<ArgumentException>(()=>ManagedExceptionIrValidator.Validate(program with {Flow=program.Flow with {Flow=program.Flow.Flow with {Flow=program.Flow.Flow.Flow with {Types=program.Flow.Flow.Flow.Types with {References=changed}}}}}));
        Assert.Throws<ArgumentException>(()=>ScalarManagedExceptionIrValidator.Validate(scalar with {Flow=scalar.Flow with {Flow=scalar.Flow.Flow with {Flow=scalar.Flow.Flow.Flow with {Flow=objects with {Flow=objects.Flow with {Types=changed}}}}}}));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Former_enum_interface_boundaries_match_dotnet(bool optimized)
    {
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(EnumArrayProbe), "UnsupportedInterface", "UnsupportedIntegerView");
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("EnumView", programs[0], EnumArrayProbe.UnsupportedInterface), ("IntegerView", programs[1], EnumArrayProbe.UnsupportedIntegerView)]);
    }
    [Fact]
    public async Task Unimplemented_widths_and_unsigned_vectors_fail_closed()
    {
        using var files=new ResolverFiles();var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=resolver.OpenInput(typeof(EnumArrayProbe).Assembly.Location).Value!;
        foreach(var name in new[]{"UnsupportedUnsigned","UnsupportedWidth"}) {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(EnumArrayProbe).GetMethod(name)!.MetadataToken),implementation);
            Assert.False(result.Success,name);Assert.NotEmpty(result.Diagnostics);
        }
    }
}
