using System.Collections.Generic;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>Distinct typed slots for phase A execution storage; target C++ supplies byte size/alignment.</summary>
internal sealed class ObjectFrameLayout
{
    private readonly Dictionary<(ObjectRootKind Kind, int Block, int Index), string> _storage = [];
    internal int IntegerCount { get; }
    internal int RootCount { get; }
    internal int CallArgumentCount { get; }

    internal ObjectFrameLayout(IrMethod method, ObjectMethodRoots roots)
    {
        RootCount = roots.Slots.Length;
        foreach (var root in roots.Slots)
            _storage.Add((root.Kind, root.Block, root.Index), "f->roots[" + Number(root.Slot) + "]");
        int integers = 0, arguments = 0;
        void Add(ObjectRootKind kind, int index, IrType type, int block = -1)
        {
            if (type.Kind == IrTypeKind.Int32)
                _storage.Add((kind, block, index), "f->integers[" + Number(integers++) + "]");
        }
        for (int i = 0; i < method.Parameters.Length; i++) Add(ObjectRootKind.Parameter, i, method.Parameters[i]);
        for (int i = 0; i < method.Locals.Length; i++) Add(ObjectRootKind.Local, i, method.Locals[i]);
        foreach (var block in method.Blocks)
        {
            for (int i = 0; i < block.Inputs.Length; i++) Add(ObjectRootKind.BlockInput, i, block.Inputs[i], block.Id);
            foreach (var value in block.Values)
            {
                Add(ObjectRootKind.Value, value.Id, value.Type, block.Id);
                int count = value.Expression switch
                {
                    IrExpression.Call call => call.Arguments.Length,
                    IrExpression.NewObject creation => creation.Arguments.Length + 1,
                    _ => 0,
                };
                arguments = System.Math.Max(arguments, count);
            }
            foreach (var call in block.Statements.OfType<IrStatement.Invoke>())
                arguments = System.Math.Max(arguments, call.Arguments.Length);
        }
        IntegerCount = integers; CallArgumentCount = arguments;
    }

    internal string Storage(ObjectRootKind kind, int index, int block = -1) => _storage[(kind, block, index)];
    private static string Number(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
