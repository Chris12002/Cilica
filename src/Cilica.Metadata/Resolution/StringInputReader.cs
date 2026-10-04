using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Cilica.Metadata.Resolution;

public sealed record UserStringInput(ImmutableArray<ushort> CodeUnits);
public sealed record IntrinsicStringInterfaces(ImmutableArray<ResolvedType> Types);

public sealed partial class AssemblyResolver
{
    /// <summary>Reads numeric UTF-16 after checking the encoded size, without text decoding or input execution.</summary>
    public ResolutionResult<UserStringInput> ReadUserString(ResolvedAssembly assembly, int token, int maximumCodeUnits)
    {
        CheckOwner(assembly);
        if (maximumCodeUnits < 0) throw new ArgumentOutOfRangeException(nameof(maximumCodeUnits));
        string location = "user string 0x" + token.ToString("x8", System.Globalization.CultureInfo.InvariantCulture) + " in " + assembly.Identity.DisplayName;
        try
        {
            var metadata = assembly.Image.Reader;
            int offset = token & 0xffffff, size = metadata.GetHeapSize(HeapIndex.UserString);
            if ((uint)token >> 24 != 0x70 || offset == 0 || offset >= size)
                throw Failure(DiagnosticIds.InvalidOperand, "ldstr requires a non-nil token inside the user-string heap.");
            var reader = assembly.Image.PEReader.GetMetadata().GetReader(metadata.GetHeapMetadataOffset(HeapIndex.UserString) + offset, size - offset);
            if (!reader.TryReadCompressedInteger(out int bytes) || bytes < 1 || (bytes & 1) == 0 || bytes > reader.RemainingBytes)
                throw Failure(DiagnosticIds.InvalidOperand, "Malformed user-string length or truncated UTF-16 payload.");
            int count = bytes / 2;
            if (count > maximumCodeUnits)
                throw Failure(DiagnosticIds.TranslationLimit, "String literal exceeds the remaining UTF-16 code-unit budget.");
            var units = ImmutableArray.CreateBuilder<ushort>(count);
            for (int i = 0; i < count; i++) units.Add(reader.ReadUInt16());
            if (reader.ReadByte() > 1) throw Failure(DiagnosticIds.InvalidOperand, "Invalid user-string terminal flag.");
            return ResolutionResult<UserStringInput>.Resolved(new(units.MoveToImmutable()));
        }
        catch (BindingFailureException failure)
        {
            return ResolutionResult<UserStringInput>.Failed(failure.Diagnostics.Select(d => d with { Location = location }));
        }
        catch (Exception exception) when (MalformedInput.IsMalformed(exception))
        {
            return ResolutionResult<UserStringInput>.Failed(DiagnosticIds.InvalidOperand, exception.Message, location);
        }
    }

    /// <summary>Reads only interface ancestry, including nongeneric parents of generic contracts.
    /// Used to reject unmodeled String conversions, not to import implementations.</summary>
    public ResolutionResult<IntrinsicStringInterfaces> ReadIntrinsicStringInterfaces(ResolvedType text, MemberResolutionBudget budget)
    {
        ArgumentNullException.ThrowIfNull(text); ArgumentNullException.ThrowIfNull(budget);
        return Bind(text.Assembly, text.Handle, () =>
        {
            var pending = new Queue<ConstructedType>();
            var seen = new HashSet<ResolvedType>();
            pending.Enqueue(new(text, SignatureList<TypeSignature>.Empty));
            while (pending.TryDequeue(out var current))
            {
                if (!seen.Add(current.Definition)) continue;
                if (seen.Count > 256) throw Failure(DiagnosticIds.TranslationLimit, "Intrinsic String interface ancestry exceeds 256 definitions.");
                foreach (var parent in ReadInterfaceEdges(current, budget)) pending.Enqueue(parent);
            }
            seen.Remove(text);
            return new IntrinsicStringInterfaces([.. seen]);
        });
    }

    /// <summary>Validates the selected intrinsic String shell, without importing its implementation fields/interfaces.</summary>
    public ResolutionResult<ResolvedType> ResolveIntrinsicStringType(ResolvedAssembly library, MemberResolutionBudget budget)
    {
        ArgumentNullException.ThrowIfNull(budget);
        var selected = ResolveRuntimeType(library, PrimitiveTypeCode.String);
        if (selected.Value is not { } text) return selected;
        var objectResult = ResolveRuntimeType(library, PrimitiveTypeCode.Object);
        if (objectResult.Value is not { } objectType) return objectResult;
        return Bind(text.Assembly, text.Handle, () =>
        {
            var permission = AuthorizeLibraryIntrinsic(text.Assembly, text.Handle, LibraryIntrinsicCapability.StringLayout);
            if (!permission.Success) throw new BindingFailureException(permission.Diagnostics);
            InspectMember(budget);
            var definition = text.Assembly.Image.Reader.GetTypeDefinition(text.Handle);
            if (text.Assembly.IsReferenceAssembly || !definition.GetDeclaringType().IsNil
                || (definition.Attributes & (TypeAttributes.VisibilityMask | TypeAttributes.Sealed)) != (TypeAttributes.Public | TypeAttributes.Sealed))
                throw Failure(DiagnosticIds.ExecutionUnsupported, "Intrinsic String requires a public sealed top-level implementation definition.");
            var parent = ResolveType(text.Assembly, definition.BaseType);
            if (parent.Value != objectType)
                throw Failure(DiagnosticIds.ExecutionUnsupported, "Intrinsic String must directly extend the selected Object definition.");
            var reader = text.Assembly.Image.Reader;
            foreach (var owner in new[] { definition, reader.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(1)) })
                foreach (var method in owner.GetMethods())
                {
                    InspectMember(budget);
                    if (reader.StringComparer.Equals(reader.GetMethodDefinition(method).Name, ".cctor"))
                        throw Failure(DiagnosticIds.ExecutionUnsupported, "Intrinsic String type/module initialization requires later lowering.");
                }
            return text;
        });
    }
}
