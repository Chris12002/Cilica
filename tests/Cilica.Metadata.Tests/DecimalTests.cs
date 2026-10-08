using System;
using System.Linq;
using System.Globalization;
using System.Threading.Tasks;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Library;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class DecimalTests
{
    private static readonly string[] Names=["LibraryMultiply","LibraryDivide","Route","IntegerMultiply","IntegerDivide","CopyAndRepeat","FaultText"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(DecimalProbe),Names));
    [Fact]
    public async Task Decimal_bodies_validate_before_and_after_value_lowering()
    { foreach(var p in await Programs.Value){ManagedExceptionIrValidator.Validate(p);ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(p));} }
    [Theory] [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)]
    public async Task Native_decimal_matches_dotnet_with_collection_and_frame_limits(bool optimized,int group)
    {
        var programs=await Programs.Value;
        var probes=Names.Select((name,i)=>(name,programs[i],typeof(DecimalProbe).GetMethod(name.Replace("Library", "", StringComparison.Ordinal))!.CreateDelegate<Func<int,int,int>>()));
        if(group<2)
        {
            int[] cases=[0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24+3,24+6,24+21,13*24+1,13*24+3,14*24+3,18*24+3,22*24+3,23*24+4,10*24+10,22*24+22,4*24+4,19*24+22,5*24+22,7*24+8,8*24+7,3*24+3];
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,probes.Skip(group).Take(1).ToArray(),(from index in cases.Concat(Enumerable.Range(0,48).Select(i=>(i*37+101)%576)).Distinct() from position in Enumerable.Range(-5,37) select(index,position)).ToArray());
        }
        else await IntegerObjectTranslationTests.VerifyTranslated(optimized,probes.Skip(2).ToArray());
    }
    internal static DecimalParts Parts(decimal value)
    { var b=decimal.GetBits(value);return new((uint)b[0],(uint)b[1],(uint)b[2],b[3]<0,(b[3]>>16)&255); }
    private static void Equal(decimal expected,DecimalParts actual)
    {
        var b=decimal.GetBits(expected); Assert.Equal((uint)b[0],actual.Low);Assert.Equal((uint)b[1],actual.Mid);Assert.Equal((uint)b[2],actual.High);Assert.Equal(b[3],actual.Flags);
        Assert.Equal(expected.ToString(CultureInfo.InvariantCulture),DecimalArithmetic.InvariantText(actual));
    }
    private static void Operation(decimal a,decimal b,bool divide)
    {
        decimal expected=0;var error=Record.Exception(()=>expected=divide?a/b:a*b);
        DecimalParts actual=default;var failure=Record.Exception(()=>actual=divide?DecimalArithmetic.Divide(Parts(a),Parts(b)):DecimalArithmetic.Multiply(Parts(a),Parts(b)));
        Assert.Equal(error?.GetType(),failure?.GetType());Assert.Equal(error?.Message,failure?.Message);if(error is null)Equal(expected,actual);
    }
    [Fact]
    public void Seeded_managed_arithmetic_matches_bits_scale_sign_text_and_faults()
    {
        var random=new Random(83167);var bytes=new byte[12];
        for(int i=0;i<10000;i++)
        {
            random.NextBytes(bytes);int lo=BitConverter.ToInt32(bytes,0),mid=BitConverter.ToInt32(bytes,4),hi=BitConverter.ToInt32(bytes,8);
            if(i%3==0)mid=hi=0;var a=new decimal(lo,mid,hi,i%2==0,(byte)random.Next(29));
            random.NextBytes(bytes);lo=BitConverter.ToInt32(bytes,0);mid=BitConverter.ToInt32(bytes,4);hi=BitConverter.ToInt32(bytes,8);
            if(i%3==1)mid=hi=0;if(i%11==0)lo=mid=hi=0;
            var b=new decimal(lo,mid,hi,i%5==0,(byte)random.Next(29));Equal(a,Parts(a));Operation(a,b,false);Operation(a,b,true);
        }
        for(int i=0;i<24;i++)for(int j=0;j<24;j++){Operation(DecimalProbe.Operand(i),DecimalProbe.Operand(j),false);Operation(DecimalProbe.Operand(i),DecimalProbe.Operand(j),true);}
        foreach(int value in new[]{int.MinValue,-1,0,1,int.MaxValue})Equal(value,DecimalArithmetic.FromInt32(value));
    }
    [Theory]
    [InlineData("UnsupportedDefault")] [InlineData("UnsupportedNull")] [InlineData("UnsupportedAddition")] [InlineData("UnsupportedParts")] [InlineData("UnsupportedConstant")]
    public async Task APIs_and_providers_outside_the_declared_surface_remain_diagnostic(string name)
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(DecimalProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(DecimalProbe).GetMethod(name)!.MetadataToken),implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.ExecutionUnsupported || d.Id==DiagnosticIds.MemberNotFound);
    }
    [Fact]
    public async Task Decimal_formatting_needs_its_own_exact_grant()
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        var manifest=JsonSerializer.Deserialize<LibraryBuildManifest>(await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),new JsonSerializerOptions{Converters={new JsonStringEnumConverter<LibraryIntrinsicCapability>()}})!;
        var artifact=Assert.Single(manifest.Artifacts);var grant=Assert.Single(artifact.Grants.Where(g=>g.Capability==LibraryIntrinsicCapability.InvariantDecimalFormatting));
        var bytes=AssemblyResolver.SerializeLibraryBuildManifest(manifest with{Artifacts=[artifact with{Grants=artifact.Grants.Remove(grant)}]});
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,bytes,Convert.ToHexStringLower(SHA256.HashData(bytes))).Value!;
        var input=scope.OpenInput(typeof(DecimalProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(DecimalProbe).GetMethod("Route")!.MetadataToken),implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.LibraryIntrinsicDenied);
    }
    [Fact]
    public async Task Detached_decimal_calls_reject_null_provider()
    {
        var program=(await Programs.Value)[2];var flow=program.Flow.Flow.Flow;
        flow=flow with{Methods=[..flow.Methods.Select(m=>m with{Blocks=[..m.Blocks.Select(b=>b with{Statements=[..b.Statements.Select(s=>s is IrStatement.Define d && d.Value.Expression is IrExpression.InvariantCulture ? d with{Value=d.Value with{Type=IrType.NullReference,Expression=new IrExpression.Null()}}:s)]})]})]};
        program=program with{Flow=program.Flow with{Flow=new(flow)}};
        Assert.Throws<ArgumentException>(()=>ManagedExceptionIrValidator.Validate(program));
    }

}
