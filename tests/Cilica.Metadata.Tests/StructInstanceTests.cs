using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class StructInstanceTests
{
    [Theory]
    [InlineData("Flow")] [InlineData("Borrow")] [InlineData("Generic")] [InlineData("Empty")]
    public void Struct_instance_receiver_uses_an_exact_checked_borrow(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(StructInstance));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Contains(result.Program!.Flow.Bindings, b => b.Kind == ObjectMethodKind.ValueInstance);
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program));
        Assert.False(IntegerObjectTranslationTests.Translate(name, enabled: false, fixture: typeof(StructInstance)).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Struct_instance_mutation_copies_aliases_and_faults_match_dotnet(bool optimized) => IntegerObjectTranslationTests.Verify(optimized,
        [("Flow", StructInstance.Flow), ("Borrow", StructInstance.Borrow), ("Generic", StructInstance.Generic), ("Empty", StructInstance.Empty)], typeof(StructInstance));

    [Theory]
    [InlineData("reference")] [InlineData("readonly")] [InlineData("constructor")]
    public void Detached_struct_instance_bindings_preserve_receiver_contract(string invalid)
    {
        var result = IntegerObjectTranslationTests.Translate("Flow", fixture: typeof(StructInstance));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!;
        var locations = program.Flow.Flow; var flow = locations.Flow;
        var binding = program.Flow.Bindings.First(b => b.Kind == ObjectMethodKind.ValueInstance);
        var method = flow.Methods.Single(m => IrMethodId.From(m.Source) == binding.Method);
        if (invalid == "constructor")
            program = program with { Flow = program.Flow with { Bindings = program.Flow.Bindings.Replace(binding, binding with { Kind = ObjectMethodKind.ValueConstructor }) } };
        else
        {
            var changed = method with { Parameters = method.Parameters.SetItem(0, invalid == "reference" ? IrType.Reference(binding.Owner) : IrType.Location(IrType.Value(binding.Owner), true)) };
            program = program with { Flow = program.Flow with { Flow = locations with { Flow = flow with { Methods = flow.Methods.Replace(method, changed) } } } };
        }
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program));
    }
}
