using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class IntegerSignTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Signed_extremes_zero_and_wrapped_directions_match_dotnet(bool optimized)
    {
        string[] names = ["Signed32", "Signed64", "Directions", "Boundaries"];
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(IntegerSignProbe), names);
        var pairs = (from a in new[] { int.MinValue, int.MinValue + 1, -65536, -1, 0, 1, 65536, int.MaxValue - 1, int.MaxValue }
                     from b in new[] { int.MinValue, -1, 0, 1, 2, int.MaxValue } select (a, b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, names.Select((name, i) =>
            (name, programs[i], typeof(IntegerSignProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).ToArray(), pairs);
    }
    [Fact]
    public async Task Only_signed_32_and_64_bit_sign_overloads_are_admitted()
    {
        using var files = new ResolverFiles();
        var (_, _, resolver, _, plan) = await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime = resolver;
        var contract = resolver.ResolveAssembly(plan.Bindings[0].Contract.Assembly).Value!;
        using var stream = File.OpenRead(LibraryBuildProducerTests.Request(files, CoreLibraryBuildTests.CorePath("CoreTypes.cs")).References.Single());
        using var image = new PEReader(stream); var reader = image.GetMetadataReader();
        var math = resolver.ResolveType(contract, "System", "Math").Value!;
        int supported = 0, rejected = 0;
        foreach (var handle in reader.GetTypeDefinition(math.Handle).GetMethods())
        {
            if (reader.GetString(reader.GetMethodDefinition(handle).Name) != "Sign") continue;
            var parameter = resolver.ResolveMethod(contract, handle).Value!.Signature.Parameters[0];
            bool expected = parameter is TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 or PrimitiveTypeCode.Int64 };
            var mapped = resolver.ResolveLibraryMethod(plan, contract, handle);
            Assert.Equal(expected, mapped.Success);
            if (expected) ++supported; else ++rejected;
        }
        Assert.Equal(2, supported); Assert.True(rejected > 0);
    }
}
