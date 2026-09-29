using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;

namespace Cilica.Metadata.Resolution;

/// <summary>An immutable sequence whose equality compares elements, not array storage.</summary>
public sealed class SignatureList<T> : IReadOnlyList<T>, IEquatable<SignatureList<T>>
{
    private readonly ImmutableArray<T> _items;
    public SignatureList(IEnumerable<T> items) => _items = [.. items];
    public static SignatureList<T> Empty { get; } = new([]);
    public int Count => _items.Length;
    public T this[int index] => _items[index];
    public bool Equals(SignatureList<T>? other) => other is not null && _items.SequenceEqual(other._items);
    public override bool Equals(object? obj) => obj is SignatureList<T> other && Equals(other);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (T item in _items) hash.Add(item);
        return hash.ToHashCode();
    }
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>Structural CLI signature syntax. Generic variables retain their declaring scope.</summary>
public abstract record TypeSignature
{
    public sealed record Primitive(PrimitiveTypeCode Code) : TypeSignature;
    /// <param name="Kind">The blob's CLASS/VALUETYPE encoding, or Unknown for a bare token.</param>
    public sealed record Named(ResolvedType Definition, SignatureTypeKind Kind) : TypeSignature;
    public sealed record Variable(bool IsMethod, int Index) : TypeSignature;
    public sealed record Generic(Named Definition, SignatureList<TypeSignature> Arguments) : TypeSignature;
    public sealed record Vector(TypeSignature Element) : TypeSignature;
    public sealed record Array(TypeSignature Element, int Rank, SignatureList<int> Sizes,
        SignatureList<int> LowerBounds) : TypeSignature;
    public sealed record Pointer(TypeSignature Element) : TypeSignature;
    public sealed record ByReference(TypeSignature Element) : TypeSignature;
    public sealed record Pinned(TypeSignature Element) : TypeSignature;
    public sealed record Modified(Named Modifier, bool Required, TypeSignature Element) : TypeSignature;
    public sealed record FunctionPointer(MethodSignature Signature) : TypeSignature;
}

public sealed record MethodSignature(byte Header, int GenericArity, TypeSignature ReturnType,
    SignatureList<TypeSignature> Parameters);

/// <summary>A declaration owner, with arguments in the caller's generic context.</summary>
public sealed record ConstructedType(ResolvedType Definition, SignatureList<TypeSignature> Arguments);

public sealed record ResolvedMethod(ConstructedType DeclaringType, MethodDefinitionHandle Handle,
    SignatureList<TypeSignature> MethodArguments, MethodSignature Signature);

public sealed record ResolvedField(ConstructedType DeclaringType, FieldDefinitionHandle Handle,
    TypeSignature Signature);
