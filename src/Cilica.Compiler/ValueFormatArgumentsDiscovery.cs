using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;
public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private readonly Dictionary<IrMethodId, HashSet<int>> _formatEntries = [];
        // A bounded candidate scan only. Translation independently checks actual
        // argument types and each box producer before accepting the selected helper.
        private ImmutableArray<(TypeSignature Signature, ObjectTypeId Proof)>? MixedFormatArguments(Work work, int callIndex, int count)
        {
            if (!_formatEntries.TryGetValue(work.Binding.Method, out var entries))
            {
                entries = work.Input.Instructions.SelectMany(i => i.Targets).ToHashSet();
                foreach (var region in work.Regions)
                { entries.Add(region.TryStart); entries.Add(region.TryEnd); entries.Add(region.HandlerStart); entries.Add(region.HandlerEnd);
                    if (region.FilterStart is { } filter) entries.Add(filter); }
                _formatEntries.Add(work.Binding.Method, entries);
            }
            if (entries.Contains(work.Input.Instructions[callIndex].Offset)) return null;
            int cursor = callIndex - 1, remaining = 256;
            var text = IncludeString(work, work.Input.Instructions[callIndex].Offset);
            var candidates = new MethodInstruction[count];
            bool hasString = false;
            for (int argument = count - 1; argument >= 0; --argument)
            {
                if (cursor < 0) return null;
                var producer = work.Input.Instructions[cursor];
                if (!Consume(out var storage)) return null;
                candidates[argument] = producer;
                if (producer.OpCode == "box") continue;
                if (storage != IrType.Reference(text) && storage != IrType.NullReference) return null;
                hasString = true;
            }
            // Preserve the established one/two-box path without resolving its
            // candidate declarations a second time.
            if (!hasString && count != 3) return null;
            var result = ImmutableArray.CreateBuilder<(TypeSignature Signature, ObjectTypeId Proof)>(count);
            foreach (var producer in candidates)
            {
                if (producer.OpCode != "box")
                { result.Add((new TypeSignature.Primitive(PrimitiveTypeCode.String), text)); continue; }
                var signature = Require(resolver.ResolveClosedTypeOperand(work.Resolved,
                    MetadataTokens.EntityHandle(Token(work, producer, 0x01, 0x02, 0x1b))), work, producer.Offset);
                var receiver = FormattingReceiver(signature, work, producer.Offset);
                var named = new TypeSignature.Named(receiver.Definition, SignatureTypeKind.ValueType);
                result.Add((receiver.Arguments.Count == 0 ? named : new TypeSignature.Generic(named, receiver.Arguments), Include(receiver, work, producer.Offset)));
            }
            return result.ToImmutable();

            bool Consume(out IrType? storage)
            {
                storage = null;
                if (cursor < 0 || --remaining < 0) return false;
                var instruction = work.Input.Instructions[cursor--]; int pops;
                switch (instruction.OpCode)
                {
                    case "ldarg.0" or "ldarg.1" or "ldarg.2" or "ldarg.3" or "ldarg.s" or "ldarg":
                        if (instruction.IntegerOperand is not { } arg || (uint)arg >= (uint)work.Parameters.Length) return false;
                        storage = work.Parameters[arg]; pops = 0; break;
                    case "ldloc.0" or "ldloc.1" or "ldloc.2" or "ldloc.3" or "ldloc.s" or "ldloc":
                        if (instruction.IntegerOperand is not { } local || (uint)local >= (uint)work.Locals.Length) return false;
                        storage = work.Locals[local]; pops = 0; break;
                    case "ldstr": storage = IrType.Reference(text); pops = 0; break;
                    case "ldnull": storage = IrType.NullReference; pops = 0; break;
                    case "box": pops = 1; break;
                    case "call" or "callvirt" or "newobj":
                        var method = Require(resolver.ResolveClosedCall(work.Resolved,
                            MetadataTokens.EntityHandle(Token(work, instruction, 0x06, 0x0a, 0x2b)), _metadata), work, instruction.Offset);
                        pops = method.Signature.Parameters.Count + (instruction.OpCode != "newobj" && (method.Signature.Header & 0x20) != 0 ? 1 : 0);
                        if (instruction.OpCode != "newobj")
                        {
                            if (method.Signature.ReturnType is TypeSignature.Primitive { Code: PrimitiveTypeCode.Void }) return false;
                            storage = Storage(method.Signature.ReturnType, work, instruction.Offset);
                        }
                        break;
                    case "ldfld" or "ldsfld":
                        var field = Require(resolver.ResolveClosedField(work.Resolved,
                            MetadataTokens.EntityHandle(Token(work, instruction, 0x04, 0x0a)), _metadata), work, instruction.Offset);
                        storage = Storage(field.Signature, work, instruction.Offset); pops = instruction.OpCode == "ldfld" ? 1 : 0; break;
                    default:
                        if (instruction.OpCode.StartsWith("ldc.i4", System.StringComparison.Ordinal) || instruction.OpCode == "ldc.i8") pops = 0;
                        else if (instruction.OpCode.StartsWith("conv.", System.StringComparison.Ordinal) || instruction.OpCode is "neg" or "not") pops = 1;
                        else if (instruction.OpCode is "add" or "sub" or "mul" or "div" or "rem" or "and" or "or" or "xor" or "shl" or "shr" or "shr.un") pops = 2;
                        else return false;
                        break;
                }
                // Never borrow a producer from a predecessor block, including a
                // branch directly to a box with its payload already on the stack.
                if (entries.Contains(instruction.Offset)) return false;
                for (int i = 0; i < pops; ++i) if (!Consume(out _)) return false;
                return true;
            }
        }
    }
}
