using System;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>Validates detached typed value flow. It does not authenticate CIL declarations or lower objects.</summary>
public static class ReferenceIrValidator
{
    public static void Validate(ReferenceIrProgram program) => Validate(program, null);

    internal static void Validate(ReferenceIrProgram program, ObjectIrRules? objectRules, ManagedLocationRules? locations = null, ScalarBoxingRules? boxes = null, ManagedExceptionRules? exceptions = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        ObjectModelValidator.Validate(program.Types);
        IrValidator.Validate(new IrProgram(program.Entry, program.Methods), program.Types, objectRules, locationRules: locations, boxedRules: boxes, exceptions: exceptions);

        var roots = program.Methods.SelectMany(method => method.Parameters.Concat(method.Locals).Append(method.ReturnType)
            .Concat(method.Blocks.SelectMany(b => b.Inputs)).Concat(method.Blocks.SelectMany(b => b.Values).Select(v => v.Type)))
            .Select(type => type.Kind == IrTypeKind.ManagedLocation ? type.Element! : type)
            .Where(type => type.Class is not null).Select(type => type.Class!);
        if (objectRules is not null) roots = roots.Concat(program.Methods.SelectMany(objectRules.OperationTypes));
        if (exceptions is not null) roots = roots.Concat(exceptions.Types.Types);
        if (boxes is not null) roots = roots.Concat(boxes.InterfaceTypes);
        ObjectCatalogUsage.Validate(program.Types, roots);
    }
}
