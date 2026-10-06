using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class BoxTypeTestRawTests
{
    private sealed record Probe(string Name, Type Target, bool Cast, bool Null = false);
    private static readonly Probe[] Probes = [
        new("IsIntIdentity", typeof(int), false), new("IsNullableIdentity", typeof(int?), false),
        new("CastIntIdentity", typeof(int), true), new("CastNullableIdentity", typeof(int?), true),
        new("CastIntNull", typeof(int), true, true), new("CastNullableNull", typeof(int?), true, true),
        new("CastEnumMismatch", typeof(BoxEnum), true), new("CastNullableEnumMismatch", typeof(BoxEnum?), true),
        new("CastUnsignedMismatch", typeof(uint), true), new("IsEnumMismatch", typeof(BoxEnum), false)];
    private static readonly Lazy<Task<Dictionary<string, ManagedExceptionTranslationResult>>> Results = new(Translate);
    private static string Message(Probe p) => "Unable to cast object of type 'System.Int32' to type '" + (Nullable.GetUnderlyingType(p.Target) ?? p.Target) + "'.";
    private static async Task<Dictionary<string, ManagedExceptionTranslationResult>> Translate()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var fixture = resolver.OpenInput(typeof(BoxEnum).Assembly.Location).Value!;
        var builder = new ResolutionAssemblyBuilder(new("RawBoxTypeTest", new Version(1, 0, 0, 0)));
        var core = builder.Reference(implementation.Identity);
        var integer = builder.TypeReference("System", "Int32", core);
        var unsigned = builder.TypeReference("System", "UInt32", core);
        var nullable = builder.TypeReference("System", "Nullable`1", core);
        var enumeration = builder.TypeReference("Cilica.Fixtures.Execution", "BoxEnum", builder.Reference(fixture.Identity));
        var exception = builder.TypeReference("System", "Exception", core);
        var invalidCast = builder.TypeReference("System", "InvalidCastException", core);
        var text = builder.TypeReference("System", "String", core);
        var getMessage = builder.Member(exception, "get_Message", 0x20, 0, 0x0e);
        var equals = builder.Member(text, "op_Equality", 0, 2, 2, 0x0e, 0x0e);
        EntityHandle Target(Type target)
        {
            if (target == typeof(int)) return integer;
            if (target == typeof(uint)) return unsigned;
            if (target == typeof(BoxEnum)) return enumeration;
            var signature = new BlobBuilder();
            signature.WriteByte(0x15); signature.WriteByte(0x11);
            signature.WriteCompressedInteger(CodedIndex.TypeDefOrRef(nullable)); signature.WriteByte(1);
            if (target == typeof(int?)) signature.WriteByte(8);
            else { signature.WriteByte(0x11); signature.WriteCompressedInteger(CodedIndex.TypeDefOrRef(enumeration)); }
            return builder.Specification(signature.ToArray());
        }
        builder.Type("Test", "Raw");
        var roots = Probes.ToDictionary(p => p.Name, p => builder.Method(p.Name, [0, 2, 8, 8, 8], locals: [7, 2, 8, 0x1c], emit: il => {
            if (p.Null) il.OpCode(ILOpCode.Ldnull);
            else { il.LoadArgument(0); il.OpCode(ILOpCode.Box); il.Token(integer); }
            il.StoreLocal(1);
            var start = il.DefineLabel(); var handler = il.DefineLabel(); var done = il.DefineLabel();
            il.MarkLabel(start); il.LoadLocal(1); il.LoadLocal(1);
            il.OpCode(p.Cast ? ILOpCode.Castclass : ILOpCode.Isinst); il.Token(Target(p.Target));
            il.OpCode(ILOpCode.Ceq); il.LoadConstantI4(10); il.OpCode(ILOpCode.Add); il.StoreLocal(0); il.Branch(ILOpCode.Leave, done);
            il.MarkLabel(handler); il.OpCode(ILOpCode.Callvirt); il.Token(getMessage);
            il.LoadString(builder.String(Message(p))); il.Call(equals); il.LoadConstantI4(20); il.OpCode(ILOpCode.Add); il.StoreLocal(0); il.Branch(ILOpCode.Leave, done);
            il.MarkLabel(done); il.LoadLocal(0); il.OpCode(ILOpCode.Ret);
            il.ControlFlowBuilder!.AddCatchRegion(start, handler, handler, done, invalidCast);
        }));
        // Even a test-only nullable operand must validate the declaration's struct constraint.
        var invalidNullable = new BlobBuilder(); invalidNullable.WriteByte(0x15); invalidNullable.WriteByte(0x11);
        invalidNullable.WriteCompressedInteger(CodedIndex.TypeDefOrRef(nullable)); invalidNullable.WriteByte(1); invalidNullable.WriteByte(0x1c);
        var malformed = builder.Specification(invalidNullable.ToArray());
        roots.Add("InvalidNullable", builder.Method("InvalidNullable", [0, 2, 8, 8, 8], emit: il => {
            il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Isinst); il.Token(malformed);
            il.OpCode(ILOpCode.Pop); il.LoadArgument(0); il.OpCode(ILOpCode.Ret);
        }));
        var missingArgument = new BlobBuilder(); missingArgument.WriteByte(0x15); missingArgument.WriteByte(0x11);
        missingArgument.WriteCompressedInteger(CodedIndex.TypeDefOrRef(nullable)); missingArgument.WriteByte(0);
        var emptyArguments = builder.Specification(missingArgument.ToArray());
        roots.Add("MissingArgument", builder.Method("MissingArgument", [0, 2, 8, 8, 8], emit: il => {
            il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Isinst); il.Token(emptyArguments);
            il.OpCode(ILOpCode.Pop); il.LoadArgument(0); il.OpCode(ILOpCode.Ret);
        }));
        var input = scope.OpenInput(files.Write("raw-tests.dll", builder.Build())).Value!;
        return roots.ToDictionary(p => p.Key, p => ValueTranslator.TranslateIntegerObjects(scope, input, p.Value, implementation));
    }
    // Authored IL only. The compiler never loads/executes its input assembly.
    private static Func<int, int, int> Oracle(Probe p)
    {
        var method = new DynamicMethod(p.Name, typeof(int), [typeof(int), typeof(int)]);
        var il = method.GetILGenerator(); il.DeclareLocal(typeof(int)); il.DeclareLocal(typeof(object));
        if (p.Null) il.Emit(OpCodes.Ldnull);
        else { il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Box, typeof(int)); }
        il.Emit(OpCodes.Stloc_1);
        var done = il.BeginExceptionBlock(); il.Emit(OpCodes.Ldloc_1); il.Emit(OpCodes.Ldloc_1);
        il.Emit(p.Cast ? OpCodes.Castclass : OpCodes.Isinst, p.Target); il.Emit(OpCodes.Ceq); il.Emit(OpCodes.Ldc_I4, 10); il.Emit(OpCodes.Add); il.Emit(OpCodes.Stloc_0);
        il.Emit(OpCodes.Leave, done); il.BeginCatchBlock(typeof(InvalidCastException));
        il.Emit(OpCodes.Callvirt, typeof(Exception).GetProperty(nameof(Exception.Message))!.GetMethod!);
        il.Emit(OpCodes.Ldstr, Message(p)); il.Emit(OpCodes.Call, typeof(string).GetMethod("op_Equality", [typeof(string), typeof(string)])!);
        il.Emit(OpCodes.Ldc_I4, 20); il.Emit(OpCodes.Add); il.Emit(OpCodes.Stloc_0); il.Emit(OpCodes.Leave, done); il.EndExceptionBlock();
        il.Emit(OpCodes.Ldloc_0); il.Emit(OpCodes.Ret);
        return method.CreateDelegate<Func<int, int, int>>();
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Raw_type_tests_preserve_identity_null_and_exact_cast_faults(bool optimized)
    {
        var results = await Results.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Probes.Select(p => {
            Assert.True(results[p.Name].Success, p.Name + ": " + string.Join("; ", results[p.Name].Diagnostics));
            return (p.Name, results[p.Name].Program!, Oracle(p));
        }).ToArray());
    }
    [Fact]
    public async Task Nullable_test_only_operands_validate_struct_constraints()
    {
        var result = (await Results.Value)["InvalidNullable"];
        Assert.False(result.Success);
        Assert.Contains("constraint", Assert.Single(result.Diagnostics).Message, StringComparison.OrdinalIgnoreCase);
        var missing = (await Results.Value)["MissingArgument"];
        Assert.False(missing.Success);
        Assert.Contains("argument", Assert.Single(missing.Diagnostics).Message, StringComparison.OrdinalIgnoreCase);
    }
}
