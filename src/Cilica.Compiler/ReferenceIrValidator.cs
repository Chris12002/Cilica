using System;
using System.Collections.Generic;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>Validates detached typed value flow. It does not authenticate CIL declarations or lower objects.</summary>
public static class ReferenceIrValidator
{
    public static void Validate(ReferenceIrProgram program) => Validate(program, null);

    internal static void Validate(ReferenceIrProgram program, ObjectIrRules? objectRules)
    {
        ArgumentNullException.ThrowIfNull(program);
        ObjectModelValidator.Validate(program.Types);
        IrValidator.Validate(new IrProgram(program.Entry, program.Methods), program.Types, objectRules);

        // A catalog root cannot smuggle unrelated layouts into the program's resource/ownership contract.
        var layouts = program.Types.Types.ToDictionary(t => ObjectTypeId.From(t.Source));
        var interfaces = program.Types.Interfaces.ToDictionary(t => ObjectTypeId.From(t.Source));
        var pending = new Stack<ObjectTypeId>(); pending.Push(program.Types.ObjectBase);
        foreach (var method in program.Methods)
        {
            if (objectRules is not null)
                foreach (var type in objectRules.OperationTypes(method)) pending.Push(type);
            foreach (var type in method.Parameters.Concat(method.Locals).Append(method.ReturnType)
                .Concat(method.Blocks.SelectMany(b => b.Inputs))
                .Concat(method.Blocks.SelectMany(b => b.Values).Select(v => v.Type)))
                if (type.Class is not null) pending.Push(type.Class);
        }
        var visited = new HashSet<ObjectTypeId>();
        while (pending.TryPop(out ObjectTypeId? id))
            if (visited.Add(id))
            {
                if (interfaces.TryGetValue(id, out var contract))
                    foreach (var parent in contract.Parents) pending.Push(parent);
                else
                {
                    if (layouts[id].BaseType is { } parent) pending.Push(parent);
                    foreach (var edge in layouts[id].Interfaces) pending.Push(edge);
                    foreach (var reference in layouts[id].Fields.Select(f => f.Storage).OfType<ObjectStorage.Reference>())
                        pending.Push(reference.Type);
                }
            }
        if (visited.Count != layouts.Count + interfaces.Count)
            throw new ArgumentException("Invalid reference program IR: catalog contains types unused by value flow or its field closure.", nameof(program));
    }
}
