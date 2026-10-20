using System.Collections.Generic;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>Same-block, single-use local addresses consumed immediately by a proven character reader.</summary>
internal static class ObjectCppLocalCharacterCalls
{
    internal sealed record Site(int Local, IrMethodId Target);

    internal static Dictionary<(int Block, int Address), Site> Analyze(
        IrMethod method, Dictionary<IrMethodId, ObjectCppCharacterMethods.Plan> targets)
    {
        var result = new Dictionary<(int, int), Site>();
        foreach (var block in method.Blocks)
        {
            // Keep the local use scan bounded; larger blocks retain ordinary borrows.
            if (block.Statements.Length > 128) continue;
            int candidates = 0;
            for (int i = 0; i + 1 < block.Statements.Length; i++)
            {
                if (block.Statements[i] is not IrStatement.Define { Value: var address }
                    || address.Expression is not IrExpression.AddressLocal local
                    || method.Locals[local.Index] != IrType.Int32
                    || address.Type.Element != IrType.Int32 || address.Type.Narrow != IntegerCastTarget.UInt16
                    || block.Statements[i + 1] is not IrStatement.Define { Value.Expression: IrExpression.Call call }
                    || call.VirtualDispatch || call.NullCheckReceiver || call.ValueKeyContract is not null
                    || call.Arguments.Length != 1 || call.Arguments[0] != address.Id
                    || !targets.TryGetValue(call.Target, out var target) || target.Target is null) continue;
                if (++candidates > 16) break;
                if (SingleUse(block, address.Id, i + 1))
                    result.Add((block.Id, address.Id), new(local.Index, call.Target));
            }
        }
        return result;
    }

    // Validated IR forbids managed locations as scalar/reference operands. Enumerate
    // every admitted location consumer, reject aliases/escapes and fail closed for
    // unknown operations. No cross-block or general borrow-lifetime inference.
    internal static bool SingleUse(IrBlock block, int address, int callIndex)
    {
        bool Absent(IEnumerable<int> operands) => !operands.Contains(address);
        for (int i = 0; i < block.Statements.Length; i++)
        {
            if (i == callIndex) continue;
            bool safe = block.Statements[i] switch
            {
                IrStatement.Define { Value.Expression: var e } => e switch
                {
                    IrExpression.Call c => Absent(c.Arguments),
                    IrExpression.NewObject c => Absent(c.Arguments),
                    IrExpression.CallDelegate c => c.Delegate != address && Absent(c.Arguments),
                    IrExpression.LoadLocation l => l.Location != address,
                    IrExpression.ReadOnlyLocation l => l.Location != address,
                    IrExpression.LocationField l => l.Location != address,
                    IrExpression.Constant or IrExpression.Constant64 or IrExpression.Null
                        or IrExpression.Parameter or IrExpression.Local or IrExpression.StackInput
                        or IrExpression.Binary or IrExpression.Binary64 or IrExpression.Unary or IrExpression.Not64
                        or IrExpression.ConvertInteger or IrExpression.IntegerCast or IrExpression.CheckedNativeIndex or IrExpression.Compare
                        or IrExpression.AddressLocal or IrExpression.AddressParameter or IrExpression.AddressStatic
                        or IrExpression.LoadField or IrExpression.AddressField or IrExpression.LoadStatic
                        or IrExpression.NewArray or IrExpression.EmptyArray or IrExpression.ArrayLength or IrExpression.LoadElement
                        or IrExpression.AddressArraySlot or IrExpression.ArrayCopyCompatible
                        or IrExpression.StringLiteral or IrExpression.StringLength or IrExpression.StringCodeUnit
                        or IrExpression.StringEquals or IrExpression.StringConcat or IrExpression.StringFromCharacter
                        or IrExpression.StringFromCodeUnits or IrExpression.CastClass or IrExpression.IsInstance => true,
                    _ => false,
                },
                IrStatement.StoreLocal s => s.Value != address,
                IrStatement.StoreLocation s => s.Location != address && s.Value != address,
                IrStatement.InitializeLocation s => s.Location != address,
                IrStatement.Invoke c => Absent(c.Arguments),
                IrStatement.InvokeDelegate c => c.Delegate != address && Absent(c.Arguments),
                IrStatement.StoreField s => s.Receiver != address && s.Value != address,
                IrStatement.StoreStatic s => s.Value != address,
                IrStatement.StoreElement s => s.Array != address && s.Index != address && s.Value != address,
                IrStatement.InitializeType => true,
                _ => false,
            };
            if (!safe) return false;
        }
        return block.Terminator switch
        {
            IrReturn r => r.Value != address,
            IrThrow t => t.Value != address,
            IrJump j => Absent(j.Edge.Arguments),
            IrLeave l => Absent(l.Edge.Arguments),
            IrBranch b => b.Left != address && b.Right != address && Absent(b.Taken.Arguments) && Absent(b.NotTaken.Arguments),
            IrEndFilter f => f.Value != address,
            IrVoidReturn or IrRethrow or IrEndFinally => true,
            _ => false,
        };
    }
}
