using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.IL;

namespace Cilica.Metadata.Resolution;

/// <summary>Validated region boundaries and scoped catch identities; no handler execution is implied.</summary>
public sealed record ManagedExceptionRegionInput(ExceptionRegionKind Kind, int TryStart, int TryEnd,
    int HandlerStart, int HandlerEnd, int? FilterStart, TypeSignature? CatchType, string? FilterKind);

public sealed record ManagedExceptionMethodInput(MethodInput Method, ImmutableArray<ManagedExceptionRegionInput> Regions);

public sealed partial class AssemblyResolver
{
    /// <summary>Reads a closed method and validates its protected control flow and stage-A filters as metadata.</summary>
    public ResolutionResult<ManagedExceptionMethodInput> ReadManagedExceptionMethodInput(ResolvedMethod method,
        ResolvedAssembly runtimeLibrary, MethodReadLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(method);
        limits ??= new();
        var input = ReadClosedMethodInput(method, runtimeLibrary, limits);
        if (input.Value is not { } value) return ResolutionResult<ManagedExceptionMethodInput>.Failed(input.Diagnostics);
        var assembly = method.DeclaringType.Definition.Assembly;
        return Bind(assembly, method.Handle, () =>
        {
            var definition = assembly.Image.Reader.GetMethodDefinition(method.Handle);
            var body = assembly.Image.PEReader.GetMethodBody(definition.RelativeVirtualAddress);
            if (body.ExceptionRegions.Length > 256)
                throw Failure(DiagnosticIds.TranslationLimit, "Managed exception methods support at most 256 clauses.");
            int remaining = limits.MaximumInitializerMembers - value.MetadataMembersRead;
            if (remaining <= 0) throw Failure(DiagnosticIds.TranslationLimit, "Managed exception metadata budget exhausted.");
            var budget = new MemberResolutionBudget(Math.Min(65536, remaining));
            var instructions = ILDecoder.Decode(body.GetILReader(), out var error, limits.MaximumInstructions);
            if (error is not null) throw Failure(DiagnosticIds.MalformedMethodBody, error);
            var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
            void Report(string code, string message, int offset) => diagnostics.Add(new(code, DiagnosticSeverity.Error, message,
                value.Source.Name + " IL_" + offset.ToString("x4", System.Globalization.CultureInfo.InvariantCulture)));
            // This reader has one exact closed caller. Repeated operand tokens
            // share declaration resolution; opcode-specific stack effects do not.
            var calls = new Dictionary<int, ResolvedMethod>();
            CallShape? Call(Instruction instruction)
            {
                if (instruction.Code.Name == "calli")
                {
                    Report(DiagnosticIds.ExecutionUnsupported, "Indirect unmanaged/function-pointer calls require a supported lowering.", instruction.Offset); return null;
                }
                if (!calls.TryGetValue(instruction.Token, out var target))
                {
                    var resolved = ResolveClosedCall(method, MetadataTokens.EntityHandle(instruction.Token), budget);
                    if (resolved.Value is not { } declaration) { diagnostics.AddRange(resolved.Diagnostics); return null; }
                    calls.Add(instruction.Token, target = declaration);
                }
                var signature = target.Signature;
                int receiver = (signature.Header & 0x20) != 0 && instruction.Code.Name != "newobj" ? 1 : 0;
                return new(signature.Parameters.Count + receiver, instruction.Code.Name == "newobj"
                    || signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Void } ? 1 : 0);
            }
            var analyzer = new ControlFlowAnalyzer(instructions, value.CodeSize, value.MaxStack,
                value.Signature.Parameters.Count + ((value.Signature.Header & 0x20) != 0 ? 1 : 0), value.Locals.Length,
                value.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Void }, Call, Report);
            var analysis = analyzer.Analyze(body.ExceptionRegions);
            var regions = ImmutableArray.CreateBuilder<ManagedExceptionRegionInput>();
            for (int i = 0; i < body.ExceptionRegions.Length; ++i)
            {
                var region = body.ExceptionRegions[i]; TypeSignature? caught = null;
                if (!budget.TryConsume(1)) throw Failure(DiagnosticIds.TranslationLimit, "Managed exception metadata budget exhausted.");
                if (region.Kind == ExceptionRegionKind.Catch)
                {
                    var type = ResolveClosedTypeOperand(method, region.CatchType);
                    if (type.Value is null) diagnostics.AddRange(type.Diagnostics); else caught = type.Value;
                }
                string? kind = null;
                if (region.Kind == ExceptionRegionKind.Filter)
                {
                    analysis.FilterKinds.TryGetValue(i, out kind);
                    if (kind != "type-test") Report(DiagnosticIds.ExecutionUnsupported,
                        "General exception filters require full search-before-unwind support; only acyclic type-test filters are admitted.", region.FilterOffset);
                }
                regions.Add(new(region.Kind, region.TryOffset, region.TryOffset + region.TryLength,
                    region.HandlerOffset, region.HandlerOffset + region.HandlerLength,
                    region.Kind == ExceptionRegionKind.Filter ? region.FilterOffset : null, caught, kind));
            }
            if (diagnostics.Count != 0) throw new BindingFailureException(diagnostics);
            return new ManagedExceptionMethodInput(value with
                { MetadataMembersRead = checked(value.MetadataMembersRead + Math.Min(65536, remaining) - budget.Remaining) }, regions.ToImmutable());
        });
    }
}
