using System.Collections.Generic;
namespace Cilica.Compiler;
internal sealed partial class ObjectIrRules
{
    // Populated only after each method's unique direct initialization is validated.
    private readonly Dictionary<IrMethodId, IrMethodId> _constructorChains = [];
    internal void ConstructorChains()
    {
        var complete = new HashSet<IrMethodId>();
        foreach (var start in _constructorChains.Keys)
        {
            if (complete.Contains(start)) continue;
            var visiting = new HashSet<IrMethodId>();
            var current = start;
            while (_constructorChains.TryGetValue(current, out var next) && !complete.Contains(current))
            {
                Require(visiting.Add(current), "same-owner constructor initialization chains must be acyclic and reach an immediate base constructor.");
                current = next;
            }
            complete.UnionWith(visiting);
        }
    }
}
