from pathlib import Path
r=Path('/Users/christian/.codex/worktrees/alchemy-replay-baseline/Cilica')
(r/'tests/Fixtures/Cilica.Fixtures.Execution/CandidateCharacterArrayProbe.cs').write_text('''using System;
using System.Linq;
using System.Text;
namespace Cilica.Fixtures.Execution
{
    // Owned API/composition probes only; no external decoder implementation.
    public static class CandidateCharacterArrayProbe
    {
        public static string Source(int scenario)
        {
            if (scenario == 0) return "";
            if (scenario == 1) return "\\0";
            if (scenario == 2) return "A\\0\\ud800\\udfffZ\\uffff";
            if (scenario == 3) return "\\ud800x\\udfff";
            if (scenario == 4) return "aAaA==";
            if (scenario == 5) return " \\t\\u00a0\\u3000";
            return "copy independent";
        }
        public static int Payload(int scenario, int index)
        {
            string source = Source(scenario);
            char[] first = source.ToCharArray();
            char[] second = source.ToCharArray();
            string later = new StringBuilder().Append('!').ToString();
            if (index == -1) return first.Length;
            if (index == -2) return (object)first == (object)second ? 1 : 0;
            if (index == -3) return (object)first == (object)Array.Empty<char>() ? 1 : 0;
            if (index == -4) return source.Length;
            return index >= 0 && index < first.Length ? first[index] : later[0];
        }
        public static int Independent(int scenario, int index)
        {
            string source = Source(scenario);
            char[] first = source.ToCharArray();
            char[] second = source.ToCharArray();
            for (int i = 0; i < first.Length; ++i) first[i] = '\\uffff';
            string later = new StringBuilder().Append('!').ToString();
            if (index == -1) return second.Length;
            if (index == -2) return (object)first == (object)second ? 1 : 0;
            if (index >= 0 && index < second.Length)
                return second[index] == source[index] ? second[index] : -100;
            return later[0];
        }
        public static int AllUnits(int first, int unused)
        {
            var source = new StringBuilder();
            for (int i = 0; i < 32; ++i) source.Append(unchecked((char)(first + i)));
            string text = source.ToString();
            char[] result = text.ToCharArray();
            if (result.Length != 32) return -1;
            for (int i = 0; i < result.Length; ++i)
                if (result[i] != unchecked((char)(first + i))) return i + 1;
            return 0;
        }
        public static string Receiver(ref int trace, int mode)
        {
            trace = unchecked(trace * 10 + 1);
            if (mode == 0) return null;
            if (mode == 1) throw new InvalidOperationException("receiver");
            return "ab";
        }
        public static int ReceiverFault(int mode, int value)
        {
            int trace = value;
            try { return unchecked(trace * 31 + Receiver(ref trace, mode).ToCharArray().Length + trace * 17); }
            catch (NullReferenceException) { return unchecked(trace * 23); }
            catch (InvalidOperationException) { return unchecked(trace * 29); }
        }
        public static int ReadOnlyReference(int scenario, int index)
        {
            string input = Source(scenario);
            return Read(in input, index);
        }
        public static int Read(in string input, int index)
        {
            char[] units = input.TrimEnd(new char[] { '=' }).ToCharArray();
            return index < 0 ? units.Length : index < units.Length ? units[index] : -1;
        }
        public static bool IsLetterA(char value) => value == 'A';
        public static int CharacterPredicate(int scenario, int unused)
        {
            char[] units = new char[] { unchecked((char)scenario), 'b' };
            Func<char, bool> predicate = new Func<char, bool>(IsLetterA);
            return units.Any(predicate) ? 1 : 0;
        }
        public static int MissingSection(int start, int length) => "abcd".ToCharArray(start, length).Length;
    }
}
''')
(r/'tests/Cilica.Metadata.Tests/CandidateCharacterArrayTests.cs').write_text('''using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class CandidateCharacterArrayTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateCharacterArrayProbe), "Payload", "Independent", "AllUnits", "ReceiverFault"));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Copies_payload_independence_empty_identity_and_faults_match_dotnet(bool optimized)
    {
        var p = await Programs.Value;
        var pairs = (from scenario in Enumerable.Range(0,7) from index in Enumerable.Range(-4,24) select (scenario,index)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("CharacterPayload",p[0],CandidateCharacterArrayProbe.Payload), ("CharacterIndependence",p[1],CandidateCharacterArrayProbe.Independent)],pairs);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("CharacterReceiver",p[3],CandidateCharacterArrayProbe.ReceiverFault)]);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Every_utf16_code_unit_is_copied_exactly(bool optimized)
    {
        var p = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("CharacterUnits",p[2],CandidateCharacterArrayProbe.AllUnits)],Enumerable.Range(0,2048).Select(i=>(i*32,0)).ToArray());
    }
    [Theory]
    [InlineData("ReadOnlyReference",false)] [InlineData("ReadOnlyReference",true)]
    [InlineData("CharacterPredicate",false)] [InlineData("CharacterPredicate",true)]
    public async Task Minimal_decoder_compositions_match_dotnet(string name,bool optimized)
    {
        var p = await CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateCharacterArrayProbe),name);
        var oracle = typeof(CandidateCharacterArrayProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>();
        var pairs = name=="ReadOnlyReference"
            ? (from scenario in Enumerable.Range(0,7) from index in Enumerable.Range(-1,21) select(scenario,index)).ToArray()
            : new[]{(0,0),(65,0),(97,0),(0xd800,0),(0xffff,0),(-1,0)};
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(name,p[0],oracle)],pairs);
    }
    [Fact]
    public async Task Section_overload_remains_explicitly_diagnostic()
    {
        using var files=new ResolverFiles();
        var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=resolver.OpenInput(typeof(CandidateCharacterArrayProbe).Assembly.Location).Value!;
        var method=(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CandidateCharacterArrayProbe).GetMethod("MissingSection")!.MetadataToken);
        var translated=ValueTranslator.TranslateIntegerObjects(scope,input,method,implementation);
        Assert.False(translated.Success);
        Assert.Contains(translated.Diagnostics,d=>d.Id=="CIL3010" && d.Message.Contains("ToCharArray",StringComparison.Ordinal));
    }
}
''')
print('Added owned ToCharArray and minimal byref/predicate probes.')
