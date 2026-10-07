using System;
using System.Linq;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;
internal static class EnumQueryMetadata
{
    internal static string Name(TypeSource source) => source.RuntimeName ?? source.Name.Replace('/', '+');
    internal static void Validate(ValueTypeCatalog values)
    {
        int units = 0;
        foreach (var type in values.Types.Where(t => t.ScalarBox is not null))
        {
            string name = Name(type.Source);
            if (name.Length > ObjectStringLimits.MaximumLiteralCodeUnits
                || (units += name.Length + (type.ScalarEnum ? name.Length + 2 : 0)) > ObjectStringLimits.MaximumTotalLiteralCodeUnits)
                throw new ArgumentException("Enum query type-name text exceeds the bounded literal budget.");
        }
    }
}
