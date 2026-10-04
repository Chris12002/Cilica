using System;
using System.IO;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ManagedExceptionInputTests
{
    internal static ResolutionResult<ManagedExceptionMethodInput> Read(string name)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(ManagedExceptionProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var handle = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ManagedExceptionProbe).GetMethod(name)!.MetadataToken);
        var method = resolver.ResolveDeclaredMethod(input, handle, new()).Value!;
        return resolver.ReadManagedExceptionMethodInput(method, library);
    }
    [Theory]
    [InlineData("TypedCatch", 1)]
    [InlineData("Cleanup", 2)]
    [InlineData("TypeFilter", 1)]
    [InlineData("Rethrow", 2)]
    public void Managed_method_snapshots_preserve_validated_regions(string name, int count)
    {
        var result = Read(name); Assert.True(result.Value is not null, string.Join("; ", result.Diagnostics));
        Assert.Equal(count, result.Value!.Regions.Length);
        Assert.All(result.Value.Regions, region =>
        {
            Assert.True(region.TryStart < region.TryEnd && region.HandlerStart < region.HandlerEnd);
            Assert.True(region.TryEnd <= result.Value.Method.CodeSize && region.HandlerEnd <= result.Value.Method.CodeSize);
            if (region.Kind == ExceptionRegionKind.Catch) Assert.NotNull(region.CatchType);
            if (region.Kind == ExceptionRegionKind.Filter) { Assert.Equal("type-test", region.FilterKind); Assert.NotNull(region.FilterStart); }
        });
    }
    [Fact]
    public void State_reading_filters_remain_explicit_diagnostics()
    {
        var result = Read("GeneralFilter"); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("search-before-unwind", StringComparison.Ordinal));
    }
    [Fact]
    public void Region_analysis_honors_exact_metadata_work_budget()
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(ManagedExceptionProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var handle = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ManagedExceptionProbe).GetMethod("TypeFilter")!.MetadataToken);
        var method = resolver.ResolveDeclaredMethod(input, handle, new()).Value!;
        var baseline = resolver.ReadManagedExceptionMethodInput(method, library);
        Assert.True(baseline.Success, string.Join("; ", baseline.Diagnostics));
        int work = baseline.Value!.Method.MetadataMembersRead;
        Assert.True(resolver.ReadManagedExceptionMethodInput(method, library, new(MaximumInitializerMembers: work)).Success);
        var insufficient = resolver.ReadManagedExceptionMethodInput(method, library, new(MaximumInitializerMembers: work - 1));
        Assert.False(insufficient.Success);
        Assert.Contains(insufficient.Diagnostics, d => d.Id == DiagnosticIds.TranslationLimit);
    }
    [Fact]
    public void Ordinary_branches_cannot_enter_a_catch_handler()
    {
        var builder = new ResolutionAssemblyBuilder(new("InvalidCatch", new Version(1, 0, 0, 0)));
        var obj = builder.Type("System", "Object");
        var exception = builder.Type("System", "Exception", baseType: obj);
        builder.Type("Test", "Driver", baseType: obj);
        var entry = builder.Method("Execute", [0, 0, 1], emit: il =>
        {
            var start = il.DefineLabel(); var handler = il.DefineLabel(); var done = il.DefineLabel();
            il.MarkLabel(start); il.OpCode(ILOpCode.Ldnull); il.Branch(ILOpCode.Br_s, handler);
            il.MarkLabel(handler); il.OpCode(ILOpCode.Pop); il.Branch(ILOpCode.Leave_s, done);
            il.MarkLabel(done); il.OpCode(ILOpCode.Ret);
            il.ControlFlowBuilder!.AddCatchRegion(start, handler, handler, done, exception);
        });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var input = resolver.OpenInput(files.Write("catch.dll", builder.Build())).Value!;
        var method = resolver.ResolveDeclaredMethod(input, entry, new()).Value!;
        var result = resolver.ReadManagedExceptionMethodInput(method, input);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.InvalidControlTransfer);
    }

}
