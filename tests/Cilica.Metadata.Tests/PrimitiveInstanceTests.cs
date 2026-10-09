using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class PrimitiveInstanceTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram>> Fixture = new(() => CoreLibraryBuildTests.TranslateFixture(nameof(CoreLibraryProbe.IntegerHashes)));

    [Fact]
    public async Task Integer_hashes_use_four_exact_scalar_receivers_and_ordinary_managed_bodies()
    {
        var program = await Fixture.Value;
        var bindings = program.Flow.Bindings.Where(b => b.Kind == ObjectMethodKind.PrimitiveInstance).ToArray();
        Assert.Equal(4, bindings.Length);
        Assert.Equal(4, bindings.Select(b => b.Primitive!.Code).Distinct().Count());
        foreach (var binding in bindings)
        {
            Assert.Equal(program.Flow.Flow.Flow.Types.References.ObjectBase.ContentHash, binding.Owner.ContentHash);
            var method = program.Flow.Flow.Flow.Methods.Single(m => IrMethodId.From(m.Source) == binding.Method);
            Assert.True(binding.ReadOnlyReceiver);
            Assert.Equal(IrType.Location(binding.Primitive!.Storage, readOnly: true), method.Parameters[0]);
            Assert.Contains(method.Blocks.SelectMany(b => b.Values), v => v.Expression is IrExpression.LoadLocation);
        }
        var lowered = ValueScalarLowering.LowerExceptions(program);
        ScalarManagedExceptionIrValidator.Validate(lowered);
        Assert.All(lowered.Flow.Flow.Flow.Flow.Bindings, b => { Assert.Equal(ObjectMethodKind.Static, b.Kind); Assert.Null(b.Primitive); });
    }

    [Theory]
    [InlineData("missing")] [InlineData("name")] [InlineData("code")] [InlineData("token")] [InlineData("image")]
    [InlineData("duplicate-code")] [InlineData("width")] [InlineData("kind")] [InlineData("virtual-slot")] [InlineData("profile")]
    public async Task Primitive_receiver_evidence_cannot_be_forged_or_disagree_with_scalar_width(string corruption)
    {
        var program = await Fixture.Value; var flow = program.Flow.Flow.Flow;
        var binding = program.Flow.Bindings.First(b => b.Primitive?.Code == PrimitiveTypeCode.Int32);
        var changed = corruption switch {
            "missing" => binding with { Primitive = null },
            "name" => binding with { Primitive = binding.Primitive! with { Source = binding.Primitive.Source with { Name = "User.Int32" } } },
            "duplicate-code" => binding with { Primitive = binding.Primitive! with { Code = PrimitiveTypeCode.UInt32, Source = binding.Primitive.Source with { Name = "System.UInt32" } } },
            "code" => binding with { Primitive = binding.Primitive! with { Code = PrimitiveTypeCode.Double } },
            "token" => binding with { Primitive = binding.Primitive! with { Source = binding.Primitive.Source with { Token = binding.Owner.Token + 1 } } },
            "image" => binding with { Primitive = binding.Primitive! with { Source = binding.Primitive.Source with { ContentHash = new string('a', 64) } } },
            "kind" => binding with { Kind = ObjectMethodKind.Static, Attributes = MethodAttributes.Public | MethodAttributes.Static },
            "virtual-slot" => binding with { VirtualSlot = new(binding.Method, binding.Owner) },
            _ => binding,
        };
        if (corruption == "width") flow = flow with { Methods = [.. flow.Methods.Select(m => IrMethodId.From(m.Source) == binding.Method ? m with { Parameters = [IrType.Location(IrType.Int64)] } : m)] };
        if (corruption == "profile") flow = flow with { Types = flow.Types with { References = flow.Types.References with { Integer64 = false } } };
        program = program with { Flow = program.Flow with { Bindings = program.Flow.Bindings.Replace(binding, changed), Flow = new(flow) } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program));
    }
}
