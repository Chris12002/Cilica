using System;
using System.Collections.Generic;
using System.Linq;

namespace Cilica.Compiler;

// Invariant messages for the explicitly selected bounded runtime faults. Metadata
// construction still authenticates the actual framework identities separately.
internal static class ManagedExceptionMessages
{
    internal static Dictionary<(ObjectTypeId Type, int Kind), string> Build(ManagedExceptionTypes exceptions, ObjectTypeCatalog catalog)
    {
        var classes = catalog.Types.ToDictionary(t => ObjectTypeId.From(t.Source));
        var statuses = exceptions.RuntimeFaults.ToDictionary(b => b.Type, b => b.Status);
        var resources = exceptions.Resources.ToDictionary(r => r.Type, r => r.Kind);
        var result = new Dictionary<(ObjectTypeId, int), string>(); var unique = new HashSet<string>(StringComparer.Ordinal);
        int units = 0;
        foreach (var type in exceptions.Types)
        {
            string name = classes[type].Source.Name;
            if (name.Length > 1024) throw new ArgumentException("Exception display name exceeds 1024 UTF-16 code units.");
            string generic = "Exception of type '" + name + "' was thrown.";
            uint status = 0;
            for (ObjectTypeId? current = type; current is not null && current != catalog.ObjectBase; current = classes[current].BaseType)
                if (statuses.TryGetValue(current, out status)) break;
            string ordinary = status switch
            {
                1 => "Attempted to divide by zero.", 2 => "Arithmetic operation resulted in an overflow.",
                4 => "Object reference not set to an instance of an object.",
                5 => "Insufficient memory to continue the execution of the program.",
                7 => "Specified cast is not valid.", 10 => "Index was outside the bounds of the array.",
                11 => "Attempted to access an element as a type incompatible with the array.",
                12 => "Value does not fall within the expected range.", _ => generic,
            };
            if (resources.TryGetValue(type, out var resource)) ordinary = resource switch {
                ExceptionResourceKind.ArgumentNull => "Value cannot be null.",
                ExceptionResourceKind.ArgumentOutOfRange => "Specified argument was out of the range of valid values.",
                ExceptionResourceKind.InvalidOperation => "Operation is not valid due to the current state of the object.",
                ExceptionResourceKind.NotSupported => "Specified method is not supported.",
                ExceptionResourceKind.Format => "One of the identified items was in an invalid format.",
                ExceptionResourceKind.System => "System error.",
                ExceptionResourceKind.Arithmetic => "Overflow or underflow in the arithmetic operation.",
                ExceptionResourceKind.KeyNotFound => "The given key was not present in the dictionary.",
                ExceptionResourceKind.ObjectDisposed => "Cannot access a disposed object.",
                _ => throw new ArgumentException("Unknown exception resource.") };
            for (int kind = 0; kind < 3; ++kind)
            {
                bool genericNull = kind == 0 && (status == 0 && !resources.ContainsKey(type)
                    || resources.TryGetValue(type, out var messageResource) && messageResource == ExceptionResourceKind.System);
                string text = genericNull ? generic : kind == 2 && status == 12 && !resources.ContainsKey(type) ? "Delegate to an instance method cannot have null 'this'." : ordinary;
                if (unique.Add(text) && (units += text.Length) > ObjectStringLimits.MaximumTotalLiteralCodeUnits)
                    throw new ArgumentException("Exception message resource budget exceeded.");
                result.Add((type, kind), text);
            }
        }
        return result;
    }
}
