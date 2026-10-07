using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;
public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private readonly Dictionary<IrMethodId, bool> _publicVirtualDeclarations = [];
        private readonly Dictionary<IrMethodId, Dictionary<int, MethodInstruction>> _receiverProducers = [];
        // Narrow only a statically sealed reference proven within one bounded block on a
        // zero-argument callvirt. Normal typed stack validation still checks the
        // receiver, and ordinary virtual dispatch retains its null check and slot.
        private ResolvedMethod NarrowVirtualReceiver(Work work, MethodInstruction instruction, ResolvedMethod target)
        {
            if (!integers64 || instruction.OpCode != "callvirt" || target.Signature.Parameters.Count != 0
                || target.Signature.Header != 0x20 || target.MethodArguments.Count != 0) return target;
            if (!_receiverProducers.TryGetValue(work.Binding.Method, out var producers))
            {
                // Build the predecessor map once, keeping proof work linear in
                // the bounded instruction/edge input rather than scanning per call.
                var entries = work.Input.Instructions.SelectMany(i => i.Targets).ToHashSet();
                foreach (var region in work.Regions)
                { entries.Add(region.TryStart); entries.Add(region.TryEnd); entries.Add(region.HandlerStart); entries.Add(region.HandlerEnd);
                    if (region.FilterStart is { } filter) entries.Add(filter); }
                producers = [];
                for (int index = 1; index < work.Input.Instructions.Length; ++index)
                {
                    var current = work.Input.Instructions[index]; var prior = work.Input.Instructions[index - 1];
                    if (!entries.Contains(current.Offset) && prior.Offset + prior.Length == current.Offset)
                        producers.Add(current.Offset, prior);
                }
                _receiverProducers.Add(work.Binding.Method, producers);
            }
            var storage = SealedReceiverStorage(work, instruction.Offset, producers);
            if (storage is not { Kind: IrTypeKind.Reference, Class: { ArrayElement: null } receiver }
                || !_references.TryGetValue(receiver, out var type)
                || (SnapshotClosed(type, work, instruction.Offset).Attributes & (TypeAttributes.Sealed | TypeAttributes.Interface)) != TypeAttributes.Sealed)
                return target;
            var id = MethodId(target);
            if (!_publicVirtualDeclarations.TryGetValue(id, out bool isVirtual))
            {
                if (_metadata.Remaining == 0) Fail(work, "Sealed receiver declaration metadata budget exhausted.", instruction.Offset, Cilica.Metadata.DiagnosticIds.TranslationLimit);
                var declaration = Require(resolver.ReadMethodDeclarationInput(target.DeclaringType.Definition.Assembly,
                    target.Handle, new(MaximumMembers: _metadata.Remaining)), work, instruction.Offset);
                if (!_metadata.TryConsume(declaration.MetadataMembersRead)) Fail(work, "Sealed receiver declaration metadata budget exceeded.", instruction.Offset, Cilica.Metadata.DiagnosticIds.TranslationLimit);
                isVirtual = declaration.OwnerExternallyVisible
                    && (declaration.Attributes & (MethodAttributes.Virtual | MethodAttributes.MemberAccessMask))
                        == (MethodAttributes.Virtual | MethodAttributes.Public);
                _publicVirtualDeclarations.Add(id, isVirtual);
            }
            return isVirtual ? ResolveVirtual(target, type, work, instruction.Offset).Implementation : target;
        }
        // Track one stack slot backwards. Calls can consume a duplicate while
        // preserving the original below their arguments. This is a candidate
        // identity only: ordinary translation independently checks the real SSA
        // receiver and retains null checks, dispatch and fault ordering.
        private IrType? SealedReceiverStorage(Work work, int offset, Dictionary<int, MethodInstruction> producers)
        {
            int depth = 0;
            for (int remaining = 256; remaining > 0 && producers.TryGetValue(offset, out var instruction); --remaining)
            {
                offset = instruction.Offset;
                int pops = 0, pushes = 0;
                IrType? storage = null;
                switch (instruction.OpCode)
                {
                    case "dup":
                        // Both output slots refer to the same input slot.
                        if (depth > 0) --depth;
                        continue;
                    case "nop": continue;
                    case "pop": pops = 1; break;
                    case "ldloc.0" or "ldloc.1" or "ldloc.2" or "ldloc.3" or "ldloc.s" or "ldloc":
                        pushes = 1;
                        if (depth == 0 && instruction.IntegerOperand is { } local && (uint)local < (uint)work.Locals.Length) storage = work.Locals[local];
                        break;
                    case "ldarg.0" or "ldarg.1" or "ldarg.2" or "ldarg.3" or "ldarg.s" or "ldarg":
                        pushes = 1;
                        if (depth == 0 && instruction.IntegerOperand is { } argument && (uint)argument < (uint)work.Parameters.Length) storage = work.Parameters[argument];
                        break;
                    case "ldfld" or "ldsfld":
                        pushes = 1; pops = instruction.OpCode == "ldfld" ? 1 : 0;
                        if (depth == 0)
                        {
                            var field = Require(resolver.ResolveClosedField(work.Resolved,
                                MetadataTokens.EntityHandle(Token(work, instruction, 0x04, 0x0a)), _metadata), work, offset);
                            storage = Storage(field.Signature, work, offset);
                        }
                        break;
                    case "stloc.0" or "stloc.1" or "stloc.2" or "stloc.3" or "stloc.s" or "stloc" or "starg" or "starg.s" or "stsfld": pops = 1; break;
                    case "stfld": pops = 2; break;
                    case "ldstr" or "ldnull" or "ldloca" or "ldloca.s" or "ldarga" or "ldarga.s": pushes = 1; break;
                    case "call" or "callvirt" or "newobj":
                        var producer = Require(resolver.ResolveClosedCall(work.Resolved,
                            MetadataTokens.EntityHandle(Token(work, instruction, 0x06, 0x0a, 0x2b)), _metadata), work, offset);
                        pops = producer.Signature.Parameters.Count + (instruction.OpCode != "newobj" && (producer.Signature.Header & 0x20) != 0 ? 1 : 0);
                        pushes = instruction.OpCode == "newobj" || producer.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Void } ? 1 : 0;
                        if (depth == 0 && pushes != 0)
                            storage = instruction.OpCode == "newobj" ? IrType.Reference(ObjectModelBuilder.Id(producer.DeclaringType))
                                : Storage(producer.Signature.ReturnType, work, offset, allowVoid: true);
                        break;
                    default:
                        if (instruction.OpCode.StartsWith("ldc.i4", StringComparison.Ordinal) || instruction.OpCode == "ldc.i8") pushes = 1;
                        else return null;
                        break;
                }
                if (depth < pushes) return storage;
                depth = depth - pushes + pops;
                if (depth > 256) return null;
            }
            return null;
        }
    }
}
