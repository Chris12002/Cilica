using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;
public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private readonly Dictionary<IrMethodId, HashSet<int>> _vectorCallEntries = [];
        // Candidate proof only; normal call lowering independently checks stack types.
        private (TypeSignature? Signature, bool LiteralNull)[]? VectorCallArguments(Work work, int callIndex, int count)
        {
            if (!_vectorCallEntries.TryGetValue(work.Binding.Method, out var entries))
            {
                entries = work.Input.Instructions.SelectMany(i => i.Targets).ToHashSet();
                foreach (var region in work.Regions)
                {
                    entries.Add(region.TryStart); entries.Add(region.TryEnd); entries.Add(region.HandlerStart); entries.Add(region.HandlerEnd);
                    if (region.FilterStart is { } filter) entries.Add(filter);
                }
                _vectorCallEntries.Add(work.Binding.Method, entries);
            }
            if (entries.Contains(work.Input.Instructions[callIndex].Offset)) return null;
            int cursor = callIndex - 1, remaining = 128;
            var arguments = new (TypeSignature? Signature, bool LiteralNull)[count];
            for (int i = count - 1; i >= 0; --i)
            {
                if (cursor < 0) return null;
                bool literalNull = work.Input.Instructions[cursor].OpCode == "ldnull";
                if (!Consume(out var signature)) return null;
                arguments[i] = (signature, literalNull);
            }
            return arguments;

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
