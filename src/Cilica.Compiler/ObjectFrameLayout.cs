using System.Collections.Generic;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>Distinct typed slots for phase A execution storage; target C++ supplies byte size/alignment.</summary>
internal sealed class ObjectFrameLayout
{
    private readonly Dictionary<(ObjectRootKind Kind, int Block, int Index), string> _storage = [];
    private readonly Dictionary<(ObjectRootKind Kind, int Block, int Index), int> _indices = [];
    private readonly Dictionary<(ObjectRootKind Kind, int Block, int Index), string> _owners = [];
    internal int IntegerCount { get; }
    internal int WideIntegerCount { get; }
    // Address-taken scalars occupy the leading slots; managed locations reach them by index.
    // Later scalars use CILICA_I/CILICA_W, which only the direct rendering maps to native locals.
    internal int FrameIntegerCount { get; }
    internal int FrameWideIntegerCount { get; }
    internal int RootCount { get; }
    internal int CallArgumentCount { get; }
    internal bool HasBoxedDispatch { get; }
    internal int BoxArgumentCount { get; }
    internal int LocationCount { get; }
    internal bool BorrowsFrame { get; }
    internal List<IrType> RootTypes { get; } = [];
    internal HashSet<IrTypeKind> DispatchKinds { get; } = [];

    internal ObjectFrameLayout(IrMethod method, ObjectMethodRoots roots, ObjectTypeId? exceptionBase = null, IReadOnlyDictionary<IrMethodId, int>? boxedDispatchArguments = null)
    {
        RootCount = roots.Slots.Length;
        var valueTypes = method.Blocks.SelectMany(b => b.Values.Select(v => (b.Id, v)))
            .ToDictionary(p => (Block: p.Id, Value: p.v.Id), p => p.v.Type);
        foreach (var root in roots.Slots)
        {
            _storage.Add((root.Kind, root.Block, root.Index), "f->roots[" + Number(root.Slot) + "]");
            _indices.Add((root.Kind, root.Block, root.Index), root.Slot);
            var type = root.Kind switch
            {
                ObjectRootKind.Exception => IrType.Reference(exceptionBase!),
                ObjectRootKind.Parameter => method.Parameters[root.Index],
                ObjectRootKind.Local => method.Locals[root.Index],
                ObjectRootKind.BlockInput => method.Blocks[root.Block].Inputs[root.Index],
                _ => valueTypes[(root.Block, root.Index)],
            };
            RootTypes.Add(type);
        }
        var addressed = method.Blocks.SelectMany(b => b.Values).Select(v => v.Expression switch
        {
            IrExpression.AddressParameter p => (Kind: ObjectRootKind.Parameter, p.Index),
            IrExpression.AddressLocal l => (Kind: ObjectRootKind.Local, l.Index),
            _ => (Kind: ObjectRootKind.Value, Index: -1),
        }).Where(p => p.Index >= 0).ToHashSet();
        bool Framed(ObjectRootKind kind, int index, IrTypeKind type) => addressed.Contains((kind, index)) && type is IrTypeKind.Int32 or IrTypeKind.Int64;
        FrameIntegerCount = method.Parameters.Select((t, i) => Framed(ObjectRootKind.Parameter, i, t.Kind) && t.Kind == IrTypeKind.Int32)
            .Concat(method.Locals.Select((t, i) => Framed(ObjectRootKind.Local, i, t.Kind) && t.Kind == IrTypeKind.Int32)).Count(x => x);
        FrameWideIntegerCount = method.Parameters.Select((t, i) => Framed(ObjectRootKind.Parameter, i, t.Kind) && t.Kind == IrTypeKind.Int64)
            .Concat(method.Locals.Select((t, i) => Framed(ObjectRootKind.Local, i, t.Kind) && t.Kind == IrTypeKind.Int64)).Count(x => x);
        int frameIntegers = 0, frameWideIntegers = 0;
        int integers = FrameIntegerCount, wideIntegers = FrameWideIntegerCount, arguments = 0, locations = 0;
        int CallWidth(IrMethodId target, int count, bool dispatch)
        {
            if (dispatch && boxedDispatchArguments?.TryGetValue(target, out int expanded) == true)
                return System.Math.Max(count, expanded);
            return count;
        }
        HasBoxedDispatch = method.Blocks.Any(b => b.Values.Any(v => v.Expression is IrExpression.Call { VirtualDispatch: true } call
                && boxedDispatchArguments?.ContainsKey(call.Target) == true)
            || b.Statements.OfType<IrStatement.Invoke>().Any(c => c.VirtualDispatch && boxedDispatchArguments?.ContainsKey(c.Target) == true));
        void Add(ObjectRootKind kind, int index, IrType type, int block = -1)
        {
            bool framed = Framed(kind, index, type.Kind);
            if (type.Kind == IrTypeKind.Int32)
            {
                int slot = framed ? frameIntegers++ : integers++;
                _indices.Add((kind, block, index), slot);
                _storage.Add((kind, block, index), (framed ? "f->integers[" : "CILICA_I(") + Number(slot) + (framed ? "]" : ")"));
            }
            else if (type.Kind == IrTypeKind.Int64)
            {
                int slot = framed ? frameWideIntegers++ : wideIntegers++;
                _indices.Add((kind, block, index), slot);
                _storage.Add((kind, block, index), (framed ? "f->wide_integers[" : "CILICA_W(") + Number(slot) + (framed ? "]" : ")"));
            }
            else if (type.Kind == IrTypeKind.ManagedLocation)
            {
                _owners.Add((kind, block, index), _storage[(kind, block, index)]);
                _storage[(kind, block, index)] = "f->locations[" + Number(locations++) + "]";
            }
        }
        for (int i = 0; i < method.Parameters.Length; i++) Add(ObjectRootKind.Parameter, i, method.Parameters[i]);
        for (int i = 0; i < method.Locals.Length; i++) Add(ObjectRootKind.Local, i, method.Locals[i]);
        foreach (var block in method.Blocks)
        {
            for (int i = 0; i < block.Inputs.Length; i++) Add(ObjectRootKind.BlockInput, i, block.Inputs[i], block.Id);
            foreach (var value in block.Values)
            {
                Add(ObjectRootKind.Value, value.Id, value.Type, block.Id);
                if (value.Expression is IrExpression.Call { VirtualDispatch: true }) DispatchKinds.Add(value.Type.Kind);
                int count = value.Expression switch
                {
                    IrExpression.Call call => CallWidth(call.Target, call.Arguments.Length, call.VirtualDispatch),
                    IrExpression.NewObject creation => creation.Arguments.Length + 1,
                    IrExpression.CallDelegate call => call.Arguments.Length + 1,
                    _ => 0,
                };
                arguments = System.Math.Max(arguments, count);
                if (value.Expression is IrExpression.BoxScalars boxed) BoxArgumentCount = System.Math.Max(BoxArgumentCount, boxed.Values.Length);
            }
            foreach (var call in block.Statements.OfType<IrStatement.InvokeDelegate>())
                arguments = System.Math.Max(arguments, call.Arguments.Length + 1);
            foreach (var call in block.Statements.OfType<IrStatement.Invoke>())
            {
                arguments = System.Math.Max(arguments, CallWidth(call.Target, call.Arguments.Length, call.VirtualDispatch));
                if (call.VirtualDispatch) DispatchKinds.Add(IrTypeKind.Void);
            }
        }
        if (frameIntegers != FrameIntegerCount || frameWideIntegers != FrameWideIntegerCount)
            throw new System.InvalidOperationException("Address-taken scalar frame slots were not assigned exactly once.");
        IntegerCount = integers; WideIntegerCount = wideIntegers; CallArgumentCount = arguments; LocationCount = locations;
        BorrowsFrame = method.Blocks.SelectMany(b => b.Values).Any(v => v.Expression is IrExpression.AddressLocal or IrExpression.AddressParameter or IrExpression.AddressStatic);
    }

    internal string Storage(ObjectRootKind kind, int index, int block = -1) => _storage[(kind, block, index)];
    internal string Owner(ObjectRootKind kind, int index, int block = -1) => _owners[(kind, block, index)];
    internal int Index(ObjectRootKind kind, int index, int block = -1) => _indices[(kind, block, index)];
    private static string Number(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
