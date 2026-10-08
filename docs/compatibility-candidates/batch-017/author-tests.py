from pathlib import Path
r=Path('/Users/christian/.codex/worktrees/alchemy-replay-baseline/Cilica')
p=r/'src/Cilica.Compiler/ValueTranslator.cs';s=p.read_text();assert s.count('integer-managed-objects-cil-v104')==1;p.write_text(s.replace('integer-managed-objects-cil-v104','integer-managed-objects-cil-v105'))
p=r/'docs/decisions/compatibility-019-character-array-enumeration.md';s=p.read_text().replace('Reserve a distinct CIL capability version\nafter the concurrent provider-parse lane; coordinate before modifying its file.','CIL advances from v104 to v105 after the committed provider-parse slice\n30cbcf8; object IR63/native79 are unchanged.');p.write_text(s)
original=(r/'tests/Cilica.Metadata.Tests/ArrayEnumerationTests.cs').read_text()
header=original[:original.index('public sealed class')]
grant=original[original.index('    [Fact]\n    public async Task Exact_grant'):original.index('    [Fact]\n    public async Task Detached_enumeration')].replace('ArrayEnumerationProbe','CharArrayEnumerationProbe').replace('Method("Integers")','Method("States")')
gc=original[original.index('    [Theory]\n    [InlineData(false)] [InlineData(true)]\n    public async Task Enumerators_retain'):].replace('ArrayEnumerationProbe','CharArrayEnumerationProbe').replace('heap.count == 4','heap.count == 3')
gc=gc.replace('        var build = await NativeProcess.Run','        NativeEvidence.Record(cpp, optimized);\n        var build = await NativeProcess.Run').replace('        var run = await NativeProcess.Run(executable','        NativeEvidence.Record(cpp, optimized, executable);\n        var run = await NativeProcess.Run(executable')
body='''public sealed class CharArrayEnumerationTests
{
    private static readonly string[] Names=["States","FreshAndNull","Queries","NullAndFaults","DecoderShape","Empty","AllUnits"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(CharArrayEnumerationProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)]
    public async Task Cursor_and_query_semantics_match_dotnet(bool optimized,int group)
    {
        var p=await Programs.Value;
        int[] inputs=[0,1,2,3,4,-1,65,255,256,32768,0xd800,0xdfff,0xffff,0x10000,int.MinValue,int.MaxValue];
        var pairs=(from a in inputs from b in new[]{0,1,65,-1,0xffff} select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,Names.Take(5).Select((name,i)=>
            (name,p[i],typeof(CharArrayEnumerationProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(2).ToArray(),pairs);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Mutable_empty_cache_has_exact_closed_identity(bool optimized)
    {
        var p=await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("CharEmpty",p[5],CharArrayEnumerationProbe.Empty)],expectedStaticRoots:2);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Every_UTF16_unit_is_read_through_exact_generic_Current(bool optimized)
    {
        var p=await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("CharUnits",p[6],CharArrayEnumerationProbe.AllUnits)],Enumerable.Range(0,1024).Select(i=>(i*64,0)).ToArray());
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Former_generic_enumeration_blocker_executes(bool optimized)
    {
        var p=await CoreLibraryBuildTests.TranslateFixtures(typeof(NarrowArrayMetadataProbe),"UnsupportedEnumeration");
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("FormerCharEnumeration",p[0],NarrowArrayMetadataProbe.UnsupportedEnumeration)]);
    }
    [Theory]
    [InlineData("UnsupportedByte")] [InlineData("UnsupportedBool")] [InlineData("UnsupportedSet")] [InlineData("UnsupportedCharCopy")]
    public async Task Other_narrow_operations_remain_rejected(string name)
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(CharArrayEnumerationProbe).Assembly.Location).Value!;
        var method=(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CharArrayEnumerationProbe).GetMethod(name)!.MetadataToken);
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,method,implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("Narrow vector interfaces",StringComparison.Ordinal));
    }
    [Fact]
    public async Task Detached_Char_enumeration_packets_require_exact_receivers_and_results()
    {
        var program=(await Programs.Value)[5];
        foreach(var plan in new[]{program.Flow.InterfaceDispatch!,ValueScalarLowering.LowerExceptions(program).Flow.Flow.Flow.Flow.InterfaceDispatch!})
        {
            var helper=plan.Methods.Single(m=>m.ArrayHelper && m.Name=="Enumerate" && m.Parameters[0].Class!.ArrayElement!.Kind==ObjectArrayElementKind.Char);
            var integer=plan.Methods.Single(m=>m.ArrayHelper && m.Name=="Enumerate" && m.Parameters[0].Class!.ArrayElement!.Kind==ObjectArrayElementKind.Int32);
            foreach(var changed in new[]{helper with {ReturnType=integer.ReturnType},helper with {Parameters=integer.Parameters},
                helper with {Name="Get",Parameters=helper.Parameters.Add(IrType.Int32),ReturnType=IrType.Int32},
                helper with {ArrayHelper=false},helper with {ReturnType=IrType.Reference(plan.Types.ObjectBase)}})
                Assert.Throws<ArgumentException>(()=>ObjectInterfacePlanValidator.Validate(plan with {Methods=plan.Methods.Replace(helper,changed)}));
        }
    }
'''
(r/'tests/Cilica.Metadata.Tests/CharArrayEnumerationTests.cs').write_text(header+body+grant+gc)
d=r/'artifacts/compatibility-batch-017';d.mkdir(exist_ok=True)
s=(r/'docs/compatibility-candidates/batch-016/run-refined.py').read_text().replace('compatibility-batch-016','compatibility-batch-017').replace('refined','initial')
s=s.replace("'Cilica.Metadata.Tests.FrameworkDelegateTests'","'Cilica.Metadata.Tests.CharArrayEnumerationTests'").replace("'Cilica.Metadata.Tests.WideDelegateTests'","'Cilica.Metadata.Tests.NarrowArrayMetadataTests'")
(d/'run.py').write_text(s)
