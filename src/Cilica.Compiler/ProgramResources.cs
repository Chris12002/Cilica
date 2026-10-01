using System.Linq;

namespace Cilica.Compiler;

/// <summary>Shared accounting for translated and caller-constructed program IR.</summary>
internal sealed class ProgramResources
{
    private long _blocks, _statements, _inputs, _edges, _parameters, _locals, _arguments;

    internal bool Add(IrMethod method)
    {
        _parameters += method.Parameters.Length; _locals += method.Locals.Length;
        _blocks += method.Blocks.Length;
        foreach (IrBlock block in method.Blocks)
        {
            _statements += block.Statements.Length; _inputs += block.Inputs.Length;
            foreach (IrStatement.Define define in block.Statements.OfType<IrStatement.Define>())
                if (define.Value.Expression is IrExpression.Call call) _arguments += call.Arguments.Length;
            switch (block.Terminator)
            {
                case IrJump jump: _edges += jump.Edge.Arguments.Length; break;
                case IrBranch branch: _edges += branch.Taken.Arguments.Length + branch.NotTaken.Arguments.Length; break;
            }
        }
        return _parameters <= IrLimits.MaximumParameters && _locals <= IrLimits.MaximumLocals
            && _blocks <= IrLimits.MaximumBlocks && _statements <= IrLimits.MaximumStatements
            && _inputs <= IrLimits.MaximumInputs && _edges <= IrLimits.MaximumEdgeArguments
            && _arguments <= ProgramLimits.MaximumCallArguments;
    }
}
