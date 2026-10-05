using System;
using System.Collections.Generic;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>Connects detached interface assertions to actual executable signatures and body closure.</summary>
internal sealed class ObjectInterfaceOperationRules
{
    private readonly Dictionary<IrMethodId, ObjectInterfaceDeclaration> _declarations = [];
    private readonly Dictionary<IrMethodId, ObjectInterfaceDispatch> _maps = [];
    private readonly Dictionary<IrMethodId, ObjectDispatchMethod> _facts = [];
    internal int TargetCount { get; }

    internal ObjectInterfaceOperationRules(ObjectIrProgram program, IReadOnlyDictionary<IrMethodId, ObjectMethodBinding> bindings,
        IReadOnlyDictionary<IrMethodId, IrMethod> methods)
    {
        if (program.InterfaceDispatch is not { } plan) return;
        ObjectInterfacePlanValidator.Validate(plan);
        Require(CatalogMatches(program.Flow.Types, plan.Types), "interface plan and executable catalog disagree.");
        foreach (var declaration in plan.Declarations)
        {
            var id = IrMethodId.From(declaration.Source);
            Require(!methods.ContainsKey(id), "bodyless interface declarations cannot masquerade as executable methods.");
            _declarations.Add(id, declaration);
        }
        foreach (var fact in plan.Methods)
        {
            _facts.Add(fact.Binding.Method, fact);
            if (methods.TryGetValue(fact.Binding.Method, out var body))
                Require(bindings[fact.Binding.Method] == fact.Binding && body.ReturnType == fact.ReturnType
                    && body.Parameters.SequenceEqual(fact.Parameters), "executable body contradicts its interface method facts.");
        }
        Require(methods.Keys.Concat(_declarations.Keys).Concat(_facts.Keys).Distinct().Count() <= ProgramLimits.MaximumMethods,
            "executable bodies, declarations and mapping facts share the 256-method limit.");
        foreach (var map in plan.Dispatch)
        {
            _maps.Add(map.Declaration, map); TargetCount += map.Targets.Length;
            foreach (var target in map.Targets)
                Require(methods.ContainsKey(target.Implementation), "interface map implementation lacks a translated executable body.");
        }
    }

    internal bool Has(IrMethodId id) => _declarations.ContainsKey(id);
    internal ObjectInterfaceDeclaration Declaration(IrMethodId id) => _declarations[id];
    internal IEnumerable<IrMethodId> Targets(IrMethodId id) => _maps[id].Targets.Select(t => t.Implementation);
    internal bool Mapped(ObjectMethodBinding binding) => _facts.TryGetValue(binding.Method, out var fact) && fact.Binding == binding;
    internal IEnumerable<ObjectTypeId> SignatureTypes(IrMethodId id) => _declarations[id].Parameters.Append(_declarations[id].ReturnType)
        .Where(t => t.Class is not null).Select(t => t.Class!);

    internal void ValidateUsage(ObjectIrProgram program)
    {
        var used = new HashSet<IrMethodId>();
        foreach (var statement in program.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Statements))
            switch (statement)
            {
                case IrStatement.Define { Value.Expression: IrExpression.Call { VirtualDispatch: true } call } when Has(call.Target): used.Add(call.Target); break;
                case IrStatement.Invoke { VirtualDispatch: true } call when Has(call.Target): used.Add(call.Target); break;
            }
        Require(used.SetEquals(_maps.Keys), "interface maps must exactly match reachable interface call declarations.");
    }

    private static bool CatalogMatches(ObjectTypeCatalog left, ObjectTypeCatalog right)
    {
        if (left.Integer64 != right.Integer64 || !left.Delegates.SequenceEqual(right.Delegates)
            || left.String != right.String || left.ObjectBase != right.ObjectBase || !left.Roots.ToHashSet().SetEquals(right.Roots)
            || left.Types.Length != right.Types.Length || left.Interfaces.Length != right.Interfaces.Length
            || !left.Arrays.ToHashSet().SetEquals(right.Arrays)) return false;
        var classes = right.Types.ToDictionary(t => ObjectTypeId.From(t.Source));
        var interfaces = right.Interfaces.ToDictionary(t => ObjectTypeId.From(t.Source));
        return left.Types.All(t => classes.TryGetValue(ObjectTypeId.From(t.Source), out var other) && t.Attributes == other.Attributes
                && t.BaseType == other.BaseType && t.Fields.SequenceEqual(other.Fields) && t.Interfaces.SequenceEqual(other.Interfaces))
            && left.Interfaces.All(t => interfaces.TryGetValue(ObjectTypeId.From(t.Source), out var other)
                && t.Attributes == other.Attributes && t.ExternallyVisible == other.ExternallyVisible && t.Parents.SequenceEqual(other.Parents)
                && t.Arguments.SequenceEqual(other.Arguments));
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new ArgumentException("Invalid interface operation IR: " + message);
    }
}
