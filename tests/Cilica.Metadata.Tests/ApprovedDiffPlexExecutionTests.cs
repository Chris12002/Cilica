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
public sealed class ApprovedDiffPlexExecutionTests
{
    // This opt-in is not a sandbox. The documented isolated environment and human
    // execution approval are prerequisites, even if these variables are supplied.
    [Fact(Explicit=true)]
    public async Task Selected_roots_match_every_transcript_observation()
    {
        Assert.Equal("f500e73f28e28813f942f1f16dec5a34b40e48e3",Environment.GetEnvironmentVariable("CILICA_APPROVED_DIFFPLEX_PIN"));
        string path=Environment.GetEnvironmentVariable("CILICA_DIFFPLEX_ASSEMBLY")!;
        Assert.True(Path.IsPathFullyQualified(path)&&File.Exists(path),"Set the approved isolated assembly path.");
        var context=new AssemblyLoadContext("approved-diffplex",isCollectible:true);
        try
        {
            var assembly=context.LoadFromAssemblyPath(path);
            var method=assembly.GetType("Cilica.CandidateValidation.Probe")!.GetMethod("Run")!;
            using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
            using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
            var input=scope.OpenInput(path).Value!;
            var translated=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(method.MetadataToken),implementation);
            Assert.True(translated.Success,string.Join("; ",translated.Diagnostics));
            var oracle=method.CreateDelegate<Func<int,int,int>>();
            var observations=new List<(int,int)>();
            for(int scenario=0;scenario<64;scenario++)
            {
                int length=oracle(scenario,-1);Assert.InRange(length,1,2048);
                observations.Add((scenario,-1));for(int index=0;index<length;index++)observations.Add((scenario,index));
            }
            foreach(bool optimized in new[]{false,true})
                foreach(var batch in System.Linq.Enumerable.Chunk(observations,128))
                    await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("DiffPlex",translated.Program!,oracle)],batch,persistentRoots:true);
        }
        finally {context.Unload();}
    }
}
