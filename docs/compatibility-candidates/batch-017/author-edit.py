from pathlib import Path
r=Path('/Users/christian/.codex/worktrees/alchemy-replay-baseline/Cilica')
def edit(name,a,b):
 p=r/name;s=p.read_text();assert s.count(a)==1,(name,a,s.count(a));p.write_text(s.replace(a,b))
(r/'docs/decisions/compatibility-019-character-array-enumeration.md').write_text('''# Exact generic Char-array enumeration\n\n2026-10-08. Owned follow-up to batch 016.\n\n## Decision\n\nAdmit only the existing generic Enumerate helper for exact Char arrays. Any's\nmanaged array loop already works; discovery also includes the fallback path,\nwhich requires an IEnumerable<Char> target. Reuse ArrayInterface<Char>.Enumerate\nand SZGenericArrayEnumerator<Char>, with live Current, reset/disposal, faults,\nper-closed-type mutable empty cache and ordinary managed ownership.\n\nMake the narrow recipe predicate depend on the actual vector identity. Shared\ndiscovery and detached validators must admit Char Enumerate while preserving\nall other existing gates. Match IEnumerator<Char> by exact closed primitive\nidentity and require the helper to retain the exact Char receiver. No new\nmanaged library body, intrinsic grant, native operation, quota or GC path.\n\nChar indexed operations/copy and Byte/Boolean generic enumeration remain outside\nthis slice. Signed aliases and forged receiver/result packets remain rejected.\nInterface plan advances from v19 to v20. Reserve a distinct CIL capability version\nafter the concurrent provider-parse lane; coordinate before modifying its file.\n\n## Validation plan\n\nOwned differential tests cover all UTF-16 units, cursor states and live mutation,\nempty cache identity, faults, Any/All/Count, the reduced decoder composition, and\nsole-array retention/release across GC. Test denied exact grant and malformed\nlogical/scalar plans. Preserve prior narrow negative controls and raw failures.\nNo upstream source execution or full candidate compatibility claim.\n''')
edit('src/Cilica.Compiler/ObjectArrayInterfaceRecipes.cs','internal static bool NarrowRecipe(string? name) => ElementIndependent(name) || name == "EnumerateObjects";','internal static bool NarrowRecipe(string? name, ObjectTypeId vector) => ElementIndependent(name) || name == "EnumerateObjects"\n        || name == "Enumerate" && vector.ArrayElement?.Kind == ObjectArrayElementKind.Char;')
edit('src/Cilica.Compiler/ObjectArrayInterfaceRecipes.cs','!NarrowRecipe(method.Name)','!NarrowRecipe(method.Name, vector)')
edit('src/Cilica.Compiler/ObjectArrayInterfaceRecipes.cs','        string wide = ClosedGenericKey.Create','        string character = ClosedGenericKey.Create(new([new TypeSignature.Primitive(PrimitiveTypeCode.Char)]), SignatureList<TypeSignature>.Empty);\n        string wide = ClosedGenericKey.Create')
edit('src/Cilica.Compiler/ObjectArrayInterfaceRecipes.cs',': vector.ArrayElement.Kind == ObjectArrayElementKind.Int64 ? i.Arguments[0].Identity == wide',': vector.ArrayElement.Kind == ObjectArrayElementKind.Char ? i.Arguments[0].Identity == character\n                : vector.ArrayElement.Kind == ObjectArrayElementKind.Int64 ? i.Arguments[0].Identity == wide')
edit('src/Cilica.Compiler/ObjectInterfacePlanBuilder.cs','!ObjectArrayInterfaceRecipes.NarrowRecipe(recipe)','!ObjectArrayInterfaceRecipes.NarrowRecipe(recipe, receiver)')
edit('src/Cilica.Compiler/ObjectInterfacePlanBuilder.cs','Count, collection flags, SyncRoot and nongeneric enumeration; other element operations','Count, collection flags, SyncRoot, nongeneric enumeration and exact Char generic enumeration; other element operations')
edit('src/Cilica.Compiler/ObjectInterfacePlanBuilder.cs','&& recipe is "Enumerate" or "CopyTo")','&& recipe is "Enumerate" or "CopyTo"\n                    && !(recipe == "Enumerate" && primitive.Code == PrimitiveTypeCode.Char && receiver.ArrayElement.Kind == ObjectArrayElementKind.Char))')
edit('src/Cilica.Compiler/ObjectInterfacePlanValidator.cs','ObjectArrayInterfaceRecipes.NarrowRecipe(mapping.Name)','ObjectArrayInterfaceRecipes.NarrowRecipe(mapping.Name, target.Receiver)')
edit('src/Cilica.Compiler/ObjectInterfacePlanValidator.cs','"Narrow array targets require a metadata or nongeneric enumeration helper and an exact Boolean/Byte/Char contract."','"Narrow array targets require a metadata, nongeneric enumeration or exact Char enumeration helper and an exact Boolean/Byte/Char contract."')
edit('src/Cilica.Compiler/ObjectInterfacePlanValidator.cs','                    var requestedArgument = contracts[declaration.Owner].Arguments.FirstOrDefault();','                    Require(mapping.Name != "Enumerate" || target.Receiver.ArrayElement.Kind != ObjectArrayElementKind.Char\n                        || mapping.Parameters[0].Class == target.Receiver,\n                        "Generic Char enumeration must retain the exact Char array receiver.");\n                    var requestedArgument = contracts[declaration.Owner].Arguments.FirstOrDefault();')
edit('src/Cilica.Compiler/ObjectInterfaceDispatchPlan.cs','interface-dispatch-plan-v19','interface-dispatch-plan-v20')
edit('tests/Cilica.Metadata.Tests/NarrowArrayMetadataTests.cs','"UnsupportedGet", "UnsupportedCopy", "UnsupportedEnumeration", "UnsupportedClear"','"UnsupportedGet", "UnsupportedCopy", "UnsupportedClear"')
edit('tests/Cilica.Metadata.Tests/CandidateCharacterArrayTests.cs','    [InlineData("CharacterPredicate", "CIL2001", "Narrow vector interfaces admit")]\n','')
edit('tests/Cilica.Metadata.Tests/CandidateCharacterArrayTests.cs','    [InlineData("ReadOnlyReference",false)] [InlineData("ReadOnlyReference",true)]','    [InlineData("ReadOnlyReference",false)] [InlineData("ReadOnlyReference",true)]\n    [InlineData("CharacterPredicate",false)] [InlineData("CharacterPredicate",true)]')
p=r/'tests/Cilica.Metadata.Tests/CharacterDelegateTests.cs';s=p.read_text();start=s.index('    [Fact]\n    public async Task Character_LINQ_composition');end=s.index('    [Fact]\n',start+12);s=s[:start]+'''    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Character_LINQ_live_mutation_matches_dotnet(bool optimized)
    {
        var p=await CoreLibraryBuildTests.TranslateFixtures(typeof(CharacterDelegateProbe),"LiveAny");
        int[] units=[0,1,-1,65,255,256,32767,32768,0xd800,0xdfff,0xffff,0x10000,int.MinValue,int.MaxValue];
        var pairs=(from a in units from b in new[]{0,1,-1,65,0xffff} select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("LiveAny",p[0],CharacterDelegateProbe.LiveAny)],pairs);
    }
'''+s[end:];p.write_text(s)
(r/'tests/Fixtures/Cilica.Fixtures.Execution/CharArrayEnumerationProbe.cs').write_text('''using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public static class CharArrayEnumerationProbe
    {
        public static int States(int a,int b)
        {
            var values=new char[]{unchecked((char)a),unchecked((char)b)};
            var e=((IEnumerable<char>)values).GetEnumerator();int result=0;
            try { return e.Current; } catch(InvalidOperationException ex) { result+=ex.Message=="Enumeration has not started. Call MoveNext."?1:-100; }
            e.MoveNext();values[0]=unchecked((char)b);result=unchecked(result+e.Current);
            e.Dispose();if(!e.MoveNext())return -101;result=unchecked(result+e.Current);
            if(e.MoveNext()||e.MoveNext())return -102;
            try{return e.Current;}catch(InvalidOperationException ex){result+=ex.Message=="Enumeration already finished."?3:-100;}
            ((IEnumerator)e).Reset();if(!e.MoveNext())return -103;
            return unchecked(result+e.Current);
        }
        public static int Empty(int a,int b)
        {
            var first=((IEnumerable<char>)new char[0]).GetEnumerator();((IEnumerator)first).Reset();
            var second=((IEnumerable<char>)new char[0]).GetEnumerator();
            var other=((IEnumerable<int>)new int[0]).GetEnumerator();
            if(first!=second||(object)first==other)return -100;
            int result=0;
            try{return first.Current;}catch(InvalidOperationException ex){result+=ex.Message=="Enumeration has not started. Call MoveNext."?1:-100;}
            if(second.MoveNext())return -101;
            var third=((IEnumerable<char>)new char[0]).GetEnumerator();
            try{return third.Current;}catch(InvalidOperationException ex){result+=ex.Message=="Enumeration already finished."?3:-100;}
            first.Dispose();((IEnumerator)second).Reset();
            try{return third.Current;}catch(InvalidOperationException ex){result+=ex.Message=="Enumeration has not started. Call MoveNext."?7:-100;}
            return result;
        }
        public static int FreshAndNull(int a,int b)
        {
            var values=new char[]{unchecked((char)a)};
            var first=((IEnumerable<char>)values).GetEnumerator();var second=((IEnumerable<char>)values).GetEnumerator();
            if(first==second)return -100;first.MoveNext();int result=first.Current;
            try{return second.Current;}catch(InvalidOperationException){result=unchecked(result+b);}
            IEnumerable<char> missing=null;
            try{missing.GetEnumerator();return -101;}catch(NullReferenceException){return result;}
        }
        public static int AllUnits(int first,int unused)
        {
            var values=new char[64];for(int i=0;i<64;i++)values[i]=unchecked((char)(first+i));
            var e=((IEnumerable<char>)values).GetEnumerator();int index=0;
            while(e.MoveNext()){if(e.Current!=first+index)return index+1;index++;}
            return index==64?0:-1;
        }
        public static bool Upper(char value)=>value>='A'&&value<='Z';
        public static bool Fault(char value){if(value=='\\uffff')throw new InvalidOperationException("unit");return value=='A';}
        public static int Queries(int a,int b)
        {
            char[] values=a==0?new char[0]:new char[]{unchecked((char)a),unchecked((char)b),'A'};
            var callback=new Func<char,bool>(Upper);
            return (values.Any(callback)?1:0)+(values.All(callback)?2:0)+values.Count(callback)*4;
        }
        public static int NullAndFaults(int a,int b)
        {
            char[] values=a==0?null:a==1?new char[0]:new char[]{unchecked((char)b),'A'};
            Func<char,bool> callback=b==0?null:new Func<char,bool>(Fault);
            try{return values.Any(callback)?1:0;}
            catch(ArgumentNullException ex){return ex.ParamName=="source"?11:ex.ParamName=="predicate"?13:-100;}
            catch(InvalidOperationException ex){return ex.Message=="unit"?17:-101;}
        }
        public static bool Invalid(char value)=>"ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".IndexOf(value)<0;
        public static int DecoderShape(int a,int b)
        {
            string source=a==0?"":a==1?"abc===":a==2?"2z7":a==3?"a0":a==4?"\\ud800=":"a\\0=";
            return Check(in source);
        }
        public static int Check(in string source)
        {
            char[] units=source.TrimEnd(new char[]{'='}).ToUpperInvariant().ToCharArray();
            return units.Any(new Func<char,bool>(Invalid))?-1:units.Length;
        }
        public static IEnumerator<char> CreateTracked()=>((IEnumerable<char>)new char[]{'A'}).GetEnumerator();
        public static void EndTracked(IEnumerator<char> e){e.MoveNext();e.MoveNext();e.Dispose();}
        public static int Tracked(int a,int b){var e=CreateTracked();EndTracked(e);return a;}
        public static int Cache(int a,int b)
        {
            var first=((IEnumerable<char>)new char[0]).GetEnumerator();var second=((IEnumerable<char>)new char[0]).GetEnumerator();
            var other=((IEnumerable<int>)new int[0]).GetEnumerator();return first==second&&(object)first!=other?11:-100;
        }
        public static int UnsupportedByte(int a,int b)=>((IEnumerable<byte>)new byte[1]).GetEnumerator().MoveNext()?a:b;
        public static int UnsupportedBool(int a,int b)=>((IEnumerable<bool>)new bool[1]).GetEnumerator().MoveNext()?a:b;
        public static int UnsupportedSet(int a,int b){IList<char> values=new char[1];values[0]=unchecked((char)a);return b;}
        public static int UnsupportedCharCopy(int a,int b){ICollection<char> values=new char[1];values.CopyTo(new char[1],0);return a;}
    }
}
''')
