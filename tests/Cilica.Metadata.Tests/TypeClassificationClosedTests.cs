using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class TypeClassificationClosedTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Closed_reference_receivers_do_not_retain_open_owner_layouts(bool optimized)
    {
        var result = Translate(130, false);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("ClosedOwners", result.Program!, (a, b) => unchecked(a + b))]);
    }
    [Theory]
    [InlineData(1, true)] [InlineData(257, false)]
    public void Actual_closed_layout_validation_and_type_limits_remain_enforced(int count, bool malformed)
    {
        var result = Translate(count, malformed);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => malformed ? d.Message.Contains("FIELD signature", StringComparison.Ordinal)
            : d.Id == DiagnosticIds.TranslationLimit && d.Message.Contains("limit", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Unused_generic_value_arguments_require_visibility_but_not_storage(bool optimized)
    {
        var result = TranslateUnusedArguments(257, false, false);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("UnusedArguments", result.Program!, (a, b) => unchecked(a * 257 + b))]);
    }
    [Theory]
    [InlineData(true, false)] [InlineData(false, true)]
    public void Hidden_arguments_and_executed_malformed_storage_remain_diagnostic(bool hidden, bool storage)
    {
        var result = TranslateUnusedArguments(1, hidden, storage);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains(hidden ? "Generic arguments require" : "FIELD signature", StringComparison.Ordinal));
    }
    private static ManagedExceptionTranslationResult TranslateUnusedArguments(int count, bool hidden, bool storage)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("ArgumentVisibilityBudget", new Version(1, 0, 0, 0)));
        var basis = b.TypeReference("System", "ValueType", b.Reference(library.Identity));
        var arguments = new List<TypeDefinitionHandle>();
        for (int i = 0; i < count; i++)
        {
            arguments.Add(b.Type("Test", "Payload" + i, baseType: basis,
                attributes: (hidden ? TypeAttributes.NotPublic : TypeAttributes.Public) | TypeAttributes.Sealed | TypeAttributes.SequentialLayout));
            if (hidden) b.Field("Number", 6, 8);
            else b.Field("UnusedMalformedField", 0);
        }
        b.Type("Test", "Helper");
        var echo = b.Method("Echo", [0x10, 1, 1, 8, 8], arity: 1,
            emit: il => { il.LoadArgument(0); il.OpCode(ILOpCode.Ret); });
        b.Type("Test", "Root");
        var calls = new List<MethodSpecificationHandle>();
        foreach (var type in arguments)
        {
            var signature = new BlobBuilder(); signature.WriteByte(0x0a); signature.WriteByte(1); signature.WriteByte(0x11);
            signature.WriteCompressedInteger(MetadataTokens.GetRowNumber(type) << 2);
            calls.Add(b.Instantiate(echo, signature.ToArray()));
        }
        var groups = new List<MethodDefinitionHandle>();
        for (int start = 0; start < count; start += 26)
        {
            int from = start, end = Math.Min(count, start + 26);
            groups.Add(b.Method("Group" + start, [0, 1, 8, 8], emit: il => {
                il.LoadConstantI4(0);
                for (int j = from; j < end; j++) { il.LoadArgument(0); il.Call(calls[j]); il.OpCode(ILOpCode.Add); }
                il.OpCode(ILOpCode.Ret);
            }));
        }
        var locals = new BlobBuilder(); locals.WriteByte(7); locals.WriteByte(1); locals.WriteByte(0x11);
        locals.WriteCompressedInteger(MetadataTokens.GetRowNumber(arguments[0]) << 2);
        var root = b.Method("Run", [0, 2, 8, 8, 8], locals: storage ? locals.ToArray() : null, emit: il => {
            il.LoadArgument(1);
            foreach (var group in groups) { il.LoadArgument(0); il.Call(group); il.OpCode(ILOpCode.Add); }
            il.OpCode(ILOpCode.Ret);
        });
        var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        return ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
    }
    private static ManagedExceptionTranslationResult Translate(int count, bool malformed)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("ClosedOwnerBudget", new Version(1, 0, 0, 0)));
        var parent = b.TypeReference("System", "Object", b.Reference(library.Identity));
        var baseConstructor = b.Member(parent, ".ctor", 0x20, 0, 1);
        var constructors = new List<MemberReferenceHandle>();
        for (int i = 0; i < count; i++)
        {
            var owner = b.Type("Test", "Owner" + i + "`1", baseType: parent, arity: 1,
                attributes: TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit);
            if (malformed) b.Field("Malformed", 0);
            b.Method(".ctor", [0x20, 0, 1], attributes: MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                emit: il => { il.LoadArgument(0); il.Call(baseConstructor); il.OpCode(ILOpCode.Ret); });
            var signature = new BlobBuilder(); signature.WriteByte(0x15); signature.WriteByte(0x12);
            signature.WriteCompressedInteger(MetadataTokens.GetRowNumber(owner) << 2); signature.WriteByte(1); signature.WriteByte(8);
            constructors.Add(b.Member(b.Specification(signature.ToArray()), ".ctor", 0x20, 0, 1));
        }
        b.Type("Test", "Root", baseType: parent);
        var root = b.Method("Run", [0, 2, 8, 8, 8], emit: il => {
            foreach (var constructor in constructors) { il.OpCode(ILOpCode.Newobj); il.Token(constructor); il.OpCode(ILOpCode.Pop); }
            il.LoadArgument(0); il.LoadArgument(1); il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Ret);
        });
        var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        return ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
    }
}
