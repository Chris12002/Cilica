using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ManagedExceptionTranslationTests
{
    internal static ManagedExceptionTranslationResult Translate(string name, Cilica.Metadata.IL.MethodReadLimits? limits = null)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(ManagedExceptionProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var entry = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ManagedExceptionProbe).GetMethod(name)!.MetadataToken);
        return ValueTranslator.TranslateExceptions(resolver, input, entry, library, limits);
    }
    [Theory]
    [InlineData("TypedCatch")]
    [InlineData("Cleanup")]
    [InlineData("TypeFilter")]
    [InlineData("Rethrow")]
    [InlineData("NestedCleanup")]
    [InlineData("Replacement")]
    [InlineData("Composed")]
    [InlineData("Message")]
    [InlineData("OrderedCatches")]
    [InlineData("RuntimeFaults")]
    [InlineData("GenericCastMessage")]
    [InlineData("AllocationFailure")]
    [InlineData("Unhandled")]
    public void CIL_regions_lower_to_checked_native_exception_IR(string name)
    {
        var result = Translate(name); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!;
        Assert.Contains(program.Flow.Flow.Flow.Methods, m => m.ExceptionRegions.Length != 0);
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(program));
    }
    [Theory]
    [InlineData("InvalidConstructor", "forward")]
    [InlineData("InvalidOverride", "virtual")]
    public void Unsupported_custom_exception_behavior_is_not_silently_skipped(string name, string diagnostic)
    {
        var result = Translate(name); Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains(diagnostic, StringComparison.Ordinal));
    }
    [Theory]
    [InlineData("instructions")]
    [InlineData("bytes")]
    [InlineData("metadata")]
    public void Exception_translation_preserves_bounded_read_failures(string kind)
    {
        var limits = new Cilica.Metadata.IL.MethodReadLimits();
        limits = kind switch { "instructions" => limits with { MaximumInstructions = 1 },
            "bytes" => limits with { MaximumBodyBytes = 1 }, _ => limits with { MaximumInitializerMembers = 1 } };
        var result = Translate("Composed", limits); Assert.False(result.Success); Assert.Null(result.Program); Assert.NotEmpty(result.Diagnostics);
    }
    [Fact]
    public void Earlier_delegate_profile_does_not_admit_exception_bodies()
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(ManagedExceptionProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var entry = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ManagedExceptionProbe).GetMethod("TypedCatch")!.MetadataToken);
        Assert.False(ValueTranslator.TranslateDelegates(resolver, input, entry, library).Success);
    }
    [Fact]
    public void Stateful_filters_remain_actionable_diagnostics()
    {
        var result = Translate("GeneralFilter"); Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("search-before-unwind", StringComparison.Ordinal));
    }
}
