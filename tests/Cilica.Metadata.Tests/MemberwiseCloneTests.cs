using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cilica.Metadata.Resolution;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class MemberwiseCloneTests
{
    private static readonly string[] Names=["Fields","References","Values","NoConstructor","Repeated","Null","Empty","Generic"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(MemberwiseCloneProbe),Names));
    [Fact]
    public async Task Sealed_clone_probes_translate()=>Assert.Equal(Names.Length,(await Programs.Value).Length);
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)]
    public async Task Shallow_clone_fields_identity_and_faults_match_dotnet(bool optimized,int group)
    {
        var programs=await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,Names.Select((name,i)=>(name,programs[i],typeof(MemberwiseCloneProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*3).Take(3).ToArray());
    }

    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> WidePrograms=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(MemberwiseCloneProbe),["WideFields","WideQuota"]));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Wide_clone_fields_and_shared_references_match_dotnet(bool optimized)
    {
        var program=(await WidePrograms.Value)[0];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("WideFields",program,MemberwiseCloneProbe.WideFields)]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Wide_clone_preserves_identity_across_native_translation_units(bool optimized)
    {
        var program=(await WidePrograms.Value)[0];
        var module=ObjectCppEmitter.EmitExceptionModule(ValueScalarLowering.LowerExceptions(program),methodsPerUnit:1);
        using var files=new ResolverFiles();string dir=files.DirectoryPath("wide-clone-module");
        foreach(var file in module.Sources.Prepend(module.Header))
            await File.WriteAllTextAsync(Path.Combine(dir,file.Name),file.Text,TestContext.Current.CancellationToken);
        string symbol="cilica_m_"+module.Source.ContentHash+"_"+module.Source.Token.ToString("x8",CultureInfo.InvariantCulture);
        var text=new System.Text.StringBuilder($$"""
            #include "{{module.Header.Name}}"
            #include <cassert>
            using namespace {{module.EntryPoint[..^4]}}_closure;
            [[gnu::noinline]] static void check(int a,int b,int expected) {
                for(bool forced : {false,true}) {
                    Instance heap;heap.collect_before_allocation=forced;Context c(heap);
                    LocationArgument args[2]{};args[0].bits=std::bit_cast<std::uint32_t>(a);args[1].bits=std::bit_cast<std::uint32_t>(b);
                    for(int repeat=0;repeat<3;++repeat) {
                        assert({{symbol}}(c,args)==expected && c.pending==0 && c.exception==nullptr);
                        assert(c.top==nullptr && c.borrows==nullptr && c.used==0 && c.depth==0);
                        assert(collect(c).status==MarkStatus::complete && heap.count==1);
                    }
                }
            }
            int main() {
            """);
        foreach(var (a,b) in new[]{(0,0),(1,-1),(-1,1),(int.MinValue,int.MaxValue),(int.MaxValue,int.MinValue)})
            text.Append(CultureInfo.InvariantCulture,$"check({a},{b},{MemberwiseCloneProbe.WideFields(a,b)});\n");
        text.Append("}\n");string main=Path.Combine(dir,"main.cpp"),exe=Path.Combine(dir,"clone");
        await File.WriteAllTextAsync(main,text.ToString(),TestContext.Current.CancellationToken);
        var objects=new List<string>();
        foreach(string source in module.Sources.Select(f=>Path.Combine(dir,f.Name)).Append(main))
        {
            string obj=source+".o";NativeEvidence.Record(source,optimized);
            var build=await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX")??"clang++",
                ["-std=c++20","-fno-exceptions","-fno-rtti","-Wall","-Wextra","-Werror",optimized?"-O2":"-O0","-Wframe-larger-than=2048","-fsanitize=address,undefined","-fno-sanitize-recover=all","-c",source,"-o",obj],dir);
            Assert.True(build.ExitCode==0,build.Stderr);objects.Add(obj);
        }
        var link=await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX")??"clang++",["-fsanitize=address,undefined",..objects,"-o",exe],dir);
        Assert.True(link.ExitCode==0,link.Stderr);NativeEvidence.Record(main,optimized,exe);
        var run=await NativeProcess.Run(exe,[],dir);Assert.True(run.ExitCode==0,run.Stdout+run.Stderr);Assert.Empty(run.Stderr);
    }

    [Fact]
    public async Task Clone_requires_exact_grant_and_sealed_owner()
    {
        using var files=new ResolverFiles();
        var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime=resolver;
        var bytes=await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken);
        var manifest=JsonSerializer.Deserialize<LibraryBuildManifest>(bytes,new JsonSerializerOptions {Converters={new JsonStringEnumConverter<LibraryIntrinsicCapability>()}})!;
        var artifact=Assert.Single(manifest.Artifacts);
        var grant=Assert.Single(artifact.Grants.Where(g=>g.Capability==LibraryIntrinsicCapability.ObjectMemberwiseClone));
        var reduced=AssemblyResolver.SerializeLibraryBuildManifest(manifest with {Artifacts=[artifact with {Grants=artifact.Grants.Remove(grant)}]});
        using var denied=resolver.CreateTrustedLibraryCompilationScope(plan,reduced,Convert.ToHexStringLower(SHA256.HashData(reduced))).Value!;
        var input=resolver.OpenInput(typeof(MemberwiseCloneProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name)=>(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(MemberwiseCloneProbe).GetMethod(name)!.MetadataToken);
        var result=ValueTranslator.TranslateIntegerObjects(denied,input,Method("Fields"),implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id==Cilica.Metadata.DiagnosticIds.LibraryIntrinsicDenied);
        using var allowed=resolver.CreateTrustedLibraryCompilationScope(plan,bytes,built.ManifestSha256).Value!;
        var open=ValueTranslator.TranslateIntegerObjects(allowed,input,Method("UnsupportedOpen"),implementation);
        Assert.False(open.Success);Assert.Contains(open.Diagnostics,d=>d.Message.Contains("sealed application class",StringComparison.Ordinal));
        var legacy=ValueTranslator.TranslateExceptions(allowed,input,Method("Fields"),implementation);
        Assert.False(legacy.Success);
    }
    [Theory]
    [InlineData("type")] [InlineData("operand")] [InlineData("result")]
    public async Task Both_stages_reject_forged_clone_packets(string mutation)
    {
        var program=(await Programs.Value)[0];var flow=program.Flow.Flow.Flow;
        IrMethod Mutate(IrMethod method)
        {
            var block=method.Blocks.First(b=>b.Values.Any(v=>v.Expression is IrExpression.ObjectClone));
            var statement=block.Statements.OfType<IrStatement.Define>().Single(s=>s.Value.Expression is IrExpression.ObjectClone);
            var clone=(IrExpression.ObjectClone)statement.Value.Expression;
            var changed=statement.Value with {Expression=mutation=="type"?clone with {Type=flow.Types.References.ObjectBase}:mutation=="operand"?clone with {Operand=statement.Value.Id}:clone,
                Type=mutation=="result"?IrType.Int32:statement.Value.Type};
            return method with {Blocks=method.Blocks.Replace(block,block with {Statements=block.Statements.Replace(statement,statement with {Value=changed})})};
        }
        var method=flow.Methods.Single(m=>m.Blocks.Any(b=>b.Values.Any(v=>v.Expression is IrExpression.ObjectClone)));
        var bad=program with {Flow=program.Flow with {Flow=program.Flow.Flow with {Flow=flow with {Methods=flow.Methods.Replace(method,Mutate(method))}}}};
        Assert.Throws<ArgumentException>(()=>ManagedExceptionIrValidator.Validate(bad));
        var scalar=ValueScalarLowering.LowerExceptions(program);var boxes=scalar.Flow.Flow;var objects=boxes.Flow.Flow;
        var scalarMethod=objects.Flow.Methods.Single(m=>m.Source==method.Source);
        var badScalar=scalar with {Flow=scalar.Flow with {Flow=boxes with {Flow=boxes.Flow with {Flow=objects with {Flow=objects.Flow with {Methods=objects.Flow.Methods.Replace(scalarMethod,Mutate(scalarMethod))}}}}}};
        Assert.Throws<ArgumentException>(()=>ScalarManagedExceptionIrValidator.Validate(badScalar));
    }
    [Theory]
    [InlineData(false,false)] [InlineData(true,false)] [InlineData(false,true)] [InlineData(true,true)]
    public async Task Clone_quota_recovery_and_header_ownership_are_preserved(bool optimized,bool wide)
    {
        var program=wide?(await WidePrograms.Value)[1]:(await CoreLibraryBuildTests.TranslateFixtures(typeof(MemberwiseCloneProbe),["Quota"]))[0];var scalar=ValueScalarLowering.LowerExceptions(program);
        var clone=Assert.Single(scalar.Flow.Flow.Flow.Flow.Flow.Methods.SelectMany(m=>m.Blocks).SelectMany(b=>b.Values).Select(v=>v.Expression).OfType<IrExpression.ObjectClone>());
        var artifact=ObjectCppEmitter.EmitExceptions(scalar);
        string type="t_"+clone.Type.ContentHash+"_"+clone.Type.Token.ToString("x8",System.Globalization.CultureInfo.InvariantCulture);
        string method="cilica_m_"+program.Flow.Flow.Flow.Entry.ContentHash+"_"+program.Flow.Flow.Flow.Entry.Token.ToString("x8",System.Globalization.CultureInfo.InvariantCulture);
        string text=artifact.Text+$$"""
        #include <cassert>
        int main() {
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            Instance heap;heap.collect_before_allocation=true;Context c(heap);
            Object* source=allocate<{{type}}>(c,{{type}}_descriptor);assert(source!=nullptr);
            source->synchronization=123u;
            {
                Object* roots[2]={source,nullptr};Frame frame;RootScope scope(c,frame,roots,2);
                heap.object_limit=heap.count;
                assert({{type}}_clone(c,source)==nullptr && c.pending==0u && heap.count==1);
                assert(source->synchronization==123u && heap.owns(source));
                c.pending=0;heap.object_limit=4096;
                heap.limit=heap.bytes;
                assert({{type}}_clone(c,source)==nullptr && c.pending==0u && heap.count==1);
                c.pending=0;heap.limit=1048576;
                roots[1]={{type}}_clone(c,source);
                assert(roots[1]!=nullptr && roots[1]!=source && c.pending==0);
                assert(roots[1]->type==source->type && roots[1]->owner==&heap && roots[1]->next==source);
                assert(roots[1]->synchronization==0 && roots[1]->mark_next==nullptr && !roots[1]->marked);
                assert(source->synchronization==123u && heap.count==2);
                assert(collect(c).status==MarkStatus::complete && heap.count==2);
            }
            assert(collect(c).status==MarkStatus::complete && heap.count==0);
            assert(c.used==0 && c.top==nullptr && c.borrows==nullptr);
            Instance managed_heap;managed_heap.collect_before_allocation=true;
            Context initialize(managed_heap);LocationArgument args[2];args[0].bits=0xffffffffu;args[1].bits=17u;
            assert({{method}}(initialize,args)==0 && initialize.pending==0);
            assert(collect(initialize).status==MarkStatus::complete);
            const auto permanent=managed_heap.count;
            args[0].bits=0;assert({{method}}(initialize,args)==1 && initialize.pending==0);
            assert(collect(initialize).status==MarkStatus::complete);
            const auto retained=managed_heap.count;assert(retained>permanent);
            managed_heap.object_limit=retained;args[0].bits=1u;Context failure(managed_heap);
            (void){{method}}(failure,args);
            assert(failure.pending==5u && failure.used==0 && failure.top==nullptr && failure.borrows==nullptr);
            managed_heap.object_limit=4096;Context recovery(managed_heap);
            assert({{method}}(recovery,args)==17 && recovery.pending==0);
            assert(collect(recovery).status==MarkStatus::complete && managed_heap.count==retained);
            args[0].bits=0xffffffffu;assert({{method}}(recovery,args)==0 && recovery.pending==0);
            assert(collect(recovery).status==MarkStatus::complete && managed_heap.count==permanent);
        }
        """;
        using var files=new ResolverFiles();var dir=files.DirectoryPath("clone");var cpp=Path.Combine(dir,"clone.cpp");var exe=Path.Combine(dir,"clone");
        await File.WriteAllTextAsync(cpp,text,TestContext.Current.CancellationToken);
        NativeEvidence.Record(cpp,optimized);
        var build=await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX")??"clang++",["-std=c++20","-fno-exceptions","-fno-rtti","-Wall","-Wextra","-Werror",optimized?"-O2":"-O0","-Wframe-larger-than=2048","-fsanitize=address,undefined","-fno-sanitize-recover=all",cpp,"-o",exe],dir);
        Assert.True(build.ExitCode==0,build.Stderr);
        NativeEvidence.Record(cpp,optimized,exe);
        var run=await NativeProcess.Run(exe,[],dir);Assert.True(run.ExitCode==0,run.Stdout+run.Stderr);Assert.Empty(run.Stderr);
    }

    [Fact]
    public async Task Cloning_this_before_base_initialization_is_rejected()
    {
        using var files=new ResolverFiles();
        var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var b=new ResolutionAssemblyBuilder(new("CloneBeforeBase",new Version(1,0,0,0)));
        var owner=b.TypeReference("System","Object",b.Reference(implementation.Identity));
        var parent=b.Member(owner,".ctor",0x20,0,1);var clone=b.Member(owner,"MemberwiseClone",0x20,0,0x1c);
        b.Type("Test","Payload",baseType:owner,attributes:TypeAttributes.Public|TypeAttributes.Sealed|TypeAttributes.BeforeFieldInit);
        var ctor=b.Method(".ctor",[0x20,0,1],attributes:MethodAttributes.Public|MethodAttributes.HideBySig|MethodAttributes.SpecialName|MethodAttributes.RTSpecialName,emit:il=>{
            il.LoadArgument(0);il.Call(clone);il.OpCode(System.Reflection.Metadata.ILOpCode.Pop);
            il.LoadArgument(0);il.Call(parent);il.OpCode(System.Reflection.Metadata.ILOpCode.Ret);
        });
        var root=b.Method("Run",[0,0,8],emit:il=>{il.OpCode(System.Reflection.Metadata.ILOpCode.Newobj);il.Token(ctor);il.OpCode(System.Reflection.Metadata.ILOpCode.Pop);il.LoadConstantI4(1);il.OpCode(System.Reflection.Metadata.ILOpCode.Ret);});
        var input=resolver.OpenInput(files.Write("before-base.dll",b.Build())).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,root,implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("constructor",StringComparison.OrdinalIgnoreCase));
    }
}
