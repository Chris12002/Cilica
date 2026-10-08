using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Cilica.Compiler;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ApprovedAlbireoExecutionTests(ITestOutputHelper output)
{
    // This opt-in is not a sandbox. The documented isolated environment and human
    // execution approval are prerequisites, even if these variables are supplied.
    [Fact(Explicit=true)]
    public async Task Selected_roots_match_every_transcript_observation()
    {
        Assert.Equal("1eabd2fcba515d0bd63881fd2a2879555c43648b",Environment.GetEnvironmentVariable("CILICA_APPROVED_ALBIREO_PIN"));
        string path=Environment.GetEnvironmentVariable("CILICA_ALBIREO_ASSEMBLY")!;
        Assert.True(Path.IsPathFullyQualified(path)&&File.Exists(path),"Set the approved isolated assembly path.");
        string evidence=Environment.GetEnvironmentVariable("CILICA_NATIVE_EVIDENCE_DIRECTORY")!;
        Assert.True(Path.IsPathFullyQualified(evidence),"Set the bounded native-hash evidence directory before execution.");
        Directory.CreateDirectory(evidence);
        Assert.Empty(Directory.GetFileSystemEntries(evidence));
        var context=new AssemblyLoadContext("approved-albireo",isCollectible:true);
        try
        {
            var assembly=context.LoadFromAssemblyPath(path);
            var method=assembly.GetType("Cilica.CandidateValidation.AlbireoDecodeProbe")!.GetMethod("Run")!;
            using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMapContracts(files, includeNetStandard20:true);using var lifetime=resolver;
            using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
            var input=scope.OpenInput(path).Value!;
            output.WriteLine("Stage: translating selected candidate root; no oracle invocation yet.");
            var translated=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(method.MetadataToken),implementation);
            Assert.True(translated.Success,string.Join("; ",translated.Diagnostics));
            output.WriteLine("Stage: translation passed; collecting 32 complete .NET oracle transcripts.");
            var oracle=method.CreateDelegate<Func<int,int,int>>();
            Assert.Equal(1,oracle(0,0)); // null input
            Assert.Equal(0,oracle(1,0)); Assert.Equal(0,oracle(1,1));
            Assert.Equal(0,oracle(1,2)); // distinct freshly allocated empty results
            string[] vectors=["f","fo","foo","foob","fooba","foobar","foobar"];
            for(int i=0;i<vectors.Length;i++)
            {
                Assert.Equal(0,oracle(i+2,0));Assert.Equal(vectors[i].Length,oracle(i+2,1));
                for(int j=0;j<vectors[i].Length;j++)Assert.Equal((int)vectors[i][j],oracle(i+2,j+6));
            }
            var observations=new List<(int,int)>();
            var transcript=new List<int[]>();
            var frozen=new Dictionary<(int,int),int>();
            for(int scenario=0;scenario<32;scenario++)
            {
                Assert.InRange(oracle(scenario,0),0,4);
                int length=oracle(scenario,-1);Assert.InRange(length,1,4096);
                for(int index=-1;index<length;index++)
                {
                    int value=index==-1?length:oracle(scenario,index);
                    observations.Add((scenario,index));transcript.Add([scenario,index,value]);frozen.Add((scenario,index),value);
                }
            }
            output.WriteLine($"Stage: all 32 oracle scenarios completed; {observations.Count} transcript observations including lengths.");
            string transcriptPath=Path.Combine(Directory.GetParent(Path.TrimEndingDirectorySeparator(evidence))!.FullName,"oracle-observations.json");
            await File.WriteAllTextAsync(transcriptPath,System.Text.Json.JsonSerializer.Serialize(transcript),TestContext.Current.CancellationToken);
            Func<int,int,int> frozenOracle=(scenario,index)=>frozen[(scenario,index)];
            foreach(bool optimized in new[]{false,true})
            {
                int completed=0;
                foreach(var batch in System.Linq.Enumerable.Chunk(observations,128))
                {
                    output.WriteLine($"Stage: O{(optimized?2:0)} native batch starting after {completed}/{observations.Count} completed observations.");
                    // Conditional predicate caches: require stable survivors across repeats.
                    await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("AlbireoDecode",translated.Program!,frozenOracle)],batch,persistentRoots:true);
                    completed+=batch.Length;
                }
                output.WriteLine($"Stage: O{(optimized?2:0)} completed all {completed} observations.");
            }
            Assert.True(Directory.Exists(evidence));
            var receipts=Directory.GetFiles(evidence,"*.json");
            Assert.Equal(2*((observations.Count+127)/128),receipts.Length);
            foreach(string receipt in receipts)
            {
                using var document=System.Text.Json.JsonDocument.Parse(File.ReadAllText(receipt));
                Assert.Equal(64,document.RootElement.GetProperty("source_sha256").GetString()!.Length);
                Assert.Equal(64,document.RootElement.GetProperty("executable_sha256").GetString()!.Length);
            }
            output.WriteLine($"Stage: retained {receipts.Length} source/executable hash pairs before native temporary cleanup.");
        }
        finally {context.Unload();}
    }
}
