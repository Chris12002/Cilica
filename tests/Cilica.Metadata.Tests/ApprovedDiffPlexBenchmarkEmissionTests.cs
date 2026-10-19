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
public sealed class ApprovedDiffPlexBenchmarkEmissionTests(ITestOutputHelper output)
{
    // This opt-in is not a sandbox. The documented isolated environment and human
    // execution approval are prerequisites, even if these variables are supplied.
    [Fact(Explicit=true)]
    public async Task Emit_approved_benchmark()
    {
        Assert.Equal("f500e73f28e28813f942f1f16dec5a34b40e48e3",Environment.GetEnvironmentVariable("CILICA_APPROVED_DIFFPLEX_PIN"));
        string path=Environment.GetEnvironmentVariable("CILICA_DIFFPLEX_ASSEMBLY")!;
        Assert.True(Path.IsPathFullyQualified(path)&&File.Exists(path),"Set the approved isolated assembly path.");
        var context=new AssemblyLoadContext("approved-diffplex",isCollectible:true);
        try
        {
            var assembly=context.LoadFromAssemblyPath(path);
            var method=assembly.GetType("Cilica.CandidateBenchmark.BenchProbe")!.GetMethod("Run")!;
            using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMapContracts(files, includeNetStandard20:true);using var lifetime=resolver;
            // Optional investigation evidence: keep the exact owned core whose
            // metadata tokens appear in generated symbols before temp cleanup.
            if (Environment.GetEnvironmentVariable("CILICA_BENCH_CORE_CAPTURE") is { Length: > 0 } capture)
            {
                Assert.True(Path.IsPathFullyQualified(capture) && !Directory.Exists(capture));
                Directory.CreateDirectory(capture);
                foreach (string file in Directory.GetFiles(Path.GetDirectoryName(built.AssemblyPath)!))
                    File.Copy(file, Path.Combine(capture, Path.GetFileName(file)));
            }
            using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
            var input=scope.OpenInput(path).Value!;
            output.WriteLine("Emitting benchmark C++ only; timing belongs to separate Release processes.");
            var translated=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(method.MetadataToken),implementation);
            Assert.True(translated.Success,string.Join("; ",translated.Diagnostics));
            var artifact=ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(translated.Program!));
            var entry=translated.Program!.Flow.Flow.Flow.Entry;
            string symbol="cilica_m_"+entry.ContentHash+"_"+entry.Token.ToString("x8",System.Globalization.CultureInfo.InvariantCulture);
            string template=await File.ReadAllTextAsync(Environment.GetEnvironmentVariable("CILICA_BENCH_TEMPLATE")!,TestContext.Current.CancellationToken);
            string destination=Environment.GetEnvironmentVariable("CILICA_BENCH_CPP")!;
            Assert.True(Path.IsPathFullyQualified(destination));
            await File.WriteAllTextAsync(destination,artifact.Text+template.Replace("BENCH_NAMESPACE",artifact.EntryPoint[..^4]+"_closure",StringComparison.Ordinal).Replace("BENCH_SYMBOL",symbol,StringComparison.Ordinal),TestContext.Current.CancellationToken);
        }
        finally {context.Unload();}
    }
}
