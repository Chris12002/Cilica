using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text;
using Cilica.Metadata.IL;

namespace Cilica.Metadata.Resolution;

/// <summary>Bounded, content-scoped closed signature keys; no metadata readers survive in the result.</summary>
public static class ClosedGenericKey
{
    public static string Create(SignatureList<TypeSignature> types, SignatureList<TypeSignature> methods)
    {
        if (types.Count == 0 && methods.Count == 0) return "";
        var text = new StringBuilder(); int nodes = 0;
        void Add(TypeSignature type, int depth, bool genericDefinition = false)
        {
            if (++nodes > 4096 || depth > 64) throw new ArgumentException("Closed specialization exceeds 4096 signature nodes or 64 levels.");
            switch (type)
            {
                case TypeSignature.Primitive p when p.Code is PrimitiveTypeCode.Int32 or PrimitiveTypeCode.Object:
                    text.Append('p').Append((int)p.Code).Append(';'); break;
                case TypeSignature.Named n when n.Kind is SignatureTypeKind.Class or SignatureTypeKind.ValueType:
                    if (!genericDefinition && n.Definition.Assembly.Image.Reader.GetTypeDefinition(n.Definition.Handle).GetGenericParameters().Count != 0)
                        throw new ArgumentException("Open generic definitions are not closed type arguments.");
                    text.Append('n').Append((int)n.Kind).Append(':').Append(n.Definition.Assembly.ContentHash)
                        .Append(':').Append(MetadataTokens.GetToken(n.Definition.Handle)).Append(';'); break;
                case TypeSignature.Generic g:
                    if (g.Arguments.Count == 0 || g.Arguments.Count != g.Definition.Definition.Assembly.Image.Reader
                        .GetTypeDefinition(g.Definition.Definition.Handle).GetGenericParameters().Count)
                        throw new ArgumentException("Closed type argument count does not match its generic declaration.");
                    text.Append('g').Append(g.Arguments.Count).Append('['); Add(g.Definition, depth + 1, true);
                    foreach (var argument in g.Arguments) Add(argument, depth + 1);
                    text.Append(']'); break;
                case TypeSignature.Vector v:
                    text.Append('v'); Add(v.Element, depth + 1); break;
                default: throw new ArgumentException("Closed specialization requires supported concrete type arguments; open variables, modifiers, byrefs and pointers are unsupported.");
            }
        }
        text.Append("types:").Append(types.Count).Append('[');
        foreach (var type in types) Add(type, 0);
        text.Append("]methods:").Append(methods.Count).Append('[');
        foreach (var method in methods) Add(method, 0);
        text.Append(']');
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}

public sealed partial class AssemblyResolver
{
    /// <summary>Bind a call using the caller's closed environment, retaining declaration matching rules.</summary>
    public ResolutionResult<ResolvedMethod> ResolveClosedCall(ResolvedMethod caller, EntityHandle operand, MemberResolutionBudget budget)
    {
        var assembly = caller.DeclaringType.Definition.Assembly;
        return Bind(assembly, operand, () => Method(assembly, operand,
            new(caller.DeclaringType, caller.MethodArguments), declaredOnly: true, budget: budget));
    }

    public ResolutionResult<TypeSignature> ResolveClosedTypeOperand(ResolvedMethod caller, EntityHandle operand)
    {
        var assembly = caller.DeclaringType.Definition.Assembly;
        return Bind(assembly, operand, () => InContext(new SignatureDecoder(this, assembly).Type(operand),
            new(caller.DeclaringType, caller.MethodArguments)));
    }

    /// <summary>Read unconstrained closed method input. Bodies are snapshots, not permission to execute.</summary>
    public ResolutionResult<MethodInput> ReadClosedMethodInput(ResolvedMethod method, MethodReadLimits limits)
    {
        var assembly = method.DeclaringType.Definition.Assembly;
        var input = ReadMethodInput(assembly, method.Handle, limits);
        if (input.Value is not { } value) return input;
        return Bind(assembly, method.Handle, () =>
        {
            if (method.DeclaringType.Arguments.Count != 0 || value.OwnerGenericArity != 0)
                throw new UnsupportedBindingException("Generic declaring types require a later specialization gate.");
            if (value.Signature.GenericArity != method.MethodArguments.Count)
                throw new BadImageFormatException("Closed method argument count does not match its declaration.");
            var parameters = assembly.Image.Reader.GetMethodDefinition(method.Handle).GetGenericParameters();
            foreach (var handle in parameters)
            {
                var parameter = assembly.Image.Reader.GetGenericParameter(handle);
                if (parameter.Attributes != GenericParameterAttributes.None || parameter.GetConstraints().Count != 0)
                    throw new UnsupportedBindingException("Generic parameter constraints require validated constraint lowering; this gate accepts unconstrained methods only.");
            }
            string key;
            try { key = ClosedGenericKey.Create(method.DeclaringType.Arguments, method.MethodArguments); }
            catch (ArgumentException error) { throw new UnsupportedBindingException(error.Message); }
            return value with
            {
                Source = value.Source with { Specialization = key },
                Signature = Substitute(value.Signature, method.DeclaringType.Arguments, method.MethodArguments),
                MetadataMembersRead = checked(value.MetadataMembersRead + parameters.Count),
                Locals = [.. value.Locals.Select(t => Substitute(t, method.DeclaringType.Arguments, method.MethodArguments))],
            };
        });
    }
}
