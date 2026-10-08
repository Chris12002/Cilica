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
public sealed class CandidateCharacterTextTests
{
    private static readonly string[] Names = ["Units", "Aliases", "Roots", "FaultOrder"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateCharacterTextProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    public async Task Character_text_payload_identity_roots_and_faults_match_dotnet(bool optimized, int index)
    {
        int[] edges = [int.MinValue, -1, 0, 1, 97, 127, 128, 255, 256, 0xd7ff, 0xd800, 0xdbff, 0xdc00, 0xdfff, 0xe000, 0xffff, 0x10000, int.MaxValue];
        (int,int)[] pairs = index switch {
            0 => Enumerable.Range(0,2048).Select(i => (i*32,32)).ToArray(),
            1 => (from a in edges from b in edges select (a,b)).ToArray(),
            2 => (from a in edges from b in Enumerable.Range(0,3) select (a,b)).ToArray(),
            _ => (from a in edges from b in new[]{0,1} select (a,b)).ToArray()
        };
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [(Names[index], (await Programs.Value)[index], typeof(CandidateCharacterTextProbe).GetMethod(Names[index])!.CreateDelegate<Func<int,int,int>>())], pairs);
    }
    [Theory]
    [InlineData("UnsupportedProvider")] [InlineData("UnsupportedBox")]
    [InlineData("UnsupportedArrayBorrow")] [InlineData("UnsupportedFieldBorrow")]
    public async Task Unadmitted_overloads_and_receiver_origins_remain_diagnostic(string name)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(CandidateCharacterTextProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CandidateCharacterTextProbe).GetMethod(name)!.MetadataToken);
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Id is "CIL2001" or "CIL3010");
        TestContext.Current.TestOutputHelper!.WriteLine(string.Join("; ", result.Diagnostics));
    }
    [Theory]
    [InlineData("missing")] [InlineData("width")] [InlineData("signed")] [InlineData("name")]
    [InlineData("provider")] [InlineData("flags")] [InlineData("profile")]
    public async Task Detached_character_receiver_evidence_is_checked(string corruption)
    {
        var program = (await Programs.Value)[1]; var flow = program.Flow.Flow.Flow;
        var binding = program.Flow.Bindings.Single(b => b.Primitive?.Code == PrimitiveTypeCode.Char);
        var method = flow.Methods.Single(m => IrMethodId.From(m.Source) == binding.Method);
        Assert.Equal(IrType.Location(IrType.Int32, narrow: IntegerCastTarget.UInt16), method.Parameters[0]);
        var changed = corruption switch {
            "missing" => binding with { Primitive = null },
            "name" => binding with { Primitive = binding.Primitive! with { Source = binding.Primitive.Source with { Name = "User.Char" } } },
            "provider" => binding with { InvariantProviderOnly = true },
            "flags" => binding with { Attributes = binding.Attributes | MethodAttributes.NewSlot },
            _ => binding
        };
        if (corruption is "width" or "signed") flow = flow with { Methods = flow.Methods.Replace(method, method with {
            Parameters = [IrType.Location(IrType.Int32, narrow: corruption == "signed" ? IntegerCastTarget.Int16 : null)] }) };
        if (corruption == "profile") flow = flow with { Types = flow.Types with { References = flow.Types.References with { Integer64 = false } } };
        var invalid = program with { Flow = program.Flow with { Bindings = program.Flow.Bindings.Replace(binding, changed), Flow = new(flow) } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid));
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(program));
    }
}
