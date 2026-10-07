using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;
public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private ValueIntrinsicCall? ObjectCloneCall(Work work,MethodInstruction instruction,ResolvedMethod target)
        {
            if(!integers64 || target.DeclaringType.Definition.Assembly!=library
                || target.DeclaringType.Definition!=resolver.ResolveRuntimeType(library,PrimitiveTypeCode.Object).Value)
                return null;
            if (!_runtimeTypeDeclarations.TryGetValue(target.Handle, out var input))
            {
                if(_metadata.Remaining==0) Fail(work,"Clone declaration metadata budget exhausted.",instruction.Offset,DiagnosticIds.TranslationLimit);
                input=Require(resolver.ReadMethodDeclarationInput(library,target.Handle,new(MaximumMembers:_metadata.Remaining)),work,instruction.Offset);
                if(!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work,"Clone declaration metadata budget exhausted.",instruction.Offset,DiagnosticIds.TranslationLimit);
                _runtimeTypeDeclarations.Add(target.Handle,input);
            }
            if(input.DeclaredName!="MemberwiseClone")return null;
            if(instruction.OpCode is not ("call" or "callvirt") || input.IsReferenceAssembly || input.HasBody || input.HasTypeInitializer || input.HasModuleInitializer
                || target.DeclaringType.Arguments.Count!=0 || target.MethodArguments.Count!=0 || input.OwnerGenericArity!=0
                || input.Attributes!=(MethodAttributes.Family|MethodAttributes.HideBySig) || input.Implementation!=MethodImplAttributes.InternalCall
                || input.Signature.Header!=0x20 || input.Signature.GenericArity!=0 || input.Signature.Parameters.Count!=0
                || input.Signature.ReturnType!=new TypeSignature.Primitive(PrimitiveTypeCode.Object))
                Fail(work,"Invalid selected MemberwiseClone declaration or call form.",instruction.Offset);
            _=Require(resolver.AuthorizeLibraryIntrinsic(library,target.Handle,LibraryIntrinsicCapability.ObjectMemberwiseClone),work,instruction.Offset);
            var current=work.Resolved.DeclaringType;
            var shape=SnapshotClosed(current,work,instruction.Offset);
            if((shape.Attributes&(TypeAttributes.Sealed|TypeAttributes.Abstract|TypeAttributes.Interface))!=TypeAttributes.Sealed)
                Fail(work,"MemberwiseClone requires a sealed application class receiver declared by the caller.",instruction.Offset);
            var visited=new HashSet<ObjectTypeId>();
            while(ClosedId(current,work,instruction.Offset)!=ObjectBase())
            {
                if(current.Definition.Assembly==library || !visited.Add(ClosedId(current,work,instruction.Offset)) || visited.Count>ObjectModelLimits.MaximumOwnerDepth)
                    Fail(work,"MemberwiseClone requires an ordinary application-class hierarchy ending at Object; special runtime objects are unsupported.",instruction.Offset);
                shape=SnapshotClosed(current,work,instruction.Offset);
                ConstructedType? next=shape.BaseType switch {
                    TypeSignature.Named {Kind:SignatureTypeKind.Class or SignatureTypeKind.Unknown} n=>new(n.Definition,SignatureList<TypeSignature>.Empty),
                    TypeSignature.Generic {Definition.Kind:SignatureTypeKind.Class} g=>new(g.Definition.Definition,g.Arguments),
                    _=>null! };
                if(next is null)Fail(work,"MemberwiseClone requires an ordinary application-class hierarchy ending at Object.",instruction.Offset);
                current=next!;
            }
            return new(ValueIntrinsic.ObjectClone,work.Binding.Owner,1);
        }
    }
}
