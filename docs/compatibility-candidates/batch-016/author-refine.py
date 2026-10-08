from pathlib import Path
import json,hashlib
r=Path('/Users/christian/.codex/worktrees/alchemy-replay-baseline/Cilica');d=r/'artifacts/compatibility-batch-016'
paths=['src/Cilica.Compiler/ValueDelegateDiscovery.cs','src/Cilica.Compiler/ValueProgramDiscovery.cs','src/Cilica.Compiler/ValueTranslator.cs','tests/Cilica.Metadata.Tests/CharacterDelegateTests.cs','tests/Cilica.Metadata.Tests/CandidateCharacterArrayTests.cs','tests/Cilica.Metadata.Tests/FrameworkDelegateTests.cs','tests/Fixtures/Cilica.Fixtures.Execution/CharacterDelegateProbe.cs']
(d/'initial-source-hashes.json').write_text(json.dumps({p:hashlib.sha256((r/p).read_bytes()).hexdigest() for p in paths},indent=2)+'\n')
p=r/'tests/Fixtures/Cilica.Fixtures.Execution/CharacterDelegateProbe.cs';s=p.read_text();pos=s.index('        public static char Argument');s=s[:pos]+'''        // Independently test live callback reads while retaining LiveAny as a negative.
        public static int LiveLoop(int a,int b)
        {
            var receiver=new CharacterDelegateReceiver { Expected=unchecked((char)b), Values=new char[]{unchecked((char)a),'x','y'} };
            receiver.Back=new Func<char,bool>(receiver.Mutate);
            try
            {
                for(int i=0;i<receiver.Values.Length;i++)
                    if(receiver.Back(receiver.Values[i])) return receiver.Seen*10+1;
                return receiver.Seen*10;
            }
            catch(InvalidOperationException) { return receiver.Seen*10+2; }
        }
        public static Func<char,bool> Make(char expected)
        {
            var receiver=new CharacterDelegateReceiver { Expected=expected };
            receiver.Back=new Func<char,bool>(receiver.Check);
            return receiver.Back;
        }
        public static int SoleReceiver(int a,int b)
        {
            var callback=Make(unchecked((char)a));
            var pressure=Make(unchecked((char)b));
            bool first=callback(unchecked((char)a));
            bool second=callback(unchecked((char)b));
            return (first?1:0)+(second?2:0)+(pressure(unchecked((char)b))?4:0);
        }
'''+s[pos:];p.write_text(s)
p=r/'tests/Cilica.Metadata.Tests/CharacterDelegateTests.cs';s=p.read_text().replace('"Capture","Two","LiveAny","Faults","NullTarget","AllUnits"','"Capture","Two","LiveLoop","Faults","NullTarget","SoleReceiver","AllUnits"').replace('Names.Take(5)','Names.Take(6)').replace('p[5]','p[6]');pos=s.index('    [Fact]\n');s=s[:pos]+'''    [Fact]
    public async Task Character_LINQ_composition_retains_the_narrow_vector_interface_diagnostic()
    {
        using var files=new ResolverFiles();
        var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(CharacterDelegateProbe).Assembly.Location).Value!;
        var method=(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CharacterDelegateProbe).GetMethod("LiveAny")!.MetadataToken);
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,method,implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id=="CIL2001" && d.Message.Contains("Narrow vector interfaces admit",StringComparison.Ordinal));
    }
'''+s[pos:];p.write_text(s)
p=r/'tests/Cilica.Metadata.Tests/CandidateCharacterArrayTests.cs';s=p.read_text().replace('    [InlineData("CharacterPredicate",false)] [InlineData("CharacterPredicate",true)]\n','').replace('    [InlineData("MissingSection", "CIL3010", "ToCharArray")]','    [InlineData("MissingSection", "CIL3010", "ToCharArray")]\n    [InlineData("CharacterPredicate", "CIL2001", "Narrow vector interfaces admit")]');p.write_text(s)
p=d/'run-refined.py';p.write_text((d/'run.py').read_text().replace('initial','refined'))
