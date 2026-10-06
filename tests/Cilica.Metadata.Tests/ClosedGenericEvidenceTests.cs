using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ClosedGenericEvidenceTests
{
    [Fact]
    public void Detached_shapes_reproduce_closed_keys_after_disposing_metadata()
    {
        ClosedGenericEvidence evidence;
        string key;
        using (var files = new ResolverFiles())
        using (var resolver = files.Open())
        {
            var b = new ResolutionAssemblyBuilder(new("ClosedEvidence", new Version(1, 0, 0, 0)));
            b.Type("Test", "Item"); b.Type("Test", "Holder`1", arity: 1);
            var input = resolver.OpenInput(files.Write("evidence.dll", b.Build())).Value!;
            var item = new TypeSignature.Named(resolver.ResolveType(input, "Test", "Item").Value!, SignatureTypeKind.Class);
            var holder = new TypeSignature.Named(resolver.ResolveType(input, "Test", "Holder`1").Value!, SignatureTypeKind.Class);
            var closed = new TypeSignature.Generic(holder, new([item]));
            var types = new SignatureList<TypeSignature>([item, closed, new TypeSignature.Vector(closed)]);
            var methods = new SignatureList<TypeSignature>([new TypeSignature.Primitive(PrimitiveTypeCode.UInt64), item]);
            key = ClosedGenericKey.Create(types, methods);
            evidence = ClosedGenericEvidence.Capture(types, methods);
            Assert.Throws<ArgumentException>(() => ClosedGenericEvidence.Capture(new([holder]), SignatureList<TypeSignature>.Empty));
            Assert.Throws<ArgumentException>(() => ClosedGenericEvidence.Capture(new([new TypeSignature.Variable(false, 0)]), SignatureList<TypeSignature>.Empty));
            Assert.Throws<ArgumentException>(() => ClosedGenericEvidence.Capture(new([new TypeSignature.Generic(holder, new([item, item]))]), SignatureList<TypeSignature>.Empty));
        }
        Assert.Equal(key, evidence.Key(out int nodes)); Assert.Equal(10, nodes);
        Assert.NotEqual(key, (evidence with { Methods = evidence.Methods.Reverse().ToImmutableArray() }).Key());
        Assert.Equal("", new ClosedGenericEvidence([], []).Key());
    }

    [Theory]
    [InlineData("hash")] [InlineData("token")] [InlineData("kind")] [InlineData("primitive")]
    [InlineData("null")] [InlineData("generic")] [InlineData("default")]
    public void Detached_shapes_reject_malformed_identity_and_syntax(string defect)
    {
        var named = new ClosedTypeShape.Named(SignatureTypeKind.Class, new string('a', 64), 0x02000002);
        ClosedTypeShape type = defect switch {
            "hash" => named with { ContentHash = new string('A', 64) },
            "token" => named with { Token = 0x01000002 },
            "kind" => named with { Kind = SignatureTypeKind.Unknown },
            "primitive" => new ClosedTypeShape.Primitive(PrimitiveTypeCode.Double),
            "generic" => new ClosedTypeShape.Generic(named, []),
            "null" => null!, _ => named };
        var evidence = new ClosedGenericEvidence(defect == "default" ? default : [type], []);
        Assert.Throws<ArgumentException>(() => evidence.Key());
    }

    [Fact]
    public void Detached_node_and_depth_limits_precede_hashing()
    {
        var integer = new ClosedTypeShape.Primitive(PrimitiveTypeCode.Int32);
        var exact = new ClosedGenericEvidence([.. Enumerable.Repeat<ClosedTypeShape>(integer, 4096)], []);
        Assert.NotEmpty(exact.Key(out int nodes)); Assert.Equal(4096, nodes);
        Assert.Throws<ArgumentException>(() => (exact with { Methods = [integer] }).Key());
        ClosedTypeShape nested = integer;
        for (int i = 0; i < 64; i++) nested = new ClosedTypeShape.Vector(nested);
        Assert.NotEmpty(new ClosedGenericEvidence([nested], []).Key());
        Assert.Throws<ArgumentException>(() => new ClosedGenericEvidence([new ClosedTypeShape.Vector(nested)], []).Key());
    }
}
