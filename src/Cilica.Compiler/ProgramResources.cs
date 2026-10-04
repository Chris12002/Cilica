using System.Linq;

namespace Cilica.Compiler;

/// <summary>Shared accounting for translated and caller-constructed program IR.</summary>
internal sealed class ProgramResources
{
    private long _regions, _blocks, _statements, _inputs, _edges, _parameters, _locals, _arguments;

    internal bool Add(IrMethod method)
    {
        _regions += method.ExceptionRegions.Length;
        _parameters += method.Parameters.Length; _locals += method.Locals.Length;
        _blocks += method.Blocks.Length;
        foreach (IrBlock block in method.Blocks)
        {
            _statements += block.Statements.Length; _inputs += block.Inputs.Length;
            foreach (IrStatement.Define define in block.Statements.OfType<IrStatement.Define>())
                switch (define.Value.Expression)
                {
                    case IrExpression.Call call: _arguments += call.Arguments.Length; break;
                    case IrExpression.NewDelegate: _arguments++; break;
                    case IrExpression.CallDelegate call: _arguments += call.Arguments.Length + 1; break;
                    case IrExpression.NewObject allocated: _arguments += allocated.Arguments.Length; break;
                    case IrExpression.BoxScalars boxed: _arguments += boxed.Values.Length; break;
                }
            foreach (IrStatement.Invoke call in block.Statements.OfType<IrStatement.Invoke>()) _arguments += call.Arguments.Length;
            foreach (IrStatement.InvokeDelegate call in block.Statements.OfType<IrStatement.InvokeDelegate>()) _arguments += call.Arguments.Length + 1;
            switch (block.Terminator)
            {
                case IrLeave leave: _edges += leave.Edge.Arguments.Length; break;
                case IrJump jump: _edges += jump.Edge.Arguments.Length; break;
                case IrBranch branch: _edges += branch.Taken.Arguments.Length + branch.NotTaken.Arguments.Length; break;
            }
        }
        return _regions <= 256 && _parameters <= IrLimits.MaximumParameters && _locals <= IrLimits.MaximumLocals
            && _blocks <= IrLimits.MaximumBlocks && _statements <= IrLimits.MaximumStatements
            && _inputs <= IrLimits.MaximumInputs && _edges <= IrLimits.MaximumEdgeArguments
            && _arguments <= ProgramLimits.MaximumCallArguments;
    }
}
