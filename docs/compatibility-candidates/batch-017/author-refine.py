from pathlib import Path
import hashlib,json
r=Path('/Users/christian/.codex/worktrees/alchemy-replay-baseline/Cilica');d=r/'artifacts/compatibility-batch-017'
h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
files=['tests/Fixtures/Cilica.Fixtures.Execution/CharArrayEnumerationProbe.cs','tests/Cilica.Metadata.Tests/CharArrayEnumerationTests.cs']
(d/'initial-probe-hashes.json').write_text(json.dumps({p:h(r/p) for p in files},indent=2)+'\n')
for name in files:(d/('initial-'+Path(name).name+'.txt')).write_bytes((r/name).read_bytes())
build=d/'build/bin/_Users_christian_.codex_worktrees_alchemy-replay-baseline_Cilica_tests_Cilica.Metadata.Tests/debug'
(d/'initial-assembly-hashes.json').write_text(json.dumps({p.name:h(p) for p in build.glob('Cilica*.dll')},indent=2)+'\n')
p=r/files[0];s=p.read_text();marker='        public static bool Invalid(char value)';at=s.index(marker);s=s[:at]+'''        public static int BoxedCurrent(int a,int b)
        {
            var values=new char[]{unchecked((char)a)};
            var e=((IEnumerable<char>)values).GetEnumerator();e.MoveNext();
            IEnumerator legacy=e;object first=legacy.Current;object second=legacy.Current;
            values[0]=unchecked((char)b);object later=legacy.Current;
            if(ReferenceEquals(first,second)||!(first is char)||first is int)return -100;
            return unchecked((char)first*31+(char)second*17+(char)later+e.Current);
        }
'''+s[at:];p.write_text(s)
p=r/files[1];s=p.read_text().replace('"NullAndFaults","DecoderShape","Empty"','"NullAndFaults","BoxedCurrent","Empty"');at=s.index('    [Fact]\n    public async Task Detached_');s=s[:at]+'''    [Fact]
    public async Task Full_owned_decoder_shape_records_the_next_missing_API()
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(CharArrayEnumerationProbe).Assembly.Location).Value!;
        var method=(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CharArrayEnumerationProbe).GetMethod("DecoderShape")!.MetadataToken);
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,method,implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id=="CIL3010" && d.Message.Contains("ToUpperInvariant",StringComparison.Ordinal));
    }
'''+s[at:];p.write_text(s)
(d/'run-refined.py').write_text((d/'run.py').read_text().replace('initial','refined'))
