from pathlib import Path
r=Path('/Users/christian/.codex/worktrees/alchemy-replay-baseline/Cilica')
def edit(p,a,b):
 p=r/p;s=p.read_text();assert s.count(a)==1,(p,a,s.count(a));p.write_text(s.replace(a,b))
(r/'docs/decisions/compatibility-018-character-delegate-parameters.md').write_text('''# Character delegate parameters\n\n2026-10-08. Bounded owned follow-up to batch 015.\n\n## Decision\n\nAdmit exact Char parameters in the integer-object CIL profile. Char uses the\nexisting Int32 carrier; ordinary target argument loads/stores and address loads\nretain existing UInt16 normalization. Authenticate Char target parameter identity\nbefore lowering erases primitive distinctions. Char/UInt16/Int32 substitution must\nfail even though the carrier is shared. Boolean result handling is unchanged.\n\nThis general parameter support also composes with existing Action, Predicate,\nComparison and multiple-parameter declarations. It does not admit other narrow\nparameters, Char results, byref delegate signatures, variance conversions or\nmulticast. The standalone delegate profile retains its prior bound.\n\nInteger CIL advances from v102 to v103. Delegate/object/scalar IR, native runtime,\nfunction pointer signatures, GC tracing, root ownership, quotas and grants do not\nchange. There is no boxing fallback or upstream source execution.\n\n## Validation plan\n\nCompare owned static/captured predicates, all UTF-16 values, two arguments, live\nLINQ mutation, null/fault ordering, allocation and receiver cycles with .NET at\nO0/O2, forced GC, ASan/UBSan, strict frames and exact root/borrow cleanup. Reject\nforged Char scalar aliases before lowering, unsupported adjacent signatures and\nlegacy profile use. Retain initial failures and native source/binary hashes.\n''')
edit('src/Cilica.Compiler/ValueDelegateDiscovery.cs','Code: PrimitiveTypeCode.Int64 or PrimitiveTypeCode.String or PrimitiveTypeCode.Object }\n                        or TypeSignature.Named { Kind: SignatureTypeKind.Class or SignatureTypeKind.ValueType }\n                        or TypeSignature.Generic { Definition.Kind: SignatureTypeKind.Class or SignatureTypeKind.ValueType } or TypeSignature.Vector)))\n                ||','Code: PrimitiveTypeCode.Int64 or PrimitiveTypeCode.Char or PrimitiveTypeCode.String or PrimitiveTypeCode.Object }\n                        or TypeSignature.Named { Kind: SignatureTypeKind.Class or SignatureTypeKind.ValueType }\n                        or TypeSignature.Generic { Definition.Kind: SignatureTypeKind.Class or SignatureTypeKind.ValueType } or TypeSignature.Vector)))\n                ||')
edit('src/Cilica.Compiler/ValueDelegateDiscovery.cs','exact Int32/Int64/reference/inline-value parameters','exact Int32/Int64/Char/reference/inline-value parameters')
edit('src/Cilica.Compiler/ValueDelegateDiscovery.cs','unsigned/narrow parameters/results and byref signatures','other unsigned/narrow parameters, narrow non-Boolean results and byref signatures')
edit('src/Cilica.Compiler/ValueProgramDiscovery.cs','Code: PrimitiveTypeCode.Int32 or PrimitiveTypeCode.Int64 } && pair.First != pair.Second','Code: PrimitiveTypeCode.Int32 or PrimitiveTypeCode.Int64 or PrimitiveTypeCode.Char } && pair.First != pair.Second')
edit('src/Cilica.Compiler/ValueTranslator.cs','integer-managed-objects-cil-v102','integer-managed-objects-cil-v103')
edit('tests/Cilica.Metadata.Tests/CandidateCharacterArrayTests.cs','[InlineData("ReadOnlyReference",false)] [InlineData("ReadOnlyReference",true)]','[InlineData("ReadOnlyReference",false)] [InlineData("ReadOnlyReference",true)]\n    [InlineData("CharacterPredicate",false)] [InlineData("CharacterPredicate",true)]')
edit('tests/Cilica.Metadata.Tests/CandidateCharacterArrayTests.cs','    [InlineData("CharacterPredicate", "CIL2001", "unsigned/narrow parameters/results")]\n','')
edit('tests/Cilica.Metadata.Tests/FrameworkDelegateTests.cs','    [InlineData(2, 8, 8, 8)]','    [InlineData(2, 2, 3, 8)] // Char delegate / Int32 target parameter\n    [InlineData(2, 2, 3, 7)] // Char delegate / UInt16 target parameter\n    [InlineData(2, 2, 3, 5)] // Char delegate / Byte target parameter\n    [InlineData(2, 2, 3, 2)] // Char delegate / Boolean target parameter\n    [InlineData(2, 2, 8, 3)] // Int32 delegate / Char target parameter\n    [InlineData(2, 8, 8, 8)]')
(r/'tests/Fixtures/Cilica.Fixtures.Execution/CharacterDelegateProbe.cs').write_text('''using System;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public sealed class CharacterDelegateReceiver
    {
        public int Seen;
        public char Expected;
        public char[] Values;
        public Func<char,bool> Back;
        public bool Check(char value)
        {
            var pressure = new CharacterDelegateReceiver { Expected = value };
            Seen = unchecked(Seen * 31 + pressure.Expected);
            return pressure.Expected == Expected;
        }
        public void Set(char value) { Expected = new CharacterDelegateReceiver { Expected = value }.Expected; }
        public bool Mutate(char value)
        {
            Seen++;
            Values[1] = Expected;
            var pressure = new CharacterDelegateReceiver { Expected = value };
            if (value == '\\uffff') throw new InvalidOperationException("unit");
            return pressure.Expected == Expected;
        }
    }
    public static class CharacterDelegateProbe
    {
        public static bool Test(char value) => value >= '\\u8000' || value == 'A';
        public static int Read(char value) => value;
        public static bool Same(char left, char right) => left == right;
        public static int AllUnits(int first, int unused)
        {
            var test = new Func<char,bool>(Test);
            var read = new Func<char,int>(Read);
            for (int i=0;i<32;i++)
            {
                char value=unchecked((char)(first+i));
                if (test(value) != (value >= '\\u8000' || value == 'A') || read(value) != (first+i)) return i+1;
            }
            return 0;
        }
        public static int Capture(int a,int b)
        {
            var receiver=new CharacterDelegateReceiver { Expected=unchecked((char)a), Seen=b };
            receiver.Back=new Func<char,bool>(receiver.Check);
            var callback=receiver.Back;
            bool first=callback(unchecked((char)a));
            var setter=new Action<char>(receiver.Set); setter(unchecked((char)b));
            bool second=callback(unchecked((char)a));
            return unchecked(receiver.Seen * 7 + (first?1:0) + (second?2:0) + receiver.Expected);
        }
        public static int Two(int a,int b)
        {
            var test=new Func<char,char,bool>(Same);
            var predicate=new Predicate<char>(Test);
            return (test(unchecked((char)a),unchecked((char)b))?1:0) + (predicate(unchecked((char)a))?2:0);
        }
        public static int LiveAny(int a,int b)
        {
            var receiver=new CharacterDelegateReceiver { Expected=unchecked((char)b), Values=new char[]{unchecked((char)a),'x','y'} };
            receiver.Back=new Func<char,bool>(receiver.Mutate);
            try { return receiver.Values.Any(receiver.Back)?receiver.Seen*10+1:receiver.Seen*10; }
            catch(InvalidOperationException) { return receiver.Seen*10+2; }
        }
        public static char Argument(ref int trace,int value)
        {
            trace=unchecked(trace*10+1);
            if(value==1) throw new InvalidOperationException("argument");
            return unchecked((char)value);
        }
        public static int Faults(int a,int b)
        {
            var receiver=new CharacterDelegateReceiver { Expected=unchecked((char)a) };
            Func<char,bool> callback=a==0?null:new Func<char,bool>(receiver.Check);
            int trace=b;
            try { bool answer=callback(Argument(ref trace,b));return unchecked(trace*10+(answer?1:0)); }
            catch(NullReferenceException) { return unchecked(trace*10+2); }
            catch(InvalidOperationException) { return unchecked(trace*10+3); }
        }
        public static int NullTarget(int a,int b)
        {
            CharacterDelegateReceiver receiver=null;
            try { var callback=new Func<char,bool>(receiver.Check);return callback(unchecked((char)a))?1:0; }
            catch(ArgumentException) { return unchecked(a+b); }
        }
        public static byte ByteIdentity(byte value)=>value;
        public static ushort UShortIdentity(ushort value)=>value;
        public static char CharIdentity(char value)=>value;
        public static int UnsupportedByte(int a,int b)=>new Func<byte,byte>(ByteIdentity)(unchecked((byte)a));
        public static int UnsupportedUShort(int a,int b)=>new Func<ushort,ushort>(UShortIdentity)(unchecked((ushort)a));
        public static int UnsupportedResult(int a,int b)=>new Func<char,char>(CharIdentity)(unchecked((char)a));
    }
}
''')
(r/'tests/Cilica.Metadata.Tests/CharacterDelegateTests.cs').write_text('''using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class CharacterDelegateTests
{
    private static readonly string[] Names=["Capture","Two","LiveAny","Faults","NullTarget","AllUnits"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(CharacterDelegateProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)]
    public async Task Character_callbacks_preserve_roots_mutations_and_faults(bool optimized,int group)
    {
        var programs=await Programs.Value;
        int[] units=[0,1,-1,65,255,256,32767,32768,0xd800,0xdfff,0xffff,0x10000,int.MinValue,int.MaxValue];
        var pairs=(from a in units from b in new[]{0,1,-1,65,0xffff} select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,Names.Take(5).Select((name,i)=>
            (name,programs[i],typeof(CharacterDelegateProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(2).ToArray(),pairs);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task All_UTF16_units_survive_delegate_arguments(bool optimized)
    {
        var p=await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("CharUnits",p[5],CharacterDelegateProbe.AllUnits)],Enumerable.Range(0,2048).Select(i=>(i*32,0)).ToArray());
    }
    [Theory] [InlineData("UnsupportedByte")] [InlineData("UnsupportedUShort")] [InlineData("UnsupportedResult")]
    public async Task Other_narrow_signatures_remain_diagnostic(string name)
    {
        using var files=new ResolverFiles();
        var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(CharacterDelegateProbe).Assembly.Location).Value!;
        var method=(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CharacterDelegateProbe).GetMethod(name)!.MetadataToken);
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,method,implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id=="CIL2001" && d.Message.Contains("Delegate requires",StringComparison.Ordinal));
    }
    [Fact]
    public void Standalone_delegate_profile_still_rejects_character_parameters()
    {
        using var files=new ResolverFiles();using var resolver=new Cilica.Metadata.Resolution.AssemblyResolver();
        var library=resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var input=resolver.OpenInput(typeof(CharacterDelegateProbe).Assembly.Location).Value!;
        var method=(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CharacterDelegateProbe).GetMethod("Two")!.MetadataToken);
        var result=ValueTranslator.TranslateDelegates(resolver,input,method,library);
        Assert.False(result.Success);Assert.NotEmpty(result.Diagnostics);
    }
}
''')
