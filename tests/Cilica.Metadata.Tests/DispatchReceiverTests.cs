using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class DispatchReceiverTests
{
    private static readonly string[] Names=["HashKey","EqualityKey","Polymorphic","Direct","SeparateBounds","Void"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(DispatchReceiverProbe),Names));
    [Fact]
    public async Task Identity_key_hash_does_not_discover_unrelated_comparer_override()
    {
        var program=(await CoreLibraryBuildTests.TranslateFixtures(typeof(DispatchReceiverProbe),"HashKey"))[0];
        Assert.DoesNotContain(program.Flow.Flow.Flow.Methods,m=>m.Source.Name=="System.OrdinalCaseSensitiveComparer::GetHashCode");
    }
    [Fact]
    public async Task Shared_declarations_union_distinct_receiver_bounds()
    {
        var program=(await Programs.Value)[4];
        var source=program.Flow.Flow.Flow.Methods.Single(m=>m.Source.Name=="System.Object::GetHashCode").Source;
        var map=program.Flow.Dispatch.Single(m=>m.Declaration==IrMethodId.From(source));
        Assert.Equal(2,map.ReceiverBounds.Length);
        Assert.Equal(map.ReceiverBounds.OrderBy(t=>t.Token),map.Targets.Select(t=>t.Receiver).OrderBy(t=>t.Token));
        Assert.All(map.Targets,t=>Assert.Equal(map.Declaration,t.Implementation));
    }
    [Theory]
    [InlineData("default")] [InlineData("duplicate")] [InlineData("missing-type")]
    [InlineData("array")] [InlineData("uncovered-call")] [InlineData("missing-target")]
    [InlineData("widened-bound")] [InlineData("wrong-override")]
    public async Task Detached_bounds_cannot_hide_receivers_or_override_choices(string damage)
    {
        var program=(await Programs.Value)[3];
        var flow=program.Flow;
        var source=flow.Flow.Flow.Methods.Single(m=>m.Source.Name=="System.Object::GetHashCode").Source;
        var map=flow.Dispatch.Single(m=>m.Declaration==IrMethodId.From(source));
        var bound=Assert.Single(map.ReceiverBounds);
        var leaf=ObjectTypeId.From(flow.Flow.Flow.Types.References.Types.Single(t=>t.Source.Name=="Cilica.Fixtures.Execution.DispatchHashLeaf").Source);
        var leafTarget=map.Targets.Single(t=>t.Receiver==leaf);
        var changed=damage switch {
            "default"=>map with {ReceiverBounds=default},
            "duplicate"=>map with {ReceiverBounds=[bound,bound]},
            "missing-type"=>map with {ReceiverBounds=[bound with {Token=0x0200ffff}]},
            "array"=>map with {ReceiverBounds=[bound with {ArrayElement=ObjectArrayElement.Int32}]},
            // This map is complete for Leaf, but the call still consumes Base.
            "uncovered-call"=>map with {ReceiverBounds=[leaf],Targets=[leafTarget]},
            "missing-target"=>map with {Targets=map.Targets.Remove(leafTarget)},
            "widened-bound"=>map with {ReceiverBounds=[]},
            _=>map with {Targets=map.Targets.Replace(leafTarget,leafTarget with {Implementation=map.Declaration})}
        };
        var invalid=program with {Flow=flow with {Dispatch=flow.Dispatch.Replace(map,changed)}};
        Assert.Throws<ArgumentException>(()=>ManagedExceptionIrValidator.Validate(invalid));
        Assert.Throws<ArgumentException>(()=>ValueScalarLowering.LowerExceptions(invalid));
    }
    [Fact]
    public async Task An_Object_alias_cannot_reuse_a_narrower_calls_dispatch_map()
    {
        using var files=new ResolverFiles();
        var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(DispatchReceiverProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(DispatchReceiverProbe).GetMethod("Unbounded")!.MetadataToken),implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("no implemented managed body",StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Receiver_bounds_preserve_identity_and_subclass_dispatch(bool optimized)
    {
        var programs=await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,Names.Select((name,i)=>(name,programs[i],typeof(DispatchReceiverProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).ToArray(),expectedStaticRoots:1);
    }
}
