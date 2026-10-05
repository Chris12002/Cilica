using System;
using System.Threading.Tasks;
using System.IO;
using System.Reflection.Metadata;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class GenericMemoryTests
{
    [Theory]
    [InlineData("References")] [InlineData("Integers")] [InlineData("Values")]
    public void Closed_memory_operands_retain_exact_storage(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(GenericMemory));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program!));
        if (name != "Values") Assert.False(IntegerObjectTranslationTests.Translate(name, enabled: false, fixture: typeof(GenericMemory)).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Closed_memory_copies_and_defaults_match_dotnet(bool optimized) => IntegerObjectTranslationTests.Verify(optimized,
        [("References", GenericMemory.References), ("Integers", GenericMemory.Integers), ("Values", GenericMemory.Values)], typeof(GenericMemory));

    [Fact]
    public void Typed_memory_rejects_a_different_borrow_element()
    {
        var result = ExplicitCopy(clear: false, mismatched: true);
        Assert.False(result.Success);
        Assert.Contains("exact element type", result.Diagnostics[0].Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Explicit_primitive_cpobj_and_initobj_match_native(bool optimized)
    {
        var copy = ExplicitCopy(false); var clear = ExplicitCopy(true);
        Assert.True(copy.Success, string.Join("; ", copy.Diagnostics));
        Assert.True(clear.Success, string.Join("; ", clear.Diagnostics));
        return IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("Copy", copy.Program!, (first, _) => first), ("Clear", clear.Program!, (_, _) => 0)]);
    }

    private static ManagedExceptionTranslationResult ExplicitCopy(bool clear, bool mismatched = false)
    {
        var builder = new ResolutionAssemblyBuilder(new("Memory" + clear + mismatched, new Version(1, 0, 0, 0)));
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var runtime = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var operand = builder.TypeReference("System", mismatched ? "Int32" : "Int64", builder.Reference(runtime.Identity));
        builder.Type("", "Probe");
        var root = builder.Method("Run", [0, 2, 8, 8, 8], locals: [7, 2, 10, 10], emit: il => {
            il.LoadArgument(0); il.OpCode(ILOpCode.Conv_i8); il.StoreLocal(0);
            il.LoadArgument(1); il.OpCode(ILOpCode.Conv_i8); il.StoreLocal(1);
            il.LoadLocalAddress(1);
            if (!clear) il.LoadLocalAddress(0);
            il.OpCode(clear ? ILOpCode.Initobj : ILOpCode.Cpobj); il.Token(operand);
            il.LoadLocal(1); il.OpCode(ILOpCode.Conv_i4); il.OpCode(ILOpCode.Ret);
        });
        var image = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        return ValueTranslator.TranslateIntegerObjects(resolver, image, root, runtime);
    }

    [Fact]
    public void Narrow_borrows_remain_diagnostic()
    {
        var result = IntegerObjectTranslationTests.Translate("Narrow", fixture: typeof(GenericMemory));
        Assert.False(result.Success);
        Assert.Contains("Narrow", result.Diagnostics[0].Message, StringComparison.Ordinal);
    }
}
