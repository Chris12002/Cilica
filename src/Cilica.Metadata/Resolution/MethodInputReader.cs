using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.IL;

namespace Cilica.Metadata.Resolution;

public sealed partial class AssemblyResolver
{
    /// <summary>Reads one definition as bounded compiler input; never executes its code.</summary>
    public ResolutionResult<MethodInput> ReadMethodInput(ResolvedAssembly assembly, MethodDefinitionHandle handle,
        MethodReadLimits? limits = null)
    {
        CheckOwner(assembly);
        limits ??= new();
        if (limits.MaximumBodyBytes <= 0 || limits.MaximumInstructions <= 0 || limits.MaximumInitializerMembers <= 0 || limits.MaximumOwnerDepth is <= 0 or > TypeReadLimits.MaximumSupportedOwnerDepth)
            throw new ArgumentOutOfRangeException(nameof(limits), "Method input limits must be positive, with owner depth 1..64.");
        return Bind<MethodInput>(assembly, handle, () =>
        {
            var reader = assembly.Image.Reader;
            var method = reader.GetMethodDefinition(handle);
            var header = ReadMethodHeader(assembly, handle, limits.MaximumOwnerDepth);
            var owner = reader.GetTypeDefinition(method.GetDeclaringType());
            if (method.RelativeVirtualAddress == 0)
                throw Failure(DiagnosticIds.ExecutionUnsupported, "Selected method has no managed IL body.");
            MethodBodyBlock body;
            try { body = assembly.Image.PEReader.GetMethodBody(method.RelativeVirtualAddress); }
            catch (Exception exception) when (MalformedInput.IsMalformed(exception))
            {
                throw Failure(DiagnosticIds.MalformedMethodBody, "Selected method body header is malformed: " + exception.Message);
            }
            var il = body.GetILReader();
            if (il.Length > limits.MaximumBodyBytes)
                throw Failure(DiagnosticIds.TranslationLimit, "Selected method exceeds the body-byte translation limit.");
            var instructions = ILDecoder.Decode(il, out string? error, limits.MaximumInstructions);
            if (error is not null)
                throw Failure(instructions.Count >= limits.MaximumInstructions ? DiagnosticIds.TranslationLimit : DiagnosticIds.MalformedMethodBody, error);
            ImmutableArray<TypeSignature> locals = body.LocalSignature.IsNil ? [] : [.. new SignatureDecoder(this, assembly)
                .Locals(reader.GetStandaloneSignature(body.LocalSignature).Signature)];
            int work = 0;
            bool Initializer(TypeDefinition definition)
            {
                foreach (var candidate in definition.GetMethods())
                {
                    if (++work > limits.MaximumInitializerMembers)
                        throw Failure(DiagnosticIds.TranslationLimit, "Initializer inspection exceeds the metadata-member translation limit.");
                    if (reader.StringComparer.Equals(reader.GetMethodDefinition(candidate).Name, ".cctor")) return true;
                }
                return false;
            }
            return new(header.Source, header.Signature, method.Attributes, method.ImplAttributes, header.Arity,
                assembly.IsReferenceAssembly, Initializer(owner), Initializer(reader.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(1))),
                locals, body.LocalVariablesInitialized, body.ExceptionRegions.Length, il.Length, body.MaxStack,
                [.. instructions.Select(i => new MethodInstruction(i.Offset, i.Length, i.Code.Name,
                    i.AccessesArgument is not null ? i.VariableIndex : i.Operand as int?, [.. i.Targets])
                    { Int64Operand = i.Operand as long? })],
                MetadataTokens.GetToken(method.GetDeclaringType()), header.Visible)
            { DeclaredName = reader.GetString(method.Name), MetadataMembersRead = work, OwnerAssemblyVisible = header.AssemblyVisible };
        });
    }
}
