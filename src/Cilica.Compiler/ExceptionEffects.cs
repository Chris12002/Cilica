using System.Collections.Generic;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>Conservative may-fault closure for validated primitive IR, including recursive cycles.</summary>
internal static class ExceptionEffects
{
    internal static HashSet<IrMethodId> Analyze(IrProgram program)
    {
        var callers = program.Methods.ToDictionary(m => IrMethodId.From(m.Source), _ => new HashSet<IrMethodId>());
        var throwing = new HashSet<IrMethodId>(); var pending = new Queue<IrMethodId>();
        foreach (IrMethod method in program.Methods)
        {
            IrMethodId id = IrMethodId.From(method.Source);
            foreach (IrStatement.Define define in method.Blocks.SelectMany(b => b.Statements).OfType<IrStatement.Define>())
                switch (define.Value.Expression)
                {
                    case IrExpression.Call call: callers[call.Target].Add(id); break;
                    case IrExpression.Binary binary when Int32Operations.MayFault(binary.Operation):
                    case IrExpression.Binary64 wide when Int64Operations.MayFault(wide.Operation):
                        if (throwing.Add(id)) pending.Enqueue(id); break;
                }
        }
        while (pending.TryDequeue(out IrMethodId? callee))
            foreach (IrMethodId caller in callers[callee]) if (throwing.Add(caller)) pending.Enqueue(caller);
        return throwing;
    }
}
