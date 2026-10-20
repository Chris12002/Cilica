using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Cilica.Compiler;

/// <summary>Exact descriptor guards establish the actual C++ type before member access.</summary>
internal static class ObjectCppFieldAccess
{
    internal static Dictionary<ObjectFieldId, ObjectTypeId[]> Analyze(ObjectNativeLayout native, Func<ObjectTypeId, bool> ordinary)
    {
        // Native composition is already bounded and validated. Each inherited field
        // uses its member in the actual derived structure, never a cast to its base.
        return native.Fields.Where(p => ordinary(p.Key))
            .SelectMany(p => p.Value.Select(f => (Field: f.Id, Type: p.Key)))
            .GroupBy(p => p.Field).Where(g => g.Count() <= 4)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Type).ToArray());
    }

    private static string Symbol(ObjectFieldId field, Func<ObjectTypeId, string> type, Func<ObjectFieldId, string> member) =>
        "proven_field_" + type(field.Owner) + "_" + member(field);

    internal static string Address(ObjectFieldId field, string receiver, ObjectNativeLayout native,
        Dictionary<ObjectFieldId, ObjectTypeId[]> plan, Func<ObjectTypeId, string> type, Func<ObjectFieldId, string> member) =>
        plan.ContainsKey(field) ? Symbol(field, type, member) + "(" + receiver + ")"
        : "field_address(" + receiver + ", " + native.Indices[field].ToString(CultureInfo.InvariantCulture) + ")";

    internal static void Append(StringBuilder text, ObjectNativeLayout native,
        Dictionary<ObjectFieldId, ObjectTypeId[]> plan, Func<ObjectTypeId, string> type, Func<ObjectFieldId, string> member)
    {
        foreach (var pair in plan)
        {
            text.Append("#if defined(__OPTIMIZE__)\n[[maybe_unused, gnu::always_inline]] static void* ")
                .Append(Symbol(pair.Key, type, member)).Append("(Object* value) noexcept {\n");
            foreach (var actual in pair.Value)
            {
                string field = actual == pair.Key.Owner ? member(pair.Key) : type(pair.Key.Owner) + "_" + member(pair.Key);
                text.Append("    if (value->type == &").Append(type(actual)).Append("_descriptor) return &reinterpret_cast<")
                    .Append(type(actual)).Append("*>(value)->").Append(field).Append(";\n");
            }
            text.Append("    return field_address(value, ").Append(native.Indices[pair.Key].ToString(CultureInfo.InvariantCulture)).Append(");\n}\n#else\n")
                .Append("[[maybe_unused, gnu::noinline]] static void* ").Append(Symbol(pair.Key, type, member))
                .Append("(Object* value) noexcept { return field_address(value, ").Append(native.Indices[pair.Key].ToString(CultureInfo.InvariantCulture))
                .Append("); }\n#endif\n");
        }
    }
}
