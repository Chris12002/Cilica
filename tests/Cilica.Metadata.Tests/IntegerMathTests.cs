using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Cilica.Metadata.Resolution;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class IntegerMathTests
{
    private static readonly string[] Names = ["Abs32", "Abs64", "Signed32", "Unsigned32", "Signed64", "Unsigned64", "Boundaries", "Compare32", "CompareUnsigned32", "Compare64", "CompareUnsigned64"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(IntegerMathProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    public async Task Integer_math_matches_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(IntegerMathProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Take(7).Skip(group * 4).Take(4).ToArray());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Typed_integer_comparisons_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(IntegerMathProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(7).ToArray());
    }

    [Fact]
    public async Task Only_exact_integer_overloads_map_to_implemented_members()
    {
        using var files = new ResolverFiles();
        var (_, _, resolver, _, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var contract = resolver.ResolveAssembly(plan.Bindings[0].Contract.Assembly).Value!;
        using var stream = File.OpenRead(LibraryBuildProducerTests.Request(files, CoreLibraryBuildTests.CorePath("CoreTypes.cs")).References.Single());
        using var image = new PEReader(stream); var reader = image.GetMetadataReader();
        var math = resolver.ResolveType(contract, "System", "Math").Value!;
        int supported = 0, rejected = 0;
        foreach (var handle in reader.GetTypeDefinition(math.Handle).GetMethods())
        {
            string name = reader.GetString(reader.GetMethodDefinition(handle).Name);
            if (name is not ("Abs" or "Min" or "Max")) continue;
            var signature = resolver.ResolveMethod(contract, handle).Value!.Signature;
            bool expected = signature.Parameters[0] is TypeSignature.Primitive primitive
                && (primitive.Code is PrimitiveTypeCode.Int32 or PrimitiveTypeCode.Int64
                    || name != "Abs" && primitive.Code is PrimitiveTypeCode.UInt32 or PrimitiveTypeCode.UInt64);
            var mapped = resolver.ResolveLibraryMethod(plan, contract, handle);
            Assert.Equal(expected, mapped.Success);
            if (expected) supported++; else rejected++;
        }
        Assert.Equal(10, supported); Assert.True(rejected > 0);
        foreach (var type in new[] { "Int32", "UInt32", "Int64", "UInt64" })
        {
            var owner = resolver.ResolveType(contract, "System", type).Value!;
            foreach (var handle in reader.GetTypeDefinition(owner.Handle).GetMethods())
            {
                if (reader.GetString(reader.GetMethodDefinition(handle).Name) != "CompareTo") continue;
                var parameter = resolver.ResolveMethod(contract, handle).Value!.Signature.Parameters[0];
                Assert.Equal(parameter is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Object },
                    resolver.ResolveLibraryMethod(plan, contract, handle).Success);
            }
        }
    }

}
