using System;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text;

namespace Cilica.Metadata.Resolution;

/// <summary>Detached closed signature syntax; contains no readers or executable input.</summary>
public abstract record ClosedTypeShape
{
    public sealed record Primitive(PrimitiveTypeCode Code) : ClosedTypeShape;
    public sealed record Named(SignatureTypeKind Kind, string ContentHash, int Token) : ClosedTypeShape;
    public sealed record Generic(Named Definition, ImmutableArray<ClosedTypeShape> Arguments) : ClosedTypeShape;
    public sealed record Vector(ClosedTypeShape Element) : ClosedTypeShape;
}

public sealed record ClosedGenericEvidence(ImmutableArray<ClosedTypeShape> Types, ImmutableArray<ClosedTypeShape> Methods)
{
    public static ClosedGenericEvidence Capture(SignatureList<TypeSignature> types, SignatureList<TypeSignature> methods)
    {
        ArgumentNullException.ThrowIfNull(types); ArgumentNullException.ThrowIfNull(methods);
        int nodes = 0;
        ClosedTypeShape Copy(TypeSignature type, int depth, bool definition = false)
        {
            if (++nodes > 4096 || depth > 64) throw new ArgumentException("Closed specialization exceeds 4096 signature nodes or 64 levels.");
            switch (type)
            {
                case TypeSignature.Primitive p when ClosedGenericKey.IsIntegerArgument(p.Code) || p.Code is PrimitiveTypeCode.Object or PrimitiveTypeCode.String:
                    return new ClosedTypeShape.Primitive(p.Code);
                case TypeSignature.Named n when n.Kind is SignatureTypeKind.Class or SignatureTypeKind.ValueType:
                    if (!definition && n.Definition.Assembly.Image.Reader.GetTypeDefinition(n.Definition.Handle).GetGenericParameters().Count != 0)
                        throw new ArgumentException("Open generic definitions are not closed type arguments.");
                    return new ClosedTypeShape.Named(n.Kind, n.Definition.Assembly.ContentHash, MetadataTokens.GetToken(n.Definition.Handle));
                case TypeSignature.Generic g:
                    if (g.Arguments.Count == 0 || g.Arguments.Count != g.Definition.Definition.Assembly.Image.Reader.GetTypeDefinition(g.Definition.Definition.Handle).GetGenericParameters().Count)
                        throw new ArgumentException("Closed type argument count does not match its generic declaration.");
                    if (g.Arguments.Count > 4096 - nodes) throw new ArgumentException("Closed specialization exceeds 4096 signature nodes.");
                    var owner = (ClosedTypeShape.Named)Copy(g.Definition, depth + 1, true);
                    var arguments = ImmutableArray.CreateBuilder<ClosedTypeShape>(g.Arguments.Count);
                    foreach (var argument in g.Arguments) arguments.Add(Copy(argument, depth + 1));
                    return new ClosedTypeShape.Generic(owner, arguments.ToImmutable());
                case TypeSignature.Vector v: return new ClosedTypeShape.Vector(Copy(v.Element, depth + 1));
                default: throw new ArgumentException("Closed specialization requires supported concrete type arguments; open variables, modifiers, byrefs and pointers are unsupported.");
            }
        }
        if (types.Count > 4096 || methods.Count > 4096 - types.Count) throw new ArgumentException("Closed specialization exceeds 4096 signature nodes.");
        var t = ImmutableArray.CreateBuilder<ClosedTypeShape>(types.Count);
        var m = ImmutableArray.CreateBuilder<ClosedTypeShape>(methods.Count);
        foreach (var type in types) t.Add(Copy(type, 0));
        foreach (var method in methods) m.Add(Copy(method, 0));
        return new(t.ToImmutable(), m.ToImmutable());
    }

    /// <summary>Recomputes the existing specialization key after bounded detached validation.</summary>
    public string Key() => Key(out _);

    public string Key(out int nodes)
    {
        if (Types.IsDefault || Methods.IsDefault || Types.Length > 4096 || Methods.Length > 4096 - Types.Length)
            throw new ArgumentException("Closed evidence requires bounded initialized argument lists.");
        var text = new StringBuilder(); int count = 0;
        void Add(ClosedTypeShape? type, int depth)
        {
            if (++count > 4096 || depth > 64) throw new ArgumentException("Closed specialization exceeds 4096 signature nodes or 64 levels.");
            switch (type)
            {
                case ClosedTypeShape.Primitive p when ClosedGenericKey.IsIntegerArgument(p.Code) || p.Code is PrimitiveTypeCode.Object or PrimitiveTypeCode.String:
                    text.Append('p').Append((int)p.Code).Append(';'); break;
                case ClosedTypeShape.Named n when n.Kind is SignatureTypeKind.Class or SignatureTypeKind.ValueType
                    && n.ContentHash is { Length: 64 } && IsHash(n.ContentHash) && (uint)n.Token >> 24 == 2 && (n.Token & 0xffffff) > 1:
                    text.Append('n').Append((int)n.Kind).Append(':').Append(n.ContentHash).Append(':').Append(n.Token).Append(';'); break;
                case ClosedTypeShape.Generic g when g.Definition is not null && !g.Arguments.IsDefaultOrEmpty && g.Arguments.Length <= 4096 - count:
                    text.Append('g').Append(g.Arguments.Length).Append('['); Add(g.Definition, depth + 1);
                    foreach (var argument in g.Arguments) Add(argument, depth + 1);
                    text.Append(']'); break;
                case ClosedTypeShape.Vector v: text.Append('v'); Add(v.Element, depth + 1); break;
                default: throw new ArgumentException("Invalid detached closed type shape.");
            }
        }
        text.Append("types:").Append(Types.Length).Append('[');
        foreach (var type in Types) Add(type, 0);
        text.Append("]methods:").Append(Methods.Length).Append('[');
        foreach (var method in Methods) Add(method, 0);
        text.Append(']'); nodes = count;
        return count == 0 ? "" : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static bool IsHash(string value)
    {
        foreach (char c in value) if (c is not (>= '0' and <= '9' or >= 'a' and <= 'f')) return false;
        return true;
    }
}
