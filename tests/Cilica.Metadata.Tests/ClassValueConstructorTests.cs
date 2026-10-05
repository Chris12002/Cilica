using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ClassValueConstructorTests
{
    [Theory]
    [InlineData("Flow")] [InlineData("Borrow")] [InlineData("BeforeBase")]
    public void Constructor_parameters_use_existing_value_and_borrow_storage(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(ClassValueConstructors));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program!));
        Assert.False(IntegerObjectTranslationTests.Translate(name, enabled: false, fixture: typeof(ClassValueConstructors)).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Native_constructor_values_borrows_and_fault_order_match_dotnet(bool optimized) => IntegerObjectTranslationTests.Verify(optimized,
        [("Flow", ClassValueConstructors.Flow), ("Borrow", ClassValueConstructors.Borrow), ("BeforeBase", ClassValueConstructors.BeforeBase)], typeof(ClassValueConstructors));

    [Theory]
    [InlineData("address-this")] [InlineData("missing-base")]
    public void Constructor_receiver_cannot_escape_before_base_initialization(string invalid)
    {
        var result = IntegerObjectTranslationTests.Translate("Flow", fixture: typeof(ClassValueConstructors));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = ValueScalarLowering.LowerExceptions(result.Program!);
        var boxes = program.Flow.Flow; var locations = boxes.Flow; var objects = locations.Flow;
        var binding = objects.Bindings.First(b => b.Kind == ObjectMethodKind.Constructor);
        var method = objects.Flow.Methods.Single(m => IrMethodId.From(m.Source) == binding.Method);
        var block = Assert.Single(method.Blocks);
        if (invalid == "address-this")
        {
            var statement = (IrStatement.Define)block.Statements[0];
            block = block with { Statements = block.Statements.SetItem(0, statement with { Value = statement.Value with {
                Type = IrType.Location(IrType.Reference(binding.Owner)), Expression = new IrExpression.AddressParameter(0) } }) };
        }
        else block = block with { Statements = [.. block.Statements.Where(s => s is not IrStatement.Invoke call
            || !objects.Bindings.Any(b => b.Method == call.Target && b.Kind is ObjectMethodKind.Constructor or ObjectMethodKind.ObjectBaseConstructor))] };
        var changed = method with { Blocks = [block] };
        objects = objects with { Flow = objects.Flow with { Methods = objects.Flow.Methods.Replace(method, changed) } };
        var tampered = program with { Flow = program.Flow with { Flow = boxes with { Flow = locations with { Flow = objects } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(tampered));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void A_constructor_receiver_address_requires_completed_base_initialization(bool initialized)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var runtime = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var builder = new ResolutionAssemblyBuilder(new("ConstructorAddress", new Version(1, 0, 0, 0)));
        var objectType = builder.TypeReference("System", "Object", builder.Reference(runtime.Identity));
        var objectConstructor = builder.Member(objectType, ".ctor", 0x20, 0, 1);
        builder.Type("", "Cell", baseType: objectType);
        var constructor = builder.Method(".ctor", [0x20, 0, 1], attributes: MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            emit: il => {
                if (initialized) { il.LoadArgument(0); il.Call(objectConstructor); }
                il.LoadArgumentAddress(0); il.OpCode(ILOpCode.Pop);
                if (!initialized) { il.LoadArgument(0); il.Call(objectConstructor); }
                il.OpCode(ILOpCode.Ret);
            });
        builder.Type("", "Probe");
        var root = builder.Method("Run", [0, 0, 8], emit: il => {
            il.OpCode(ILOpCode.Newobj); il.Token(constructor); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        var image = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver, image, root, runtime);
        Assert.Equal(initialized, result.Success);
        if (!initialized) Assert.Contains("uninitialized constructor receiver", result.Diagnostics[0].Message, StringComparison.Ordinal);
    }
}
