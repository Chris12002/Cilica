using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Cilica.Compiler;

public enum IrExceptionClause { Catch, Filter, Finally, Fault }

/// <summary>Half-open block ranges; metadata order breaks ties between clauses sharing one try.</summary>
public sealed record IrExceptionRegion(int TryStart, int TryEnd, int HandlerStart, int HandlerEnd,
    IrExceptionClause Kind, ObjectTypeId? CatchType = null, int? FilterStart = null);
public sealed record RuntimeExceptionBinding(uint Status, ObjectTypeId Type);
public enum ExceptionResourceKind { ArgumentNull, ArgumentOutOfRange, InvalidOperation, NotSupported, Format, System, Arithmetic }
public sealed record ExceptionResourceBinding(ObjectTypeId Type, ExceptionResourceKind Kind);

/// <summary>Intrinsic fieldless exception shells in the ordinary nominal class graph.</summary>
public sealed record ManagedExceptionTypes(ObjectTypeId Base, ImmutableArray<ObjectTypeId> Types,
    ImmutableArray<RuntimeExceptionBinding> RuntimeFaults)
{
    public ObjectTypeId? ArgumentBase { get; init; }
    public ObjectTypeId? TypeInitialization { get; init; }
    public ImmutableArray<ExceptionResourceBinding> Resources { get; init; } = [];
}

public sealed record ManagedExceptionIrProgram(DelegateValueIrProgram Flow, ManagedExceptionTypes Exceptions)
{
    public const string Profile = "managed-exceptions-ir-v3";
}

public static class ManagedExceptionIrValidator
{
    public static void Validate(ManagedExceptionIrProgram program) => _ = ValidateAndPlan(program);
    internal static ValueIrRules ValidateAndPlan(ManagedExceptionIrProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(program.Flow);
        ArgumentNullException.ThrowIfNull(program.Flow.Flow);
        ArgumentNullException.ThrowIfNull(program.Flow.Flow.Flow);
        var flow = program.Flow.Flow.Flow;
        _ = ValueLayoutPlanner.Build(flow.Types);
        var rules = new ManagedExceptionRules(program.Exceptions, flow.Types.References);
        if (!program.Flow.Bindings.IsDefault && program.Flow.Bindings.Any(b => b is not null && program.Exceptions.Types.Contains(b.Owner) && (b.Kind != ObjectMethodKind.Static || b.TypeInitializerName is not null)))
            throw new ArgumentException("Intrinsic exception shells cannot declare ordinary constructors or instance method bodies.", nameof(program));
        var validated = ValueArrayIrValidator.ValidateComposition(program.Flow.Flow, program.Flow.Bindings,
            classes: true, boxing: true, delegates: true, exceptions: rules, interfaces: program.Flow.InterfaceDispatch, dispatch: program.Flow.Dispatch);
        _ = ManagedExceptionCastMessages.Build(flow.Methods, flow.Types.References, flow.Types.Types);
        return validated;
    }
}

internal sealed class ManagedExceptionRules
{
    internal ManagedExceptionTypes Types { get; }
    private readonly ObjectTypeCatalog _catalog;
    private readonly HashSet<ObjectTypeId> _types;
    private readonly ObjectTypeRelations _relations;
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new ArgumentException("Invalid managed exception IR: " + message);
    }
    internal ManagedExceptionRules(ManagedExceptionTypes types, ObjectTypeCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(types);
        Types = types; _catalog = catalog; _relations = new(catalog);
        Require(!types.Types.IsDefaultOrEmpty && types.Types.Length <= ObjectModelLimits.MaximumTypes
            && types.Types.All(t => t is not null), "bounded exception type identities are required.");
        _types = types.Types.ToHashSet();
        Require(_types.Count == types.Types.Length && types.Base is not null && _types.Contains(types.Base), "exception base/identities are missing or duplicated.");
        var classes = catalog.Types.ToDictionary(t => ObjectTypeId.From(t.Source));
        Require(classes.TryGetValue(types.Base!, out var basis) && basis.BaseType == catalog.ObjectBase,
            "the intrinsic exception base must be a direct child of Object.");
        foreach (var type in _types)
            Require(classes.TryGetValue(type, out var declaration) && declaration.Fields.IsEmpty && declaration.Interfaces.IsEmpty
                && _relations.Ancestors[type].Contains(types.Base!), "exception shells must be fieldless nominal descendants without interfaces.");
        foreach (var type in classes.Keys)
            Require(!_relations.Ancestors[type].Contains(types.Base!) || _types.Contains(type), "all exception descendants must have intrinsic shell declarations.");
        uint[] statuses = [1, 2, 4, 5, 7, 10, 11, 12];
        Require(!types.RuntimeFaults.IsDefault && types.RuntimeFaults.Length == statuses.Length
            && types.RuntimeFaults.All(b => b is not null && _types.Contains(b.Type))
            && types.RuntimeFaults.Select(b => b.Status).Order().SequenceEqual(statuses)
            && types.RuntimeFaults.Select(b => b.Type).Distinct().Count() == statuses.Length,
            "runtime faults require distinct exact exception identities for statuses 1, 2, 4, 5, 7, 10, 11 and 12.");
        Require(!types.Resources.IsDefault && types.Resources.Length <= 7
            && types.Resources.All(r => r is not null && _types.Contains(r.Type) && Enum.IsDefined(r.Kind))
            && types.Resources.Select(r => r.Type).Distinct().Count() == types.Resources.Length
            && types.Resources.Select(r => r.Kind).Distinct().Count() == types.Resources.Length
            && types.Resources.All(r => r.Type != types.Base && !types.RuntimeFaults.Any(b => b.Type == r.Type)),
            "additional message resources require distinct admitted exception types and resource kinds.");
        Require(types.ArgumentBase is null || types.RuntimeFaults.Single(b => b.Status == 12).Type == types.ArgumentBase,
            "argument exception base must match its runtime identity.");
        foreach (var resource in types.Resources.Where(r => r.Kind is ExceptionResourceKind.ArgumentNull or ExceptionResourceKind.ArgumentOutOfRange))
            Require(types.ArgumentBase is not null && resource.Type != types.ArgumentBase
                && classes[resource.Type].BaseType == types.ArgumentBase, "argument resources require direct descendants of the selected argument base.");
        foreach (var resource in types.Resources.Where(r => r.Kind is ExceptionResourceKind.System or ExceptionResourceKind.Arithmetic or ExceptionResourceKind.Format))
        {
            var parent = resource.Kind == ExceptionResourceKind.System ? types.Base
                : types.Resources.SingleOrDefault(r => r.Kind == ExceptionResourceKind.System)?.Type;
            Require(parent is not null && classes[resource.Type].BaseType == parent
                && resource.Type.ContentHash == catalog.ObjectBase.ContentHash
                && classes[resource.Type].Source.Name == "System." + resource.Kind + "Exception",
                "framework message resources require exact selected names, images and ancestry.");
        }
        if (types.TypeInitialization is { } initialization)
            Require(catalog.Integer64 && _types.Contains(initialization) && initialization.ContentHash == catalog.ObjectBase.ContentHash
                && classes[initialization].Source.Name == "System.TypeInitializationException"
                && classes[initialization].BaseType == types.Resources.SingleOrDefault(r => r.Kind == ExceptionResourceKind.System)?.Type,
                "type initialization requires the exact selected framework identity and ancestry.");
        _ = ManagedExceptionMessages.Build(types, catalog);
    }
    internal bool Throwable(IrType type) => type == IrType.NullReference || type.Kind == IrTypeKind.Reference && _types.Contains(type.Class!);
    private bool StringOperand(int? operand, IReadOnlyList<IrType> values) => operand is null
        || operand is int index && index >= 0 && index < values.Count
            && (_catalog.String is not null && values[index] == IrType.Reference(_catalog.String) || values[index] == IrType.NullReference);
    internal bool Creation(IrExpression.NewException creation, IrType result, IReadOnlyList<IrType> values) =>
        creation.Type is not null && _types.Contains(creation.Type) && result == IrType.Reference(creation.Type)
        && StringOperand(creation.Message, values) && StringOperand(creation.ParameterName, values)
        && (creation.Inner is null || creation.Type == Types.TypeInitialization && creation.Inner is int inner && inner >= 0 && inner < values.Count && Throwable(values[inner]))
        && (creation.Type == Types.TypeInitialization ? creation.TypeName is not null && _catalog.String is not null
            && creation.Message is null && creation.ParameterName is null && StringOperand(creation.TypeName, values) : creation.TypeName is null)
        && (creation.ParameterName is null || Types.ArgumentBase is not null && _relations.Ancestors[creation.Type].Contains(Types.ArgumentBase));
    internal bool InnerException(IrExpression.ExceptionInnerException read, IrType result, IReadOnlyList<IrType> values) =>
        _catalog.Integer64 && result == IrType.Reference(Types.Base) && read.Operand >= 0 && read.Operand < values.Count && Throwable(values[read.Operand]);
    internal bool TypeName(IrExpression.ExceptionTypeName read, IrType result, IReadOnlyList<IrType> values) =>
        Types.TypeInitialization is not null && _catalog.String is not null && result == IrType.Reference(_catalog.String)
        && read.Operand >= 0 && read.Operand < values.Count && (values[read.Operand] == IrType.NullReference || values[read.Operand] == IrType.Reference(Types.TypeInitialization));
    internal bool Message(IrExpression.ExceptionMessage message, IrType result, IReadOnlyList<IrType> values) =>
        _catalog.String is not null && result == IrType.Reference(_catalog.String)
        && message.Operand >= 0 && message.Operand < values.Count && Throwable(values[message.Operand]);
    internal bool ParameterName(IrExpression.ExceptionParameterName parameter, IrType result, IReadOnlyList<IrType> values) =>
        Types.ArgumentBase is not null && _catalog.String is not null && result == IrType.Reference(_catalog.String)
        && parameter.Operand >= 0 && parameter.Operand < values.Count
        && (values[parameter.Operand] == IrType.NullReference || values[parameter.Operand].Kind == IrTypeKind.Reference
            && _types.Contains(values[parameter.Operand].Class!) && _relations.Ancestors[values[parameter.Operand].Class!].Contains(Types.ArgumentBase));

    private enum Part { Try, Handler, Filter }
    private sealed record Scope(int Region, Part Part, int Start, int End);
    private static IEnumerable<Scope> Scopes(IrMethod method)
    {
        for (int i = 0; i < method.ExceptionRegions.Length; ++i)
        {
            var region = method.ExceptionRegions[i];
            yield return new(i, Part.Try, region.TryStart, region.TryEnd);
            yield return new(i, Part.Handler, region.HandlerStart, region.HandlerEnd);
            if (region.FilterStart is int filter) yield return new(i, Part.Filter, filter, region.HandlerStart);
        }
    }
    private static bool Contains(Scope scope, int block) => block >= scope.Start && block < scope.End;
    private static int HandlerAt(IrMethod method, int block, params IrExceptionClause[] kinds) =>
        Scopes(method).Where(s => s.Part != Part.Try && Contains(s, block)).OrderBy(s => s.End - s.Start)
            .Select(s => s.Part == Part.Handler && kinds.Contains(method.ExceptionRegions[s.Region].Kind) ? s.Region : -1).DefaultIfEmpty(-1).First();
    internal int CatchAt(IrMethod method, int block) => HandlerAt(method, block, IrExceptionClause.Catch, IrExceptionClause.Filter);
    internal int CleanupAt(IrMethod method, int block) => HandlerAt(method, block, IrExceptionClause.Finally, IrExceptionClause.Fault);
    internal int FilterAt(IrMethod method, int block) => Scopes(method).Where(s => s.Part == Part.Filter && Contains(s, block)).Select(s => s.Region).DefaultIfEmpty(-1).First();

    internal void ValidateRegions(IrMethod method)
    {
        Require(method.ExceptionRegions.Length <= 256, "method has more than 256 clauses.");
        bool Range(int start, int end) => start >= 0 && start < end && end <= method.Blocks.Length;
        foreach (var region in method.ExceptionRegions)
        {
            Require(region is not null && Range(region.TryStart, region.TryEnd) && Range(region.HandlerStart, region.HandlerEnd), "region block ranges are invalid.");
            Require(region!.Kind is IrExceptionClause.Catch or IrExceptionClause.Filter or IrExceptionClause.Finally or IrExceptionClause.Fault, "unknown clause kind.");
            Require(region.Kind == IrExceptionClause.Catch ? region.CatchType is not null && _types.Contains(region.CatchType) : region.CatchType is null,
                "catch needs an exact exception type; other clauses cannot carry one.");
            Require(region.Kind == IrExceptionClause.Filter ? region.FilterStart is int start && Range(start, region.HandlerStart) : region.FilterStart is null,
                "filter needs a nonempty range before its handler; other clauses cannot carry one.");
            var expected = region.Kind is IrExceptionClause.Catch or IrExceptionClause.Filter
                ? new[] { IrType.Reference(region.CatchType ?? Types.Base) } : [];
            Require(method.Blocks[region.HandlerStart].Inputs.SequenceEqual(expected), "handler entry stack does not match its clause.");
            if (region.FilterStart is int filter)
                Require(method.Blocks[filter].Inputs.SequenceEqual(new[] { IrType.Reference(Types.Base) }), "filter entry requires the exception stack value.");
        }
        var scopes = Scopes(method).ToArray();
        for (int i = 0; i < scopes.Length; ++i)
        for (int j = i + 1; j < scopes.Length; ++j)
        {
            var a = scopes[i]; var b = scopes[j];
            bool disjoint = a.End <= b.Start || b.End <= a.Start;
            bool same = a.Start == b.Start && a.End == b.End;
            bool sharedTry = same && a.Part == Part.Try && b.Part == Part.Try;
            bool nested = a.Start <= b.Start && a.End >= b.End || b.Start <= a.Start && b.End >= a.End;
            Require(disjoint || a.Region != b.Region && nested && (!same || sharedTry), "protected scopes overlap illegally.");
            if (sharedTry)
                Require(method.ExceptionRegions[a.Region].Kind is IrExceptionClause.Catch or IrExceptionClause.Filter
                    && method.ExceptionRegions[b.Region].Kind is IrExceptionClause.Catch or IrExceptionClause.Filter,
                    "cleanup and catch clauses need distinct nested try regions.");
            if (!disjoint && a.Region != b.Region)
                Require(!(a.Part == Part.Filter && a.Start <= b.Start && a.End >= b.End)
                    && !(b.Part == Part.Filter && b.Start <= a.Start && b.End >= a.End), "nested protected regions inside filters are unsupported.");
        }
        for (int index = 0; index < method.ExceptionRegions.Length; ++index)
        {
            var own = scopes.Where(s => s.Region == index).ToArray();
            foreach (var enclosing in scopes.Where(s => s.Region != index))
            {
                bool shared = enclosing.Part == Part.Try && own.Any(s => s.Part == Part.Try && s.Start == enclosing.Start && s.End == enclosing.End);
                bool Inside(Scope scope) => scope.Start >= enclosing.Start && scope.End <= enclosing.End;
                if (!shared && own.Any(Inside)) Require(own.All(Inside), "a nested clause's try, filter and handler must remain in one enclosing scope.");
            }
        }
    }

    internal void ValidateFlow(IrMethod method)
    {
        var scopes = Scopes(method).ToArray();
        foreach (var block in method.Blocks)
        {
            var active = scopes.Where(s => Contains(s, block.Id)).ToArray();
            switch (block.Terminator)
            {
                case IrReturn or IrVoidReturn: Require(active.Length == 0, "return cannot bypass protected cleanup; use leave."); break;
                case IrJump jump: Edge(jump.Edge, false); break;
                case IrBranch branch: Edge(branch.Taken, false); Edge(branch.NotTaken, false); break;
                case IrLeave leave:
                    Require(!active.Any(s => !Contains(s, leave.Edge.Target) && (s.Part == Part.Filter || s.Part == Part.Handler && method.ExceptionRegions[s.Region].Kind is IrExceptionClause.Finally or IrExceptionClause.Fault)),
                        "leave cannot exit a filter or cleanup handler.");
                    Edge(leave.Edge, true); break;
                case IrEndFinally:
                    Require(active.OrderBy(s => s.End - s.Start).FirstOrDefault() is { Part: Part.Handler } closest
                        && method.ExceptionRegions[closest.Region].Kind is IrExceptionClause.Finally or IrExceptionClause.Fault,
                        "endfinally cannot bypass an inner protected region."); break;
            }
            void Edge(IrEdge edge, bool leave)
            {
                var target = scopes.Where(s => Contains(s, edge.Target)).ToArray();
                Require(leave || active.All(target.Contains), "ordinary control transfer cannot exit a protected scope.");
                Require(target.All(s => active.Contains(s) || !leave && s.Part == Part.Try && edge.Target == s.Start && edge.Arguments.IsEmpty),
                    "control transfer cannot enter a handler/filter or the middle of a try.");
            }
        }
        foreach (var region in method.ExceptionRegions.Where(r => r.Kind == IrExceptionClause.Filter)) ValidateFilter(method, region);
    }

    private static void ValidateFilter(IrMethod method, IrExceptionRegion region)
    {
        int first = region.FilterStart!.Value, end = region.HandlerStart;
        var writes = method.Blocks[first..end].SelectMany(b => b.Statements).OfType<IrStatement.StoreLocal>().Select(s => s.Local).ToHashSet();
        foreach (var block in method.Blocks)
        foreach (var statement in block.Statements)
        {
            int local = statement switch
            {
                IrStatement.StoreLocal s => s.Local,
                IrStatement.Define { Value.Expression: IrExpression.Local l } => l.Index,
                IrStatement.Define { Value.Expression: IrExpression.AddressLocal l } => l.Index,
                _ => -1,
            };
            if (writes.Contains(local)) Require(block.Id >= first && block.Id < region.HandlerEnd
                && statement is not IrStatement.Define { Value.Expression: IrExpression.AddressLocal }, "filter locals cannot escape their filter/handler.");
        }
        var indegree = new int[end - first]; var assigned = new HashSet<int>?[end - first];
        IEnumerable<int> Targets(IrBlock block) => block.Terminator switch
        {
            IrJump jump => [jump.Edge.Target], IrBranch branch => [branch.Taken.Target, branch.NotTaken.Target], IrEndFilter => [],
            _ => throw new ArgumentException("Invalid managed exception IR: filters require pure branches and endfilter."),
        };
        foreach (var block in method.Blocks[first..end])
        {
            foreach (var statement in block.Statements)
                Require(statement is IrStatement.StoreLocal or IrStatement.Define { Value.Expression: IrExpression.Constant or IrExpression.Null
                    or IrExpression.StackInput or IrExpression.Local or IrExpression.Compare or IrExpression.IsInstance or IrExpression.Unary
                    or IrExpression.Binary { Operation: Int32Operation.BitwiseAnd or Int32Operation.BitwiseOr or Int32Operation.BitwiseXor } },
                    "filters may only test the exception type, combine results and use private locals.");
            foreach (int target in Targets(block)) { Require(target >= first && target < end, "filter edge leaves its range."); indegree[target - first]++; }
        }
        var pending = new Queue<int>(); int work = 0, removed = 0;
        for (int i = first; i < end; ++i) if (indegree[i - first] == 0) pending.Enqueue(i);
        while (pending.TryDequeue(out int block))
        {
            removed++; var state = assigned[block - first] ?? [];
            foreach (var statement in method.Blocks[block].Statements)
            {
                if (statement is IrStatement.StoreLocal store) state.Add(store.Local);
                if (statement is IrStatement.Define { Value.Expression: IrExpression.Local local }) Require(state.Contains(local.Index), "filter local must be assigned on every incoming path.");
            }
            foreach (int target in Targets(method.Blocks[block]))
            {
                Require((work += state.Count + 1) <= 65536, "filter assignment-analysis budget exceeded.");
                if (assigned[target - first] is { } known) known.IntersectWith(state); else assigned[target - first] = new(state);
                if (--indegree[target - first] == 0) pending.Enqueue(target);
            }
        }
        Require(removed == end - first, "filter contains a control-flow cycle.");
    }
}
