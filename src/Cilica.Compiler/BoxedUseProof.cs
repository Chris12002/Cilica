using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Cilica.Compiler;

// A deliberately narrow proof: reference identities may only be inspected,
// unboxed, or passed to ordinary bodies whose parameter has the same property.
// No heap/local/edge/return alias analysis is implied. Unknown uses fail closed.
internal sealed class BoxedUseProof(IEnumerable<IrMethod> methods)
{
    private readonly Dictionary<IrMethodId, IrMethod> _methods = methods.ToDictionary(m => IrMethodId.From(m.Source));
    private readonly Dictionary<(IrMethodId Method, int Parameter), bool> _parameters = [];
    private int _work;
    private void Step()
    {
        if (++_work > ObjectModelLimits.MaximumTypeRelationWork)
            throw new ArgumentException("Boxed use proof exceeds its bounded work limit.");
    }
    internal bool Safe(IrBlock block, int value) => Uses(block, [value], 0);
    private bool Parameter(IrMethodId target, int parameter, int depth)
    {
        Step();
        if (depth >= 64 || !_methods.TryGetValue(target, out var method)) return false;
        var key = (target, parameter);
        if (_parameters.TryGetValue(key, out bool safe)) return safe;
        _parameters.Add(key, false); // Recursive argument flow is not proven safe.
        foreach (var block in method.Blocks)
        {
            var aliases = new HashSet<int>();
            foreach (var value in block.Values)
            {
                Step();
                if (value.Expression is IrExpression.AddressParameter address && address.Index == parameter) return false;
                if (value.Expression is IrExpression.Parameter source && source.Index == parameter) aliases.Add(value.Id);
            }
            if (!Uses(block, aliases, depth + 1)) return false;
        }
        _parameters[key] = true; return true;
    }
    private bool Call(IrMethodId target, ImmutableArray<int> arguments, bool dispatch, HashSet<int> aliases, int depth)
    {
        for (int index = 0; index < arguments.Length; ++index)
        {
            Step();
            if (aliases.Contains(arguments[index]) && (dispatch || !Parameter(target, index, depth))) return false;
        }
        return true;
    }
    private bool Uses(IrBlock block, HashSet<int> aliases, int depth)
    {
        if (aliases.Count == 0) return true;
        bool Used(IEnumerable<int> operands)
        {
            foreach (int operand in operands) { Step(); if (aliases.Contains(operand)) return true; }
            return false;
        }
        foreach (var statement in block.Statements)
        {
            Step();
            switch (statement)
            {
                case IrStatement.Define { Value: var value }:
                    switch (value.Expression)
                    {
                        case IrExpression.Call call:
                            if (!Call(call.Target, call.Arguments, call.VirtualDispatch, aliases, depth)) return false;
                            break;
                        case IrExpression.CheckBox check when aliases.Contains(check.Operand): aliases.Add(value.Id); break;
                        // Exact payload tests preserve the original reference on
                        // success. Follow that alias; never exempt interface tests.
                        case IrExpression.IsInstance { BoxedTarget: true } test when aliases.Contains(test.Operand): aliases.Add(value.Id); break;
                        case IrExpression.CastClass { BoxedTarget: true } cast when aliases.Contains(cast.Operand): aliases.Add(value.Id); break;
                        case IrExpression.UnboxValue or IrExpression.UnboxLocation or IrExpression.AddressBoxSlot or IrExpression.Compare or IrExpression.ObjectGetType or IrExpression.ObjectIdentityHash:
                            break;
                        default:
                            if (Used(Operands(value.Expression))) return false;
                            break;
                    }
                    break;
                case IrStatement.Invoke call:
                    if (!Call(call.Target, call.Arguments, call.VirtualDispatch, aliases, depth)) return false;
                    break;
                case IrStatement.InitializeType: break;
                case IrStatement.StoreLocal store when !aliases.Contains(store.Value): break;
                case IrStatement.StoreField store when !aliases.Contains(store.Value) && !aliases.Contains(store.Receiver): break;
                case IrStatement.StoreStatic store when !aliases.Contains(store.Value): break;
                case IrStatement.StoreElement store when !aliases.Contains(store.Value) && !aliases.Contains(store.Array) && !aliases.Contains(store.Index): break;
                case IrStatement.StoreLocation store when !aliases.Contains(store.Value) && !aliases.Contains(store.Location): break;
                case IrStatement.InitializeLocation initialize when !aliases.Contains(initialize.Location): break;
                case IrStatement.InvokeDelegate call when !aliases.Contains(call.Delegate) && !Used(call.Arguments): break;
                default: return false;
            }
        }
        bool Edge(IrEdge edge) => !Used(edge.Arguments);
        return block.Terminator switch {
            IrReturn returned => !aliases.Contains(returned.Value), IrThrow thrown => !aliases.Contains(thrown.Value),
            IrJump jump => Edge(jump.Edge), IrLeave leave => Edge(leave.Edge),
            IrBranch branch => Edge(branch.Taken) && Edge(branch.NotTaken),
            IrEndFilter filter => !aliases.Contains(filter.Value),
            IrVoidReturn or IrRethrow or IrEndFinally => true, _ => false };
    }
    private static IEnumerable<int> Operands(IrExpression expression) => expression switch {
        IrExpression.Constant or IrExpression.Constant64 or IrExpression.Null or IrExpression.Parameter or IrExpression.Local or IrExpression.StackInput
            or IrExpression.DefaultValue or IrExpression.StringLiteral or IrExpression.InvariantCulture or IrExpression.RuntimeTypeLiteral or IrExpression.EmptyArray
            or IrExpression.LoadStatic or IrExpression.AddressLocal or IrExpression.AddressParameter => [],
        IrExpression.Binary b => [b.Left, b.Right], IrExpression.Binary64 b => [b.Left, b.Right],
        IrExpression.Unary u => [u.Operand], IrExpression.Not64 u => [u.Operand],
        IrExpression.ConvertInteger c => [c.Operand], IrExpression.IntegerCast c => [c.Operand],
        IrExpression.NewException e => new[] {e.Message, e.ParameterName, e.Inner, e.TypeName}.Where(v => v.HasValue).Select(v => v!.Value),
        IrExpression.ObjectIdentityHash e => [e.Operand],
        IrExpression.ObjectGetType e => [e.Operand], IrExpression.RuntimeTypeHash e => [e.Operand],
        IrExpression.ExceptionMessage e => [e.Operand], IrExpression.ExceptionParameterName e => [e.Operand],
        IrExpression.ExceptionInnerException e => [e.Operand], IrExpression.ExceptionTypeName e => [e.Operand],
        IrExpression.NewDelegate d => [d.Receiver], IrExpression.CallDelegate d => d.Arguments.Prepend(d.Delegate),
        IrExpression.NewObject n => n.Arguments, IrExpression.NewArray n => [n.Length], IrExpression.ArrayLength a => [a.Array],
        IrExpression.LoadElement a => [a.Array, a.Index], IrExpression.AddressElement a => [a.Array, a.Index],
        IrExpression.AddressArraySlot a => [a.Array, a.Index],
        IrExpression.IntegerTextFind t => [t.Index], IrExpression.IntegerTextPublish t => [t.Index, t.Text],
        IrExpression.StringLength t => [t.Operand], IrExpression.StringCodeUnit t => [t.Operand, t.Index],
        IrExpression.StringEquals t => [t.Left, t.Right], IrExpression.StringConcat t => [t.Left, t.Right], IrExpression.StringFromCodeUnits t => [t.Array],
        IrExpression.LoadField f => [f.Receiver], IrExpression.AddressField f => [f.Receiver],
        IrExpression.CastClass c => [c.Operand], IrExpression.IsInstance c => [c.Operand],
        IrExpression.ValueField v => [v.Operand], IrExpression.WithValueField v => [v.Operand, v.Value],
        IrExpression.BoxValue b => [b.Operand], IrExpression.BoxScalars b => b.Values,
        IrExpression.CheckBox b => [b.Operand], IrExpression.LocationField l => [l.Location],
        IrExpression.ReadOnlyLocation l => [l.Location], IrExpression.LoadLocation l => [l.Location],
        _ => throw new ArgumentException("Boxed use proof does not recognize this operation.") };
}
