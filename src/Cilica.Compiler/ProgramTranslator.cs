using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

public static partial class Int32Translator
{
    /// <summary>Resolves and verifies the entire direct static-call closure before returning IR.</summary>
    public static ProgramTranslationResult TranslateProgram(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle root, MethodReadLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        try { return new(new Closure(resolver, limits).Run(assembly, root), []); }
        catch (TranslationFailure failure) { return new(null, [failure.Diagnostic]); }
    }

    internal static IntegerTranslationResult TranslateIntegerProgram(AssemblyResolver resolver, ResolvedAssembly assembly,
        MethodDefinitionHandle root, MethodReadLimits? limits)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        try
        {
            var flow = new Closure(resolver, limits, integers64: true).Run(assembly, root);
            IrValidator.Validate(flow, integers64: true);
            return new(new(flow), []);
        }
        catch (TranslationFailure failure) { return new(null, [failure.Diagnostic]); }
    }

    private sealed record ScalarCall(IrMethodId Target, ImmutableArray<IrType> Parameters, IrType ReturnType);

    private static IrType? ScalarType(TypeSignature signature, bool integers64) => signature switch
    {
        TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 } => IrType.Int32,
        TypeSignature.Primitive { Code: PrimitiveTypeCode.UInt32 } when integers64 => IrType.Int32,
        TypeSignature.Primitive { Code: PrimitiveTypeCode.SByte or PrimitiveTypeCode.Byte or PrimitiveTypeCode.Int16
            or PrimitiveTypeCode.UInt16 or PrimitiveTypeCode.Char or PrimitiveTypeCode.Boolean } when integers64 => IrType.Int32,
        TypeSignature.Primitive { Code: PrimitiveTypeCode.Int64 or PrimitiveTypeCode.UInt64 } when integers64 => IrType.Int64,
        _ => null,
    };

    private sealed class Closure(AssemblyResolver resolver, MethodReadLimits? limits, bool integers64 = false)
    {
        private sealed record Work(ResolvedAssembly Assembly, MethodInput Input, Work? Parent, int CallOffset);
        private readonly Dictionary<IrMethodId, Work> _discovered = [];
        private readonly Queue<Work> _pending = new();
        private readonly List<IrMethod> _methods = [];
        private readonly ProgramResources _resources = new();
        private long _bytes, _instructions, _callArguments;

        internal IrProgram Run(ResolvedAssembly assembly, MethodDefinitionHandle root)
        {
            Work entry = Discover(assembly, root, null, 0);
            while (_pending.TryDequeue(out Work? work))
            {
                try
                {
                    IrMethod method = new Translation(work.Input, instruction => Resolve(work, instruction), integers64).Run();
                    if (!_resources.Add(method)) Fail(work, DiagnosticIds.TranslationLimit, "Aggregate program IR limit exceeded.", 0);
                    _methods.Add(method);
                }
                catch (TranslationFailure failure)
                {
                    // The location identifies the failing method/instruction; the message
                    // explains the first deterministic inclusion path from the entry.
                    throw new TranslationFailure(failure.Diagnostic with { Message = failure.Diagnostic.Message + Path(work) });
                }
            }
            return new(IrMethodId.From(entry.Input.Source), [.. _methods.OrderBy(m => m.Source.ContentHash, StringComparer.Ordinal)
                .ThenBy(m => m.Source.Token)]);
        }

        private Work Discover(ResolvedAssembly assembly, MethodDefinitionHandle method, Work? parent, int offset)
        {
            var key = new IrMethodId(assembly.ContentHash.ToLowerInvariant(), MetadataTokens.GetToken(method));
            if (_discovered.TryGetValue(key, out Work? known)) return known;
            if (_discovered.Count >= ProgramLimits.MaximumMethods)
                Fail(parent, DiagnosticIds.TranslationLimit, "Direct-call method limit exceeded.", offset);
            if (_bytes >= ProgramLimits.MaximumBodyBytes || _instructions >= ProgramLimits.MaximumInstructions)
                Fail(parent, DiagnosticIds.TranslationLimit, "Aggregate program IL byte/instruction limit exceeded.", offset);
            MethodReadLimits requested = limits ?? new();
            var bounded = requested with
            {
                MaximumBodyBytes = Math.Min(requested.MaximumBodyBytes, ProgramLimits.MaximumBodyBytes - (int)_bytes),
                MaximumInstructions = Math.Min(requested.MaximumInstructions, ProgramLimits.MaximumInstructions - (int)_instructions),
            };
            var read = resolver.ReadMethodInput(assembly, method, bounded);
            if (read.Value is not MethodInput input) throw new TranslationFailure(read.Diagnostics[0] with
            { Message = read.Diagnostics[0].Message + (parent is null ? "" : " Called at " + Location(parent, offset) + ".") });
            _bytes += input.CodeSize; _instructions += input.Instructions.Length;
            if (_bytes > ProgramLimits.MaximumBodyBytes || _instructions > ProgramLimits.MaximumInstructions)
                Fail(parent, DiagnosticIds.TranslationLimit, "Aggregate program IL byte/instruction limit exceeded.", offset);
            var work = new Work(assembly, input, parent, offset);
            _discovered.Add(key, work); _pending.Enqueue(work); return work;
        }

        private ScalarCall Resolve(Work caller, MethodInstruction instruction)
        {
            int token = instruction.IntegerOperand ?? 0;
            if ((uint)token >> 24 is not (0x06 or 0x0a or 0x2b) || (token & 0xffffff) == 0)
                Fail(caller, DiagnosticIds.InvalidOperand, "call needs a non-nil MethodDef, MemberRef or MethodSpec token.", instruction.Offset);
            var resolved = resolver.ResolveDeclaredMethod(caller.Assembly, MetadataTokens.EntityHandle(token));
            if (resolved.Value is not ResolvedMethod target)
                throw new TranslationFailure(resolved.Diagnostics[0] with { Location = Location(caller, instruction.Offset) });
            if (target.MethodArguments.Count != 0 || target.DeclaringType.Arguments.Count != 0)
                Fail(caller, DiagnosticIds.ExecutionSignatureUnsupported, "Generic call instantiations are outside " + (integers64 ? IntegerTranslator.Profile : IrProgram.Profile) + ".", instruction.Offset);
            if (target.Signature.Header != 0 || ScalarType(target.Signature.ReturnType, integers64) is null
                || target.Signature.Parameters.Any(p => ScalarType(p, integers64) is null))
                Fail(caller, DiagnosticIds.ExecutionSignatureUnsupported, integers64
                    ? "Direct integer calls require static default-convention integer, Boolean or Char parameters and return."
                    : "Direct calls require static default-convention Int32 parameters and return.", instruction.Offset);
            if (target.Signature.Parameters.Count > IrLimits.MaximumParameters)
                Fail(caller, DiagnosticIds.TranslationLimit, "Call parameter limit exceeded.", instruction.Offset);
            _callArguments += target.Signature.Parameters.Count;
            if (_callArguments > ProgramLimits.MaximumCallArguments)
                Fail(caller, DiagnosticIds.TranslationLimit, "Aggregate program call-argument limit exceeded.", instruction.Offset);
            Work callee = Discover(target.DeclaringType.Definition.Assembly, target.Handle, caller, instruction.Offset);
            bool sameOwner = caller.Input.Source.ContentHash == callee.Input.Source.ContentHash
                && caller.Input.OwnerToken == callee.Input.OwnerToken;
            MethodAttributes access = callee.Input.Attributes & MethodAttributes.MemberAccessMask;
            if (access == MethodAttributes.PrivateScope || (!sameOwner &&
                (access != MethodAttributes.Public || !callee.Input.OwnerExternallyVisible)))
                Fail(caller, DiagnosticIds.ExecutionUnsupported,
                    "This call profile accepts members of the same declaring type, or public methods on externally visible types; other access rules require explicit lowering.", instruction.Offset);
            return new(IrMethodId.From(callee.Input.Source), [.. target.Signature.Parameters.Select(p => ScalarType(p, integers64)!)], ScalarType(target.Signature.ReturnType, integers64)!);
        }

        private static string Location(Work? work, int offset) => work is null ? "entry" :
            work.Input.Source.AssemblyIdentity + " " + work.Input.Source.Name + " token 0x" +
            work.Input.Source.Token.ToString("x8", System.Globalization.CultureInfo.InvariantCulture) +
            " IL_" + offset.ToString("x4", System.Globalization.CultureInfo.InvariantCulture);

        private static void Fail(Work? work, string code, string message, int offset) =>
            throw new TranslationFailure(new(code, DiagnosticSeverity.Error, message, Location(work, offset)));

        private static string Path(Work work)
        {
            var path = new Stack<string>();
            for (Work current = work; current.Parent is not null; current = current.Parent)
                path.Push(Location(current.Parent, current.CallOffset));
            if (path.Count == 0) path.Push(Location(work, 0));
            return " Reachable via: " + string.Join(" -> ", path) + ".";
        }
    }
}
