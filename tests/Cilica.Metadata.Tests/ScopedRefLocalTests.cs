using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ScopedRefLocalTests
{
    private static readonly string[] Names=["Frame","Aliases","Heap","Wide","Reference","Parameter","FaultCleanup"];
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    public async Task Scoped_borrows_preserve_aliases_owner_roots_and_cleanup(bool optimized,int group)
    {
        var names=Names.Skip(group*2).Take(2).ToArray();
        var programs=await CoreLibraryBuildTests.TranslateFixtures(typeof(ScopedRefLocalProbe),names);
        foreach(var program in programs)
        {
            var flow=program.Flow.Flow.Flow;
            Assert.Contains(flow.Methods,m=>m.Locals.Any(t=>t.Kind==IrTypeKind.ManagedLocation));
            var roots=ValueRootAssignment.Assign(program);
            foreach(var method in flow.Methods)
                for(int i=0;i<method.Locals.Length;++i)
                    if(method.Locals[i].Kind==IrTypeKind.ManagedLocation)
                        Assert.Contains(roots.Single(r=>r.Method==IrMethodId.From(method.Source)).Slots,
                            s=>s.Kind==ObjectRootKind.Local&&s.Index==i&&s.LocationOwner);
        }
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,names.Select((name,i)=>
            (name,programs[i],typeof(ScopedRefLocalProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).ToArray());
    }
    [Fact]
    public async Task Reassignment_regions_and_inline_value_locals_stay_diagnostic()
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(ScopedRefLocalProbe).Assembly.Location).Value!;
        foreach(string name in new[]{"UnsupportedReassignment","UnsupportedRegion","UnsupportedValue"})
        {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ScopedRefLocalProbe).GetMethod(name)!.MetadataToken),implementation);
            Assert.False(result.Success,name);Assert.Null(result.Program);Assert.NotEmpty(result.Diagnostics);
        }
    }
}
