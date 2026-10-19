using System;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Compiler;

internal sealed partial class ObjectIrRules
{
    private void ValidateConstructorFlow(IrMethod method, ObjectMethodBinding binding, string origin)
    {
        void ConstructorRequire(bool condition, string message)
        { if (!condition) Require(false, message + origin); }
        ConstructorRequire(method.Blocks.Length == 1 || _program.Flow.Types.Integer64,
            "constructors require straight-line initialization outside the integer-object profile.");
        // Each block has one agreed incoming initialization state. Conflicting
        // paths, prefix reentry and repeated initialization fail closed.
        var incoming = new bool?[method.Blocks.Length];
        var pending = new Queue<int>();
        void Admit(int id, bool initialized)
        {
            if (incoming[id] is bool prior)
                ConstructorRequire(prior == initialized, "constructor paths disagree about receiver initialization.");
            else { incoming[id] = initialized; pending.Enqueue(id); }
        }
        Admit(0, false);
        var protectedChanges = new int[method.Blocks.Length + 1];
        foreach (var region in method.ExceptionRegions)
        {
            protectedChanges[region.TryStart]++; protectedChanges[region.TryEnd]--;
            Admit(region.HandlerStart, true);
            if (region.FilterStart is int filter) Admit(filter, true);
        }
        var protectedBlocks = new bool[method.Blocks.Length]; int protection = 0;
        for (int i = 0; i < protectedBlocks.Length; i++) { protection += protectedChanges[i]; protectedBlocks[i] = protection != 0; }
        int initializers = 0;
        while (pending.TryDequeue(out int id))
        {
            var block = method.Blocks[id]; bool initialized = incoming[id]!.Value;
            ConstructorRequire(initialized || !protectedBlocks[id],
                "constructor initialization prefix cannot be protected by a local handler.");
            var values = block.Values;
            var types = values.Select(v => v.Type).ToArray();
            var expressions = values.Select(v => v.Expression).ToArray();
            bool This(int value) => expressions[value] is IrExpression.Parameter { Index: 0 };
            foreach (var statement in block.Statements)
            {
                if (statement is IrStatement.Invoke call && _bindings.TryGetValue(call.Target, out var callee) && Constructor(callee))
                {
                    ConstructorRequire(!initialized && ++initializers == 1 && ConstructorInitializationCall(method, call, types, expressions)
                        && call.Arguments.Skip(1).All(a => !This(a)),
                        "constructor must call its immediate base or same-owner constructor once on its direct receiver.");
                    if (callee.Owner == binding.Owner) _constructorChains.Add(binding.Method, callee.Method);
                    initialized = true;
                }
                else if (!initialized)
                {
                    // CLI permits declared-field initialization before the base call.
                    // Otherwise uninitialized this cannot be observed, aliased or escaped.
                    bool valid = statement switch
                    {
                        IrStatement.Define d => d.Value.Expression switch
                        {
                            IrExpression.Parameter or IrExpression.Constant or IrExpression.Null or IrExpression.Local => true,
                            IrExpression.StackInput when _program.Flow.Types.Integer64 => true,
                            IrExpression.Constant64 or IrExpression.DefaultValue or IrExpression.AddressLocal when _program.Flow.Types.Integer64 => true,
                            IrExpression.AddressParameter p when _program.Flow.Types.Integer64 => p.Index != 0,
                            IrExpression.Binary64 b when _program.Flow.Types.Integer64 => !This(b.Left) && !This(b.Right),
                            IrExpression.Not64 n when _program.Flow.Types.Integer64 => !This(n.Operand),
                            IrExpression.ConvertInteger c when _program.Flow.Types.Integer64 => !This(c.Operand),
                            IrExpression.IntegerCast c when _program.Flow.Types.Integer64 => !This(c.Operand),
                            IrExpression.CheckedNativeIndex c when _program.Flow.Types.Integer64 => !This(c.Operand),
                            IrExpression.ValueField f when _program.Flow.Types.Integer64 => !This(f.Operand),
                            IrExpression.WithValueField f when _program.Flow.Types.Integer64 => !This(f.Operand) && !This(f.Value),
                            IrExpression.LocationField f when _program.Flow.Types.Integer64 => !This(f.Location),
                            IrExpression.ReadOnlyLocation r when _program.Flow.Types.Integer64 => !This(r.Location),
                            IrExpression.LoadLocation l when _program.Flow.Types.Integer64 => !This(l.Location),
                            IrExpression.AddressField f when _program.Flow.Types.Integer64 => !This(f.Receiver),
                            IrExpression.AddressArraySlot a when _program.Flow.Types.Integer64 => !This(a.Array) && !This(a.Index),
                            IrExpression.Binary b => !This(b.Left) && !This(b.Right),
                            IrExpression.Unary u => !This(u.Operand),
                            IrExpression.Compare c => !This(c.Left) && !This(c.Right),
                            IrExpression.Call c => c.Arguments.All(a => !This(a)),
                            IrExpression.NewObject n => n.Arguments.All(a => !This(a)),
                            IrExpression.NewDelegate capture when _program.Flow.Types.Integer64 => !This(capture.Receiver),
                            IrExpression.CallDelegate callDelegate when _program.Flow.Types.Integer64 =>
                                !This(callDelegate.Delegate) && callDelegate.Arguments.All(a => !This(a)),
                            IrExpression.NewException e when _program.Flow.Types.Integer64 =>
                                new[] { e.Message, e.ParameterName, e.Inner, e.TypeName, e.ActualValue, e.ObjectName }.All(a => a is null || !This(a.Value)),
                            IrExpression.StringLiteral or IrExpression.InvariantCulture or IrExpression.RuntimeTypeLiteral => true,
                            IrExpression.ObjectDefaultText t => !This(t.Operand),
                            IrExpression.ObjectGetType t => !This(t.Operand),
                            IrExpression.RuntimeTypeHash t => !This(t.Operand),
                            IrExpression.ObjectIdentityHash t => !This(t.Operand),
                            IrExpression.ObjectClone t => !This(t.Operand),
                            IrExpression.IntegerTextFind s => !This(s.Index),
                            IrExpression.IntegerTextPublish s => !This(s.Index) && !This(s.Text),
                            IrExpression.StringLength s => !This(s.Operand),
                            IrExpression.StringCodeUnit s => !This(s.Operand) && !This(s.Index),
                            IrExpression.StringEquals s => !This(s.Left) && !This(s.Right),
                            IrExpression.StringFromCodeUnits s => !This(s.Array),
                            IrExpression.StringFromCharacter s => !This(s.Character),
                            IrExpression.StringConcat s => !This(s.Left) && !This(s.Right),
                            IrExpression.EmptyArray or IrExpression.LoadStatic or IrExpression.AddressStatic => true,
                            IrExpression.NewArray a => !This(a.Length),
                            IrExpression.ArrayLength a => !This(a.Array),
                            IrExpression.LoadElement a => !This(a.Array) && !This(a.Index),
                            IrExpression.LoadField f => !This(f.Receiver),
                            IrExpression.CastClass c => !This(c.Operand),
                            IrExpression.IsInstance t => !This(t.Operand),
                            _ => false,
                        },
                        IrStatement.StoreLocal s => !This(s.Value),
                        IrStatement.StoreLocation s when _program.Flow.Types.Integer64 => !This(s.Location) && !This(s.Value),
                        IrStatement.InitializeLocation s when _program.Flow.Types.Integer64 => !This(s.Location),
                        IrStatement.InitializeType when _program.Flow.Types.Integer64 => true,
                        IrStatement.Invoke c => c.Arguments.All(a => !This(a)),
                        IrStatement.InvokeDelegate callDelegate when _program.Flow.Types.Integer64 =>
                            !This(callDelegate.Delegate) && callDelegate.Arguments.All(a => !This(a)),
                        IrStatement.StoreStatic s => !This(s.Value),
                        IrStatement.StoreField s => !This(s.Value) && (!This(s.Receiver) || s.Field.Owner == binding.Owner),
                        IrStatement.StoreElement s => !This(s.Array) && !This(s.Index) && !This(s.Value),
                        _ => false,
                    };
                    ConstructorRequire(valid, "uninitialized constructor receiver cannot be read, aliased or passed before base initialization: "
                        + (statement is IrStatement.Define definition ? definition.Value.Expression.GetType().Name : statement.GetType().Name) + ".");

                }
            }
            void Edge(IrEdge edge)
            {
                ConstructorRequire(edge.Target != 0, "constructor body cannot re-enter its initialization prefix.");
                ConstructorRequire(initialized || edge.Arguments.All(a => !This(a)),
                    "uninitialized constructor receiver cannot be aliased through branch inputs.");
                Admit(edge.Target, initialized);
            }
            switch (block.Terminator)
            {
                case IrJump jump: Edge(jump.Edge); break;
                case IrBranch branch:
                    ConstructorRequire(initialized || !This(branch.Left) && (branch.Right is null || !This(branch.Right.Value)),
                        "uninitialized constructor receiver cannot be tested by a branch.");
                    Edge(branch.Taken); Edge(branch.NotTaken); break;
                case IrThrow thrown:
                    ConstructorRequire(initialized || !This(thrown.Value), "uninitialized constructor receiver cannot be thrown."); break;
                case IrLeave leave:
                    ConstructorRequire(initialized, "constructor cannot leave a protected region before initialization."); Edge(leave.Edge); break;
                default:
                    ConstructorRequire(initialized, "constructor must initialize its direct receiver before returning or completing a handler."); break;
            }
        }
        ConstructorRequire(initializers == 1 && incoming.All(s => s is not null),
            "constructor must initialize its direct receiver through its immediate base or same-owner constructor.");
    }
}
