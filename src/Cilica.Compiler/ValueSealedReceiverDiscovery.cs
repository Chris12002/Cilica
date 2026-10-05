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
        // Narrow only an immediately produced, statically sealed reference on a
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
                { entries.Add(region.HandlerStart); if (region.FilterStart is { } filter) entries.Add(filter); }
                producers = [];
                for (int index = 1; index < work.Input.Instructions.Length; ++index)
                {
                    var current = work.Input.Instructions[index]; var prior = work.Input.Instructions[index - 1];
                    if (!entries.Contains(current.Offset) && prior.Offset + prior.Length == current.Offset)
                        producers.Add(current.Offset, prior);
                }
                _receiverProducers.Add(work.Binding.Method, producers);
            }
            if (!producers.TryGetValue(instruction.Offset, out var previous)) return target;
            IrType? storage = null;
            switch (previous.OpCode)
            {
                case "ldloc.0" or "ldloc.1" or "ldloc.2" or "ldloc.3" or "ldloc.s" or "ldloc":
                    if (previous.IntegerOperand is { } local && (uint)local < (uint)work.Locals.Length) storage = work.Locals[local];
                    break;
                case "ldarg.0" or "ldarg.1" or "ldarg.2" or "ldarg.3" or "ldarg.s" or "ldarg":
                    if (previous.IntegerOperand is { } argument && (uint)argument < (uint)work.Parameters.Length) storage = work.Parameters[argument];
                    break;
                case "ldfld":
                    var field = Require(resolver.ResolveClosedField(work.Resolved,
                        MetadataTokens.EntityHandle(Token(work, previous, 0x04, 0x0a)), _metadata), work, previous.Offset);
                    storage = Storage(field.Signature, work, previous.Offset); break;
                case "newobj" or "call" or "callvirt":
                    var producer = Require(resolver.ResolveClosedCall(work.Resolved,
                        MetadataTokens.EntityHandle(Token(work, previous, 0x06, 0x0a, 0x2b)), _metadata), work, previous.Offset);
                    storage = previous.OpCode == "newobj" ? IrType.Reference(ObjectModelBuilder.Id(producer.DeclaringType))
                        : Storage(producer.Signature.ReturnType, work, previous.Offset, allowVoid: true);
                    break;
            }
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
    }
}
