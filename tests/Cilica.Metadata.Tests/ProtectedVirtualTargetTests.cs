using System;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ProtectedVirtualTargetTests
{
    [Theory]
    [InlineData(MethodAttributes.Assembly,false)] [InlineData(MethodAttributes.Public,false)]
    [InlineData(MethodAttributes.Private,false)] [InlineData(MethodAttributes.Family,true)]
    [InlineData(MethodAttributes.FamORAssem,false)] [InlineData(MethodAttributes.FamANDAssem,false)]
    public void Protected_slots_require_exact_access_and_keep_legacy_rejection(MethodAttributes access,bool success)
    {
        using var files=new ResolverFiles();using var resolver=AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library=resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b=new ResolutionAssemblyBuilder(new("ProtectedSlotFlags",new Version(1,0,0,0)));
        var obj=b.TypeReference("System","Object",b.Reference(library.Identity));
        var parent=b.Type("Test","Parent",baseType:obj,attributes:TypeAttributes.Public|TypeAttributes.Abstract);
        var declaration=b.Method("Read",[0x20,1,8,8],attributes:MethodAttributes.Family|MethodAttributes.Virtual|MethodAttributes.Abstract|MethodAttributes.NewSlot|MethodAttributes.HideBySig,implementation:MethodImplAttributes.IL);
        var child=b.Type("Test","Child",baseType:parent);
        var body=b.Method("Read",[0x20,1,8,8],attributes:access|MethodAttributes.Virtual|MethodAttributes.HideBySig|(access==MethodAttributes.Assembly?MethodAttributes.CheckAccessOnOverride:0),emit:il=>{il.LoadArgument(1);il.OpCode(ILOpCode.Ret);});
        var input=resolver.OpenInput(files.Write("input.dll",b.Build())).Value!;
        var receiver=resolver.ResolveType(input,child).Value!;var resolved=resolver.ResolveDeclaredMethod(input,declaration).Value!;
        Assert.False(resolver.ResolveClassVirtualTarget(input,declaration,receiver).Success);
        var budget=new MemberResolutionBudget();var result=resolver.ResolveClosedClassVirtualTarget(resolved,new(receiver,SignatureList<TypeSignature>.Empty),library,budget);
        Assert.Equal(success,result.Success);
        if(success){Assert.Equal(body,result.Value!.Implementation.Handle);Assert.Equal(declaration,result.Value.Slot.Handle);int used=65536-budget.Remaining;Assert.True(resolver.ResolveClosedClassVirtualTarget(resolved,result.Value.Receiver,library,new(used)).Success);Assert.False(resolver.ResolveClosedClassVirtualTarget(resolved,result.Value.Receiver,library,new(used-1)).Success);}
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Foreign_receivers_and_callers_do_not_gain_protected_access(bool receiver)
    {
        using var files=new ResolverFiles();
        var identity=new AssemblyIdentity("ProtectedVirtualLibrary",new Version(1,0,0,0));
        var target=new ResolutionAssemblyBuilder(identity);
        var obj=target.TypeReference("System","Object",target.Reference(new AssemblyIdentity(typeof(object).Assembly.GetName().Name!,typeof(object).Assembly.GetName().Version!,publicKeyToken:"7cec85d7bea7798e")));
        var parent=target.Type("Test","Parent",baseType:obj);
        var declaration=target.Method("Read",[0x20,1,8,8],attributes:MethodAttributes.Family|MethodAttributes.Virtual|MethodAttributes.NewSlot|MethodAttributes.HideBySig,emit:il=>{il.LoadArgument(1);il.OpCode(ILOpCode.Ret);});
        var targetPath=files.Write("ProtectedVirtualLibrary.dll",target.Build());
        using var resolver=AssemblyResolver.Create([Path.GetDirectoryName(targetPath)!,Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library=resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var caller=new ResolutionAssemblyBuilder(new("ProtectedVirtualForeign",new Version(1,0,0,0)));
        var owner=caller.TypeReference("Test","Parent",caller.Reference(identity));
        var method=caller.Member(owner,"Read",0x20,1,8,8);
        var child=caller.Type("Test","Child",baseType:owner);
        var entry=caller.Method("Run",[0,0,8],emit:il=>{il.OpCode(ILOpCode.Ldnull);il.LoadConstantI4(1);il.OpCode(ILOpCode.Callvirt);il.Token(method);il.OpCode(ILOpCode.Ret);});
        var image=resolver.OpenInput(files.Write("input.dll",caller.Build())).Value!;
        if(receiver){var source=resolver.OpenInput(targetPath).Value!;var resolved=resolver.ResolveDeclaredMethod(source,declaration).Value!;var result=resolver.ResolveClosedClassVirtualTarget(resolved,new(resolver.ResolveType(image,child).Value!,SignatureList<TypeSignature>.Empty),library);Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("slot assembly",StringComparison.Ordinal));}
        else {var result=ValueTranslator.TranslateIntegerObjects(resolver,image,entry,library);Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("accessible",StringComparison.Ordinal)||d.Message.Contains("access",StringComparison.Ordinal));}
    }
}
