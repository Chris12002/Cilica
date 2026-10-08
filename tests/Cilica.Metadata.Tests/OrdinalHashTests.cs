using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class OrdinalHashTests
{
    private static readonly string[] Names = ["Stable","Nulls","Interfaces","Dictionary","Evaluation","ReceiverNull","RawLength","RawUnits"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(OrdinalHashProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)]
    public async Task Ordinal_hash_contracts_and_value_keys_match_dotnet(bool optimized,int group)
    {
        var programs = await Programs.Value;
        foreach (int i in Enumerable.Range(group*2,2))
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,
                [(Names[i],programs[i],typeof(OrdinalHashProbe).GetMethod(Names[i])!.CreateDelegate<Func<int,int,int>>())],
                expectedStaticRoots:Names[i] == "ReceiverNull" ? 0 : 1);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Numeric_hash_matches_owned_pinned_utf16_algorithm(bool optimized)
    {
        var programs = await Programs.Value;
        int[] starts = [0,1,127,0xd800,0xdc00,0xffff];
        var lengths = (from a in Enumerable.Range(0,65) from b in starts select (a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("RawLength",programs[6],LengthHash)],lengths,expectedStaticRoots:1);
        int[] units = [0,1,65,127,255,0x7fff,0x8000,0xd7ff,0xd800,0xdbff,0xdc00,0xdfff,0xe000,0xfffe,0xffff];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("RawUnits",programs[7],UnitHash)],
            (from a in units from b in units select (a,b)).ToArray(),expectedStaticRoots:1);
    }
    private static int LengthHash(int a,int b) => Hash(new string(Enumerable.Range(0,Math.Abs(a%65)).Select(i => (char)(b+i)).ToArray()));
    private static int UnitHash(int a,int b) => Hash(new string([(char)a,'\0',(char)b,'\ud800','\udfff','z']));
    // Independent word-oriented model of pinned GetNonRandomizedHashCode. .NET's
    // randomized public numeric hashes are not a cross-runtime equality contract.
    private static int Hash(string value)
    {
        uint[] words = new uint[(value.Length+1)/2];
        for (int i=0;i<value.Length;++i) words[i/2] |= (uint)value[i] << (16*(i%2));
        uint[] state = [0x15051505u,0x15051505u];
        for (int i=0;i<words.Length;++i)
        {
            int slot = (i==words.Length-1 && value.Length%4 is 1 or 2) ? 1 : i%2;
            state[slot] = unchecked(BitOperations.RotateLeft(state[slot],5)+state[slot]) ^ words[i];
        }
        return unchecked((int)(state[0]+state[1]*1566083941u));
    }
    [Fact]
    public async Task Object_and_parameterless_hash_overloads_remain_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built,_,resolver,implementation,plan) = await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(OrdinalHashProbe).Assembly.Location).Value!;
        foreach (string name in new[] {"UnsupportedObject","UnsupportedComparerHash","UnsupportedString"})
        {
            var result = ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(OrdinalHashProbe).GetMethod(name)!.MetadataToken),implementation);
            Assert.False(result.Success,name); Assert.Null(result.Program); Assert.NotEmpty(result.Diagnostics);
        }
    }
}
