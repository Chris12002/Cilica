using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
namespace Cilica.Metadata.Resolution;

public sealed record StaticFieldDataInput(ImmutableArray<byte> Bytes)
{
    public const int MaximumBytes = 2048;
}
public sealed partial class AssemblyResolver
{
    /// <summary>Reads a bounded prefix of same-image, reference-free, explicit-size RVA storage.</summary>
    public ResolutionResult<StaticFieldDataInput> ReadStaticFieldData(ResolvedAssembly assembly,
        FieldDefinitionHandle handle, int length, ResolvedType valueBase, MemberResolutionBudget budget)
    {
        ArgumentNullException.ThrowIfNull(assembly); ArgumentNullException.ThrowIfNull(budget);
        return Bind(assembly, handle, () => {
            if (length is < 1 or > StaticFieldDataInput.MaximumBytes)
                throw Failure(DiagnosticIds.TranslationLimit, "Static array data byte limit exceeded.");
            InspectMember(budget);
            var reader = assembly.Image.Reader;
            var field = reader.GetFieldDefinition(handle);
            if (assembly.IsReferenceAssembly || field.Attributes != (FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.InitOnly | FieldAttributes.HasFieldRVA)
                || field.GetRelativeVirtualAddress() <= 0 || field.GetOffset() != -1 || !field.GetDefaultValue().IsNil || !field.GetMarshallingDescriptor().IsNil)
                throw Failure(DiagnosticIds.ExecutionUnsupported, "Static array data requires same-image internal static readonly RVA storage.");
            var signature = new SignatureDecoder(this, assembly).Field(field.Signature);
            int storageSize;
            if (signature is TypeSignature.Primitive primitive)
            {
                storageSize = primitive.Code switch {
                    PrimitiveTypeCode.SByte or PrimitiveTypeCode.Byte => 1,
                    PrimitiveTypeCode.Int16 or PrimitiveTypeCode.UInt16 => 2,
                    PrimitiveTypeCode.Int32 or PrimitiveTypeCode.UInt32 => 4,
                    PrimitiveTypeCode.Int64 or PrimitiveTypeCode.UInt64 => 8, _ => 0 };
            }
            else if (signature is TypeSignature.Named { Kind: SignatureTypeKind.ValueType } named && named.Definition.Assembly == assembly)
            {
                InspectMember(budget);
                var type = reader.GetTypeDefinition(named.Definition.Handle);
                var layout = type.GetLayout();
                if (type.Attributes != (TypeAttributes.NestedAssembly | TypeAttributes.Sealed | TypeAttributes.ExplicitLayout)
                    || type.GetGenericParameters().Count != 0 || type.GetFields().Count != 0 || type.GetMethods().Count != 0
                    || type.GetInterfaceImplementations().Count != 0 || layout.PackingSize != 1
                    || type.GetDeclaringType() != field.GetDeclaringType()
                    || new SignatureDecoder(this, assembly).Type(type.BaseType) is not TypeSignature.Named parent || parent.Definition != valueBase)
                    throw Failure(DiagnosticIds.ExecutionUnsupported, "Static array data requires bounded fieldless explicit layout storage.");
                storageSize = layout.Size;
            }
            else throw Failure(DiagnosticIds.ExecutionUnsupported, "Static array data requires integer or same-image explicit-size storage.");
            if (storageSize < length || storageSize > StaticFieldDataInput.MaximumBytes)
                throw Failure(DiagnosticIds.ExecutionUnsupported, "Static array data storage is too small or exceeds the byte limit.");
            // Read only file-backed bytes. No pointer or host endianness enters the output.
            int rva = field.GetRelativeVirtualAddress(); bool backed = false;
            foreach (var section in assembly.Image.PEReader.PEHeaders.SectionHeaders)
            {
                long delta = (long)rva - section.VirtualAddress;
                if (delta >= 0 && delta + storageSize <= section.SizeOfRawData) { backed = true; break; }
            }
            if (!backed) throw Failure(DiagnosticIds.ExecutionUnsupported, "Static array data lies outside a file-backed PE section.");
            if (!budget.TryConsume(length)) throw Failure(DiagnosticIds.TranslationLimit, "Static array data work limit exceeded.");
            return new StaticFieldDataInput(assembly.Image.PEReader.GetSectionData(rva).GetContent(0, length));
        });
    }
}
