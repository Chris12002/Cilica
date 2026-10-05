using System;
using System.Collections.Generic;
using Cilica.Metadata.IL;

namespace Cilica.Compiler;

public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private Dictionary<int, MethodInstruction> ConstrainedPrefixes(Work work)
        {
            var result = new Dictionary<int, MethodInstruction>();
            for (int index = 0; index < work.Input.Instructions.Length; index++)
            {
                var instruction = work.Input.Instructions[index];
                if (instruction.OpCode != "constrained.") continue;
                if (!integers64) Fail(work, "Constrained direct value calls require the integer-object profile.", instruction.Offset);
                if (index + 1 >= work.Input.Instructions.Length || work.Input.Instructions[index + 1].OpCode != "callvirt"
                    || index > 0 && work.Input.Instructions[index - 1].OpCode.EndsWith(".", StringComparison.Ordinal))
                    Fail(work, "Constrained value calls require one adjacent constrained./callvirt pair; combined prefixes and static-interface calls require later lowering.", instruction.Offset);
                result.Add(work.Input.Instructions[index + 1].Offset, instruction);
            }
            return result;
        }
    }
}
