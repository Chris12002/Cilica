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
public sealed class ApprovedCrc32ExecutionTests(ITestOutputHelper output)
{
    // This opt-in is not a sandbox. The documented isolated environment and human
    // execution approval are prerequisites, even if these variables are supplied.
    [Fact(Explicit=true)]
    public async Task Selected_roots_match_every_transcript_observation()
    {
        Assert.Equal("26c5a818a5c7a3d6a622c92d3cd08dba586c263c",Environment.GetEnvironmentVariable("CILICA_APPROVED_CRC32_PIN"));
        string path=Environment.GetEnvironmentVariable("CILICA_CRC32_ASSEMBLY")!;
        Assert.True(Path.IsPathFullyQualified(path)&&File.Exists(path),"Set the approved isolated assembly path.");
        var context=new AssemblyLoadContext("approved-crc32",isCollectible:true);
        try
        {
            var assembly=context.LoadFromAssemblyPath(path);
            var method=assembly.GetType("Cilica.CandidateValidation.Crc32Probe")!.GetMethod("Run")!;
            using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMapContracts(files, includeNetStandard20:true);using var lifetime=resolver;
            using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
            var input=scope.OpenInput(path).Value!;
            output.WriteLine("Stage: translating selected candidate root; no oracle invocation yet.");
            var translated=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(method.MetadataToken),implementation);
            Assert.True(translated.Success,string.Join("; ",translated.Diagnostics));
            output.WriteLine("Stage: translation passed; collecting 41 complete .NET oracle transcripts.");
            var oracle=method.CreateDelegate<Func<int,int,int>>();
            Assert.Equal(unchecked((int)0xcbf43926u),oracle(40,0));
            var observations=new List<(int,int)>();
            for(int scenario=0;scenario<41;scenario++)
            {
                Assert.Equal(1,oracle(scenario,1));
                int expectedFault=scenario<24||scenario>=32?0:scenario<26?1:scenario==29||scenario==30?3:2;
                Assert.Equal(expectedFault,oracle(scenario,2));
                int length=oracle(scenario,-1);Assert.InRange(length,1,2048);
                observations.Add((scenario,-1));for(int index=0;index<length;index++)observations.Add((scenario,index));
            }
            output.WriteLine($"Stage: all 41 oracle scenarios completed; {observations.Count} transcript observations including lengths.");
            foreach(bool optimized in new[]{false,true})
            {
                int completed=0;
                foreach(var batch in System.Linq.Enumerable.Chunk(observations,128))
                {
                    output.WriteLine($"Stage: O{(optimized?2:0)} native batch starting after {completed}/{observations.Count} completed observations.");
                    await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("CRC32",translated.Program!,oracle)],batch,persistentRoots:true);
                    completed+=batch.Length;
                }
                output.WriteLine($"Stage: O{(optimized?2:0)} completed all {completed} observations.");
            }
        }
        finally {context.Unload();}
    }
}
