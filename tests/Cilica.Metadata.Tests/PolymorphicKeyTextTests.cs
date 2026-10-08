using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class PolymorphicKeyTextTests
{
    private static readonly string[] Names=["DefaultNames","Missing","Callbacks","SortedList","SortedDerived","NullOrder","BaseCall"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(PolymorphicKeyTextProbe),Names));
    [Fact]
    public async Task Dynamic_text_validates_at_both_stages()
    { foreach(var p in await Programs.Value) { ManagedExceptionIrValidator.Validate(p); ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(p)); } }
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)]
    public async Task Dynamic_names_slots_callbacks_and_faults_match_dotnet(bool optimized,int group)
    {
        var programs=await Programs.Value;
        var pairs=(from a in Enumerable.Range(0,8) from b in Enumerable.Range(0,8) select(a,b)).ToArray();
        foreach(int i in Enumerable.Range(group*2,2))
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(Names[i],programs[i],typeof(PolymorphicKeyTextProbe).GetMethod(Names[i])!.CreateDelegate<Func<int,int,int>>())],pairs,expectedStaticRoots:Names[i].StartsWith("Sorted",StringComparison.Ordinal)?1:0);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Explicit_base_call_uses_dynamic_default_name(bool optimized)
    { var p=(await Programs.Value)[6]; await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("BaseCall",p,PolymorphicKeyTextProbe.BaseCall)]); }
    [Theory]
    [InlineData("UnsupportedDerivedFormattable", "IFormattable requires provider dispatch")]
    [InlineData("UnsupportedObjectArray", "proven ordinary class receiver")]
    [InlineData("UnsupportedObjectBox", "proven ordinary class receiver")]
    [InlineData("UnsupportedRuntimeType", "reflected-name semantics")]
    public async Task Derived_formattable_is_rejected_after_complete_receiver_discovery(string name, string diagnostic)
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(PolymorphicKeyTextProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(PolymorphicKeyTextProbe).GetMethod(name)!.MetadataToken),implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains(diagnostic,StringComparison.Ordinal));
    }
    [Fact]
    public async Task Exact_default_text_grant_and_private_caller_are_required()
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        var bytes=await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken);
        var manifest=JsonSerializer.Deserialize<LibraryBuildManifest>(bytes,new JsonSerializerOptions{Converters={new JsonStringEnumConverter<LibraryIntrinsicCapability>()}})!;
        var artifact=Assert.Single(manifest.Artifacts);Assert.Single(artifact.Grants.Where(g=>g.Capability==LibraryIntrinsicCapability.ObjectDefaultText));
        var narrowed=manifest with {Artifacts=[artifact with {Grants=[..artifact.Grants.Where(g=>g.Capability!=LibraryIntrinsicCapability.ObjectDefaultText)]}]};
        var deniedBytes=AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
        using(var denied=resolver.CreateTrustedLibraryCompilationScope(plan,deniedBytes,Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!)
        {
            var input=denied.OpenInput(typeof(PolymorphicKeyTextProbe).Assembly.Location).Value!;
            var result=ValueTranslator.TranslateIntegerObjects(denied,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(PolymorphicKeyTextProbe).GetMethod("DefaultNames")!.MetadataToken),implementation);
            Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.LibraryIntrinsicDenied);
        }
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,bytes,built.ManifestSha256).Value!;
        var hostile=new ResolutionAssemblyBuilder(new("HostileDefaultText",new Version(1,0,0,0)));
        var helper=hostile.TypeReference("Cilica.Runtime","ObjectText",hostile.Reference(implementation.Identity));
        var call=hostile.Member(helper,"Default",0,1,0x0e,0x1c);hostile.Type("Test","Caller");
        var root=hostile.Method("Run",[0,0,8],emit:il=>{il.OpCode(ILOpCode.Ldnull);il.Call(call);il.OpCode(ILOpCode.Pop);il.LoadConstantI4(0);il.OpCode(ILOpCode.Ret);});
        var h=scope.OpenInput(files.Write("hostile.dll",hostile.Build())).Value!;
        var rejected=ValueTranslator.TranslateIntegerObjects(scope,h,root,implementation);
        Assert.False(rejected.Success);Assert.Contains(rejected.Diagnostics,d=>d.Message.Contains("lexical declaring scope",StringComparison.Ordinal) || d.Message.Contains("exact private declaration",StringComparison.Ordinal));
    }
    [Theory]
    [InlineData("operand")] [InlineData("result")]
    public async Task Detached_default_text_packets_are_checked_at_both_stages(string mutation)
    {
        var program=(await Programs.Value)[0];var flow=program.Flow.Flow.Flow;
        IrMethod Mutate(IrMethod method)
        {
            var block=method.Blocks.First(b=>b.Values.Any(v=>v.Expression is IrExpression.ObjectDefaultText));
            var statement=block.Statements.OfType<IrStatement.Define>().Single(s=>s.Value.Expression is IrExpression.ObjectDefaultText);
            var call=(IrExpression.ObjectDefaultText)statement.Value.Expression;
            var changed=statement.Value with {Expression=mutation=="operand"?call with {Operand=statement.Value.Id}:call,Type=mutation=="result"?IrType.Int32:statement.Value.Type};
            return method with {Blocks=method.Blocks.Replace(block,block with {Statements=block.Statements.Replace(statement,statement with {Value=changed})})};
        }
        var method=flow.Methods.Single(m=>m.Blocks.Any(b=>b.Values.Any(v=>v.Expression is IrExpression.ObjectDefaultText)));
        var bad=program with {Flow=program.Flow with {Flow=program.Flow.Flow with {Flow=flow with {Methods=flow.Methods.Replace(method,Mutate(method))}}}};
        Assert.Throws<ArgumentException>(()=>ManagedExceptionIrValidator.Validate(bad));
        var scalar=ValueScalarLowering.LowerExceptions(program);var boxes=scalar.Flow.Flow;var objects=boxes.Flow.Flow;
        var scalarMethod=objects.Flow.Methods.Single(m=>m.Source==method.Source);
        var badScalar=scalar with {Flow=scalar.Flow with {Flow=boxes with {Flow=boxes.Flow with {Flow=objects with {Flow=objects.Flow with {Methods=objects.Flow.Methods.Replace(scalarMethod,Mutate(scalarMethod))}}}}}};
        Assert.Throws<ArgumentException>(()=>ScalarManagedExceptionIrValidator.Validate(badScalar));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Default_text_quota_cache_and_instance_boundaries_are_preserved(bool optimized)
    {
        var program=(await Programs.Value)[0];var flow=program.Flow.Flow.Flow;
        var scalar=ValueScalarLowering.LowerExceptions(program);var artifact=ObjectCppEmitter.EmitExceptions(scalar);
        var layout=flow.Types.References.Types.Single(t=>t.Source.Name=="Cilica.Fixtures.Execution.PolyTextDerived");
        var defaultMethod=flow.Methods.Single(m=>m.Source.Name=="System.Object::ToString");
        string type="t_"+layout.Source.ContentHash+"_"+layout.Source.Token.ToString("x8",CultureInfo.InvariantCulture);
        string method="cilica_m_"+defaultMethod.Source.ContentHash+"_"+defaultMethod.Source.Token.ToString("x8",CultureInfo.InvariantCulture);
        string expected=typeof(PolyTextDerived).ToString();
        Assert.Equal(expected,layout.Source.RuntimeName??layout.Source.Name.Replace('/','+'));
        string units=string.Join(",",expected.Select(c=>((ushort)c).ToString(CultureInfo.InvariantCulture)+"u"));
        string source=artifact.Text+$$"""
        #include <cassert>
        using namespace {{artifact.EntryPoint[..^4]}}_closure;
        [[gnu::noinline]] static void run() {
            Instance heap; Context c(heap); heap.collect_before_allocation=true;
            Object* roots[2]={nullptr,nullptr}; Frame frame; RootScope scope(c,frame,roots,2);
            roots[0]=allocate<{{type}}>(c,{{type}}_descriptor); assert(roots[0]!=nullptr);
            const auto before=heap.bytes; const auto count=heap.count;
            heap.object_limit=count;
            assert(default_object_text(c,roots[0])==nullptr && heap.bytes==before && heap.literals==nullptr);
            heap.object_limit=4096; heap.limit=before;
            assert(default_object_text(c,roots[0])==nullptr && heap.bytes==before && heap.literals==nullptr);
            constexpr std::uint16_t expected[]={{{units}}};
            const auto charge=sizeof(StringObject)+sizeof(expected);
            heap.limit=before+charge-1;
            assert(default_object_text(c,roots[0])==nullptr && heap.bytes==before && heap.literals==nullptr);
            heap.limit=before+charge;
            roots[1]=default_object_text(c,roots[0]); assert(roots[1]!=nullptr && heap.count==count+1 && heap.bytes==before+charge);
            assert(string_length(roots[1])==sizeof(expected)/sizeof(expected[0]));
            for(std::uint32_t i=0;i<sizeof(expected)/sizeof(expected[0]);++i) assert(string_code_unit(roots[1],i)==expected[i]);
            assert(default_object_text(c,roots[0])==roots[1] && heap.count==count+1);
            assert(collect(c).status==MarkStatus::complete);
            auto* otherHeap=new Instance; auto* other=new Context(*otherHeap);
            Object* foreign=allocate<{{type}}>(*other,{{type}}_descriptor);assert(foreign!=nullptr);
            LocationArgument args[1];args[0].reference=foreign;
            assert({{method}}(c,args)==nullptr && c.pending==6u);c.pending=0;
            // A malformed root must stop a fresh allocation without publishing a name.
            Frame invalid;Object* invalidRoot=foreign;
            {RootScope bad(c,invalid,&invalidRoot,1); assert(allocate<{{type}}>(c,{{type}}_descriptor)==nullptr && c.pending==6u);}
            c.pending=0;
            Object* foreignName=default_object_text(*other,foreign);assert(foreignName!=nullptr && foreignName!=roots[1]);
            assert(foreignName->owner==otherHeap && roots[1]->owner==&heap);
            delete other;delete otherHeap;
            roots[0]=nullptr;roots[1]=nullptr;
            assert(collect(c).status==MarkStatus::complete && heap.count==1 && heap.bytes==charge);
        }
        int main(){run();}
        """;
        using var files=new ResolverFiles();string dir=files.DirectoryPath("default-text"),cpp=Path.Combine(dir,"text.cpp"),exe=Path.Combine(dir,"text");
        File.WriteAllText(cpp,source);NativeEvidence.Record(cpp,optimized);
        var build=await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX")??"clang++",["-std=c++20","-fno-exceptions","-fno-rtti","-Wall","-Wextra","-Werror",optimized?"-O2":"-O0","-Wframe-larger-than=2048","-fsanitize=address,undefined","-fno-sanitize-recover=all",cpp,"-o",exe],dir);
        Assert.True(build.ExitCode==0,build.Stderr);NativeEvidence.Record(cpp,optimized,exe);
        var run=await NativeProcess.Run(exe,[],dir);Assert.True(run.ExitCode==0,run.Stdout+run.Stderr);Assert.Empty(run.Stderr);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Nonvirtual_calls_cannot_bypass_reference_receiver_proof(bool array)
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var b=new ResolutionAssemblyBuilder(new("DirectDefaultText",new Version(1,0,0,0)));
        var reference=b.Reference(implementation.Identity);var obj=b.TypeReference("System","Object",reference);var integer=b.TypeReference("System","Int32",reference);
        var ctor=b.Member(obj,".ctor",0x20,0,1);var text=b.Member(obj,"ToString",0x20,0,0x0e);
        b.Type("Test","Caller");
        var root=b.Method("Run",[0,0,8],emit:il=>{
            if(array){il.LoadConstantI4(1);il.OpCode(ILOpCode.Newarr);il.Token(integer);}
            else{il.OpCode(ILOpCode.Newobj);il.Token(ctor);}
            il.Call(text);il.OpCode(ILOpCode.Pop);il.LoadConstantI4(0);il.OpCode(ILOpCode.Ret);
        });
        var input=scope.OpenInput(files.Write("direct-default.dll",b.Build())).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,root,implementation);
        Assert.False(result.Success);Assert.Null(result.Program);Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public async Task Detached_runtime_names_obey_literal_budget_before_materialization()
    {
        var program=(await Programs.Value)[0];var flow=program.Flow.Flow.Flow;var references=flow.Types.References;
        var layout=references.Types.Single(t=>t.Source.Name=="Cilica.Fixtures.Execution.PolyTextDerived");
        var changed=layout with {Source=layout.Source with {RuntimeName=new string('x',ObjectStringLimits.MaximumLiteralCodeUnits+1)}};
        var bad=program with {Flow=program.Flow with {Flow=program.Flow.Flow with {Flow=flow with {Types=flow.Types with {References=references with {Types=references.Types.Replace(layout,changed)}}}}}};
        Assert.Throws<ArgumentException>(()=>ManagedExceptionIrValidator.Validate(bad));
    }

}
