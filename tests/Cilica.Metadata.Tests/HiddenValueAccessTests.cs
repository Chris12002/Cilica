using System;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class HiddenValueAccessTests
{
    [Theory]
    [InlineData("opaque",true)] [InlineData("box",false)] [InlineData("token",false)]
    [InlineData("local",false)] [InlineData("signature",false)] [InlineData("field",false)]
    [InlineData("private-field",false)] [InlineData("array",true)] [InlineData("array-explicit",false)]
    public void Foreign_generic_code_cannot_name_hidden_value_or_expose_members(string use,bool admitted)
    {
        var result=ForeignClosure(use);Assert.True(result.Success==admitted,string.Join("; ",result.Diagnostics));
        if(admitted)ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program!));
        else Assert.Contains(result.Diagnostics,d=>d.Message.Contains(use=="private-field"?"Instance field access requires":use=="array"?"Invalid reference IR":"lexical declaring scope",StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(false, "opaque")] [InlineData(true, "opaque")] [InlineData(false, "array")] [InlineData(true, "array")]
    public async Task Foreign_opaque_value_boxing_preserves_nominal_identity(bool optimized, string use)
    {
        var result=ForeignClosure(use);Assert.True(result.Success,string.Join("; ",result.Diagnostics));
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("ForeignValue",result.Program!,(int a,int b)=>unchecked(a+b+1))]);
    }
    private static ManagedExceptionTranslationResult ForeignClosure(string use)
    {
        using var files=new ResolverFiles();string runtime=Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        using var bootstrap=AssemblyResolver.Create([runtime]).Value!;
        var core=bootstrap.OpenInput(typeof(object).Assembly.Location).Value!.Identity;
        var homeIdentity=new AssemblyIdentity("HiddenValueOwner",new Version(1,0,0,0));
        var bridgeIdentity=new AssemblyIdentity("OpaqueValueBridge",new Version(1,0,0,0));
        var bridge=new ResolutionAssemblyBuilder(bridgeIdentity);
        var hidden=bridge.TypeReference("Test","Hidden",bridge.Reference(homeIdentity));
        byte hiddenCode=checked((byte)((MetadataTokens.GetRowNumber(hidden)<<2)|1));
        var variable=bridge.Specification(0x1e,0);
        var foreignField=bridge.Member(hidden,"Value",6,8);
        bridge.Type("Test","Bridge",baseType:bridge.TypeReference("System","Object",bridge.Reference(core)));
        byte[] parameter=use=="signature"?[0x11,hiddenCode]:[0x1e,0];
        bridge.Method("Box",[0x10,1,1,0x1c,..parameter],arity:1,
            locals:use=="local"?[7,1,0x11,hiddenCode]:null,emit:il=>{
                if(use=="token"){il.OpCode(ILOpCode.Ldtoken);il.Token(hidden);il.OpCode(ILOpCode.Pop);}
                if(use=="field"){il.LoadArgumentAddress(0);il.OpCode(ILOpCode.Ldfld);il.Token(foreignField);il.OpCode(ILOpCode.Pop);}
                if(use is "array" or "array-explicit"){il.LoadConstantI4(1);il.OpCode(ILOpCode.Newarr);il.Token(use=="array-explicit"?hidden:variable);il.OpCode(ILOpCode.Pop);}
                il.LoadArgument(0);il.OpCode(ILOpCode.Box);il.Token(use=="box"?hidden:variable);il.OpCode(ILOpCode.Ret);
            });
        files.Write("OpaqueValueBridge.dll",bridge.Build());
        var home=new ResolutionAssemblyBuilder(homeIdentity);
        var parent=home.TypeReference("System","ValueType",home.Reference(core));
        var type=home.Type("Test","Hidden",baseType:parent,attributes:TypeAttributes.NotPublic|TypeAttributes.Sealed|TypeAttributes.SequentialLayout);
        var field=home.FieldWithAttributes("Value",use=="private-field"?FieldAttributes.Private:FieldAttributes.Public,6,8);
        byte typeCode=checked((byte)(MetadataTokens.GetRowNumber(type)<<2));
        var helper=home.TypeReference("Test","Bridge",home.Reference(bridgeIdentity));
        var member=home.Member(helper,"Box",use=="signature"?new byte[]{0x10,1,1,0x1c,0x11,typeCode}:new byte[]{0x10,1,1,0x1c,0x1e,0});
        var call=home.Instantiate(member,0x0a,1,0x11,typeCode);
        home.Type("Test","Entry");
        var root=home.Method("Run",[0,2,8,8,8],locals:[7,2,0x11,typeCode,8],emit:il=>{
            il.LoadLocalAddress(0);il.OpCode(ILOpCode.Initobj);il.Token(type);
            il.LoadLocalAddress(0);il.LoadArgument(0);il.OpCode(ILOpCode.Stfld);il.Token(field);
            il.LoadLocal(0);il.Call(call);il.OpCode(ILOpCode.Dup);il.OpCode(ILOpCode.Isinst);il.Token(type);
            il.OpCode(ILOpCode.Ldnull);il.OpCode(ILOpCode.Cgt_un);il.StoreLocal(1);
            il.OpCode(ILOpCode.Unbox_any);il.Token(type);il.StoreLocal(0);
            il.LoadLocalAddress(0);il.OpCode(ILOpCode.Ldfld);il.Token(field);il.LoadArgument(1);il.OpCode(ILOpCode.Add);
            il.LoadLocal(1);il.OpCode(ILOpCode.Add);il.OpCode(ILOpCode.Ret);
        });
        var path=files.Write("HiddenValueOwner.dll",home.Build());
        using var resolver=AssemblyResolver.Create([files.DirectoryPath("refs"),runtime]).Value!;
        var library=resolver.OpenInput(typeof(object).Assembly.Location).Value!;var input=resolver.OpenInput(path).Value!;
        return ValueTranslator.TranslateIntegerObjects(resolver,input,root,library);
    }
}
