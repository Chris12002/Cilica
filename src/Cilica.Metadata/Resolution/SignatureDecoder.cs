using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Cilica.Metadata.Resolution;

// A bounded parser is intentional: the framework decoder recursively descends a blob
// before calling its provider, so provider callbacks cannot bound malicious nesting.
internal sealed class SignatureDecoder(AssemblyResolver resolver, ResolvedAssembly assembly)
{
    private const int MaximumDepth = 64;
    private const int MaximumNodes = 4096;
    private int _nodes;
    private MetadataReader Metadata => assembly.Image.Reader;

    internal TypeSignature Type(EntityHandle handle)
    {
        Guard(0);
        if (handle.Kind != HandleKind.TypeSpecification)
            return new TypeSignature.Named(Definition(handle), SignatureTypeKind.Unknown);
        var specification = (TypeSpecificationHandle)handle;
        BlobReader blob = Metadata.GetBlobReader(Metadata.GetTypeSpecification(specification).Signature);
        TypeSignature type = ReadType(ref blob, 1);
        End(blob);
        return type;
    }

    internal MethodSignature Method(BlobHandle handle)
    {
        BlobReader blob = Metadata.GetBlobReader(handle);
        MethodSignature signature = ReadMethod(ref blob, 0);
        End(blob);
        return signature;
    }

    internal TypeSignature Field(BlobHandle handle)
    {
        BlobReader blob = Metadata.GetBlobReader(handle);
        if (blob.ReadByte() != 0x06) throw Invalid("Expected a FIELD signature.");
        TypeSignature type = ReadType(ref blob, 0);
        End(blob);
        return type;
    }

    internal SignatureList<TypeSignature> MethodArguments(BlobHandle handle)
    {
        BlobReader blob = Metadata.GetBlobReader(handle);
        if (blob.ReadByte() != 0x0a) throw Invalid("Expected a GENERICINST method signature.");
        int count = Count(ref blob);
        var arguments = new List<TypeSignature>();
        for (int i = 0; i < count; i++) arguments.Add(ReadType(ref blob, 0));
        End(blob);
        return new(arguments);
    }

    internal SignatureList<TypeSignature> Locals(BlobHandle handle)
    {
        BlobReader blob = Metadata.GetBlobReader(handle);
        if (blob.ReadByte() != 0x07) throw Invalid("Expected a LOCAL_SIG signature.");
        int count = Count(ref blob);
        var locals = new List<TypeSignature>();
        for (int i = 0; i < count; i++) locals.Add(ReadType(ref blob, 0, allowByRef: true, allowPinned: true));
        End(blob);
        return new(locals);
    }

    private MethodSignature ReadMethod(ref BlobReader blob, int depth)
    {
        Guard(depth);
        byte header = blob.ReadByte();
        int convention = header & 0x0f;
        if ((header & 0x80) != 0 || ((header & 0x40) != 0 && (header & 0x20) == 0))
            throw Invalid("Invalid method signature flags.");
        if (convention == 5) throw new UnsupportedBindingException("Vararg signature binding is not supported.");
        if (convention > 4 && convention != 9) throw Invalid("Invalid method calling convention.");
        int arity = (header & 0x10) != 0 ? Integer(ref blob) : 0;
        if (arity > MaximumNodes) throw Invalid("Generic method arity exceeds 4096.");
        if ((header & 0x10) != 0 && arity == 0) throw Invalid("Generic method signature has zero arity.");
        if (depth > 0 && arity != 0)
            throw new UnsupportedBindingException("Generic function-pointer signatures require a separate generic binding context.");
        int count = Count(ref blob);
        TypeSignature returns = ReadType(ref blob, depth + 1, allowVoid: true, allowByRef: true);
        var parameters = new List<TypeSignature>();
        for (int i = 0; i < count; i++) parameters.Add(ReadType(ref blob, depth + 1, allowByRef: true));
        return new(header, arity, returns, new(parameters));
    }

    private TypeSignature ReadType(ref BlobReader blob, int depth, bool allowVoid = false, bool allowByRef = false, bool allowPinned = false)
    {
        Guard(depth);
        byte code = blob.ReadByte();
        switch (code)
        {
            case 0x01 when allowVoid:
            case >= 0x02 and <= 0x0e:
            case 0x16:
            case 0x18:
            case 0x19:
            case 0x1c:
                return new TypeSignature.Primitive((PrimitiveTypeCode)code);
            case 0x0f:
                return new TypeSignature.Pointer(ReadType(ref blob, depth + 1, allowVoid: true));
            case 0x10 when allowByRef:
                return new TypeSignature.ByReference(ReadType(ref blob, depth + 1));
            case 0x45 when allowPinned:
                return new TypeSignature.Pinned(ReadType(ref blob, depth + 1, allowByRef: true));
            case 0x11:
            case 0x12:
                return new TypeSignature.Named(Definition(TypeToken(ref blob)), code == 0x11 ? SignatureTypeKind.ValueType : SignatureTypeKind.Class);
            case 0x13:
            case 0x1e:
                return new TypeSignature.Variable(code == 0x1e, Integer(ref blob));
            case 0x14:
            {
                TypeSignature element = ReadType(ref blob, depth + 1);
                int rank = Integer(ref blob);
                if (rank == 0 || rank > 32) throw Invalid("Array rank must be between 1 and 32.");
                int count = Count(ref blob);
                if (count > rank) throw Invalid("Array size count exceeds its rank.");
                var sizes = new List<int>();
                for (int i = 0; i < count; i++) sizes.Add(Integer(ref blob));
                count = Count(ref blob);
                if (count > rank) throw Invalid("Array lower-bound count exceeds its rank.");
                var bounds = new List<int>();
                for (int i = 0; i < count; i++) bounds.Add(blob.ReadCompressedSignedInteger());
                return new TypeSignature.Array(element, rank, new(sizes), new(bounds));
            }
            case 0x15:
            {
                byte kind = blob.ReadByte();
                if (kind is not (0x11 or 0x12)) throw Invalid("Generic instance requires CLASS or VALUETYPE.");
                var definition = new TypeSignature.Named(Definition(TypeToken(ref blob)), kind == 0x11 ? SignatureTypeKind.ValueType : SignatureTypeKind.Class);
                int count = Count(ref blob);
                int arity = definition.Definition.Assembly.Image.Reader.GetTypeDefinition(definition.Definition.Handle).GetGenericParameters().Count;
                if (count == 0 || count != arity) throw Invalid("Generic type argument count does not match its definition.");
                var arguments = new List<TypeSignature>();
                for (int i = 0; i < count; i++) arguments.Add(ReadType(ref blob, depth + 1));
                return new TypeSignature.Generic(definition, new(arguments));
            }
            case 0x1b:
                return new TypeSignature.FunctionPointer(ReadMethod(ref blob, depth + 1));
            case 0x1d:
                return new TypeSignature.Vector(ReadType(ref blob, depth + 1));
            case 0x1f:
            case 0x20:
            {
                // Modifier TypeSpecs occur in real metadata; preserve their structure
                // only once the profile supports them, never erase a modifier to bind.
                var modifier = new TypeSignature.Named(Definition(TypeToken(ref blob)), SignatureTypeKind.Unknown);
                return new TypeSignature.Modified(modifier, code == 0x1f,
                    ReadType(ref blob, depth + 1, allowVoid, allowByRef, allowPinned));
            }
            default: throw Invalid($"Invalid or unsupported type signature element 0x{code:x2}.");
        }
    }

    private ResolvedType Definition(EntityHandle handle)
    {
        if (handle.Kind == HandleKind.TypeSpecification)
            throw new UnsupportedBindingException("A CLASS, VALUETYPE or modifier token referring to a TypeSpec requires a future binding profile.");
        var result = resolver.ResolveType(assembly, handle);
        return result.Value ?? throw new BindingFailureException(result.Diagnostics);
    }

    private static EntityHandle TypeToken(ref BlobReader blob)
    {
        int coded = Integer(ref blob);
        int row = coded >> 2;
        if (row == 0 || row > 0x00ffffff || (coded & 3) == 3) throw Invalid("Invalid TypeDefOrRef signature token.");
        return (coded & 3) switch
        {
            0 => MetadataTokens.TypeDefinitionHandle(row),
            1 => MetadataTokens.TypeReferenceHandle(row),
            _ => MetadataTokens.TypeSpecificationHandle(row),
        };
    }

    private static int Integer(ref BlobReader blob)
    {
        int value = blob.ReadCompressedInteger();
        if (value < 0) throw Invalid("Invalid compressed integer.");
        return value;
    }

    private static int Count(ref BlobReader blob)
    {
        int value = Integer(ref blob);
        if (value > MaximumNodes || value > blob.RemainingBytes) throw Invalid("Signature count exceeds the available data or 4096-element limit.");
        return value;
    }

    private void Guard(int depth)
    {
        if (depth > MaximumDepth || ++_nodes > MaximumNodes)
            throw Invalid("Signature exceeds the 64-level or 4096-node decoding limit.");
    }

    private static void End(BlobReader blob)
    {
        if (blob.RemainingBytes != 0) throw Invalid("Trailing data in signature blob.");
    }

    private static BadImageFormatException Invalid(string message) => new(message);
}

internal sealed class BindingFailureException(IEnumerable<Diagnostic> diagnostics) : Exception
{
    internal IReadOnlyList<Diagnostic> Diagnostics { get; } = diagnostics.ToArray();
}
