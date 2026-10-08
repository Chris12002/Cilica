using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;
public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private readonly Dictionary<IrMethodId, HashSet<int>> _clearEntries = [];

        private void ArraySectionClear(Work work, MethodInstruction instruction, int callIndex, ref ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library
                || target.DeclaringType.Definition != resolver.ResolveType(library, "System", "Array").Value
                || target.Signature.Parameters.Count != 3) return;
            int offset = instruction.Offset;
            MethodDeclarationInput Read(ResolvedMethod method)
            {
                if (_metadata.Remaining == 0) Fail(work, "Array.Clear metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
                var result = Require(resolver.ReadMethodDeclarationInput(library, method.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
                if (!_metadata.TryConsume(result.MetadataMembersRead)) Fail(work, "Array.Clear metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
                return result;
            }
            var input = Read(target);
            if (input.DeclaredName != "Clear") return;
            if (instruction.OpCode != "call" || input.IsReferenceAssembly || input.HasBody || input.HasTypeInitializer || input.HasModuleInitializer
                || input.OwnerGenericArity != 0 || target.MethodArguments.Count != 0 || target.DeclaringType.Arguments.Count != 0
                || input.Signature.Header != 0 || input.Signature.GenericArity != 0
                || input.Attributes != (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig)
                || input.Implementation != MethodImplAttributes.InternalCall
                || input.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Void }
                || input.Signature.Parameters[0] is not TypeSignature.Named { Kind: SignatureTypeKind.Class } array
                || array.Definition != target.DeclaringType.Definition
                || input.Signature.Parameters.Skip(1).Any(p => p is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 }))
                Fail(work, "Array.Clear requires the exact selected static Array, Int32, Int32 declaration.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.ArraySectionClear), work, offset);
            var element = ClearArrayElement(work, callIndex);
            if (element is null)
                Fail(work, "Array.Clear requires a typed vector producer in the same block; general Array aliases and joined argument stacks require later lowering.", offset);
            CheckArgumentAccess(element!, work, offset);
            _ = Vector(element!, work, offset);
            var owner = Require(resolver.ResolveType(library, "Cilica.Runtime", "ArraySections`1"), work, offset);
            var helper = Require(resolver.ResolveClosedDeclaration(new(owner, new([element!])), "ClearSection", 3, _metadata), work, offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, helper.Handle, LibraryIntrinsicCapability.ArraySectionClearBridge), work, offset);
            var body = Read(helper);
            if (body.IsReferenceAssembly || !body.HasBody || body.HasTypeInitializer || body.HasModuleInitializer
                || body.OwnerGenericArity != 1 || body.Signature.Header != 0 || body.Signature.GenericArity != 0
                || body.Attributes != (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig)
                || body.Implementation != MethodImplAttributes.IL
                || body.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Void }
                || body.Signature.Parameters.Count != 3
                || body.Signature.Parameters[0] is not TypeSignature.Vector { Element: TypeSignature.Variable { IsMethod: false, Index: 0 } }
                || body.Signature.Parameters.Skip(1).Any(p => p is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 }))
                Fail(work, "Array.Clear requires the exact managed typed-vector bridge.", offset);
            // Ordinary call lowering independently requires the actual stack value
            // to be assignable to this exact closed vector parameter.
            target = helper;
        }

        private TypeSignature? ClearArrayElement(Work work, int callIndex)
        {
            if (!_clearEntries.TryGetValue(work.Binding.Method, out var entries))
            {
                entries = work.Input.Instructions.SelectMany(i => i.Targets).ToHashSet();
                foreach (var region in work.Regions)
                {
                    entries.Add(region.TryStart); entries.Add(region.TryEnd); entries.Add(region.HandlerStart); entries.Add(region.HandlerEnd);
                    if (region.FilterStart is { } filter) entries.Add(filter);
                }
                _clearEntries.Add(work.Binding.Method, entries);
            }
            if (entries.Contains(work.Input.Instructions[callIndex].Offset)) return null;
            int cursor = callIndex - 1, remaining = 128;
            if (!Consume(out _) || !Consume(out _) || cursor < 0) return null;
            bool literalNull = work.Input.Instructions[cursor].OpCode == "ldnull";
            if (!Consume(out var signature)) return null;
            return literalNull ? new TypeSignature.Primitive(PrimitiveTypeCode.Int32)
                : signature is TypeSignature.Vector vector ? vector.Element : null;

            bool Consume(out TypeSignature? signature)
            {
                signature = null;
                if (cursor < 0 || --remaining < 0) return false;
                var instruction = work.Input.Instructions[cursor--];
                bool boundary = entries.Contains(instruction.Offset);
                int pops;
                switch (instruction.OpCode)
                {
                    case "ldarg.0" or "ldarg.1" or "ldarg.2" or "ldarg.3" or "ldarg.s" or "ldarg":
                        if (instruction.IntegerOperand is not { } arg) return false;
                        if ((work.Input.Signature.Header & 0x20) != 0 && arg == 0) { pops = 0; break; }
                        arg -= (work.Input.Signature.Header & 0x20) != 0 ? 1 : 0;
                        if ((uint)arg >= (uint)work.Input.Signature.Parameters.Count) return false;
                        signature = work.Input.Signature.Parameters[arg]; pops = 0; break;
                    case "ldloc.0" or "ldloc.1" or "ldloc.2" or "ldloc.3" or "ldloc.s" or "ldloc":
                        if (instruction.IntegerOperand is not { } local || (uint)local >= (uint)work.Input.Locals.Length) return false;
                        signature = work.Input.Locals[local]; pops = 0; break;
                    case "ldfld" or "ldsfld":
                        var field = Require(resolver.ResolveClosedField(work.Resolved,
                            MetadataTokens.EntityHandle(Token(work, instruction, 0x04, 0x0a)), _metadata), work, instruction.Offset);
                        signature = field.Signature; pops = instruction.OpCode == "ldfld" ? 1 : 0; break;
                    case "newarr" or "castclass":
                        var operand = Require(resolver.ResolveClosedTypeOperand(work.Resolved,
                            MetadataTokens.EntityHandle(Token(work, instruction, 0x01, 0x02, 0x1b))), work, instruction.Offset);
                        signature = OperandSignature(instruction.OpCode == "newarr" ? new TypeSignature.Vector(operand) : operand, work, instruction.Offset);
                        pops = 1; break;
                    case "call" or "callvirt":
                        var method = Require(resolver.ResolveClosedCall(work.Resolved,
                            MetadataTokens.EntityHandle(Token(work, instruction, 0x06, 0x0a, 0x2b)), _metadata), work, instruction.Offset);
                        if (method.Signature.ReturnType is TypeSignature.Primitive { Code: PrimitiveTypeCode.Void }) return false;
                        signature = method.Signature.ReturnType;
                        pops = method.Signature.Parameters.Count + ((method.Signature.Header & 0x20) != 0 ? 1 : 0); break;
                    case "ldnull" or "ldstr": pops = 0; break;
                    case "ldlen": pops = 1; break;
                    default:
                        if (instruction.OpCode.StartsWith("ldc.i4", StringComparison.Ordinal) || instruction.OpCode == "ldc.i8") pops = 0;
                        else if (instruction.OpCode.StartsWith("conv.", StringComparison.Ordinal) || instruction.OpCode is "neg" or "not") pops = 1;
                        else if (instruction.OpCode is "add" or "sub" or "mul" or "div" or "rem" or "and" or "or" or "xor" or "shl" or "shr" or "shr.un") pops = 2;
                        else return false;
                        break;
                }
                if (boundary && pops != 0) return false;
                for (int i = 0; i < pops; ++i) if (!Consume(out _)) return false;
                if (boundary) cursor = -1;
                return true;
            }
        }
    }
}
