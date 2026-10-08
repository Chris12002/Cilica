using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class CandidateOrdinalIgnoreCaseTests
{
    private static readonly string[] Names=["Compare","EqualityHash","ScalarMapping","ScalarPair","Dictionary","IdentityFaults"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateOrdinalIgnoreCaseProbe),Names));
    [Fact]
    public void Every_scalar_and_lone_surrogate_matches_the_oracle_profile()
    {
        for(int scalar=0;scalar<=0x10ffff;scalar++)
        {
            string source=CandidateOrdinalIgnoreCaseProbe.ScalarText(scalar);
            string sentinel=scalar<=0xffff?"\0":"\ud800\udc00";
            int mapped=Cilica.Runtime.UnicodeOrdinalUpper.Map(scalar);
            string canonical=CandidateOrdinalIgnoreCaseProbe.ScalarText(mapped);
            Assert.Equal(StringComparer.OrdinalIgnoreCase.Compare(source,sentinel),Cilica.Runtime.OrdinalIgnoreCaseText.Compare(source,sentinel));
            Assert.True(StringComparer.OrdinalIgnoreCase.Equals(source,canonical),$"U+{scalar:X} maps incorrectly on this host");
            Assert.Equal(Cilica.Runtime.OrdinalIgnoreCaseText.Hash(source),Cilica.Runtime.OrdinalIgnoreCaseText.Hash(canonical));
            Assert.Equal(source.Length,canonical.Length);
        }
    }
    [Fact]
    public void Seeded_malformed_sequences_match_exact_comparison_and_equality()
    {
        var random=new Random(6091);int[] units=[0,65,97,0x130,0x131,0x17f,0x3c2,0x3a3,0xd800,0xd801,0xdbff,0xdc00,0xdc28,0xdfff,0xffff];
        string Make()=>new string(Enumerable.Range(0,random.Next(1,33)).Select(_=>(char)units[random.Next(units.Length)]).ToArray());
        for(int i=0;i<12000;i++)
        {
            string first=Make(),second=i%2==0?first.ToUpperInvariant():Make();
            Assert.Equal(StringComparer.OrdinalIgnoreCase.Compare(first,second),Cilica.Runtime.OrdinalIgnoreCaseText.Compare(first,second));
            Assert.Equal(StringComparer.OrdinalIgnoreCase.Equals(first,second),Cilica.Runtime.OrdinalIgnoreCaseText.Equal(first,second));
            if(Cilica.Runtime.OrdinalIgnoreCaseText.Equal(first,second))Assert.Equal(Cilica.Runtime.OrdinalIgnoreCaseText.Hash(first),Cilica.Runtime.OrdinalIgnoreCaseText.Hash(second));
        }
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Changed_scalars_and_neighbors_compare_exactly_in_native(bool optimized)
    {
        var points=Enumerable.Range(0,0x110000).Where(c=>Cilica.Runtime.UnicodeOrdinalUpper.Map(c)!=c)
            .SelectMany(c=>new[]{c-1,c,c+1}).Concat(new[]{0,127,128,0x130,0x131,0x17f,0xd800,0xdbff,0xdc00,0xdfff,0xffff,0x10ffff}).Distinct().Order().Select(c=>(c,0)).ToArray();
        foreach(var batch in points.Chunk(192))await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("ScalarMapping",(await Programs.Value)[2],CandidateOrdinalIgnoreCaseProbe.ScalarMapping)],batch,expectedStaticRoots:1);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Every_changed_scalar_has_consistent_native_equality_and_hash(bool optimized)
    {
        var points=Enumerable.Range(0,0x110000).Where(c=>Cilica.Runtime.UnicodeOrdinalUpper.Map(c)!=c).Select(c=>(c,Cilica.Runtime.UnicodeOrdinalUpper.Map(c))).ToArray();
        foreach(var batch in points.Chunk(192))await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("ScalarPair",(await Programs.Value)[3],CandidateOrdinalIgnoreCaseProbe.ScalarPair)],batch,expectedStaticRoots:1);
    }
    [Theory] [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    public async Task Catalog_comparison_and_equality_match_exactly(bool optimized,int index)
    {
        var pairs=(from a in Enumerable.Range(0,48) from b in Enumerable.Range(0,48) select(a,b)).ToArray();
        foreach(var batch in pairs.Chunk(192))await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [(Names[index],(await Programs.Value)[index],typeof(CandidateOrdinalIgnoreCaseProbe).GetMethod(Names[index])!.CreateDelegate<Func<int,int,int>>())],batch,expectedStaticRoots:1);
    }
    [Theory] [InlineData(false,4)] [InlineData(true,4)] [InlineData(false,5)] [InlineData(true,5)]
    public async Task Conditional_dictionary_identity_and_faults_match_dotnet(bool optimized,int index)
    {
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [(Names[index],(await Programs.Value)[index],typeof(CandidateOrdinalIgnoreCaseProbe).GetMethod(Names[index])!.CreateDelegate<Func<int,int,int>>())],expectedStaticRoots:2);
    }
}
