using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Cilica.Metadata.Resolution;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class SealedReceiverTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(SealedReceiverProbe), "Calls", "Fields"));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Proven_sealed_virtual_receivers_retain_null_checks_and_override_results(bool optimized)
    {
        var programs = await Programs.Value;
        foreach (var program in programs)
        {
            var flow = program.Flow.Flow.Flow;
            Assert.DoesNotContain(flow.Methods, m => m.Source.Name == "System.Object::ToString");
            Assert.NotEmpty(program.Flow.Dispatch);
            Assert.All(program.Flow.Dispatch, m => Assert.Single(m.Targets));
            Assert.Contains(flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Statements),
                s => s is IrStatement.Define { Value.Expression: IrExpression.Call { NullCheckReceiver: true, VirtualDispatch: true } });
        }
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("Calls", programs[0], SealedReceiverProbe.Calls), ("Fields", programs[1], SealedReceiverProbe.Fields)]);
    }
    [Fact]
    public async Task Unproven_Object_receiver_cannot_be_narrowed_from_a_callers_allocation()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(SealedReceiverProbe).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(SealedReceiverProbe).GetMethod("UnprovenReceiver")!.MetadataToken), implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("no managed IL body", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Branch_entry_and_nonpublic_declarations_do_not_supply_a_receiver_proof(bool nonpublic)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var b = new ResolutionAssemblyBuilder(new("SealedProofBoundary", new Version(1, 0, 0, 0)));
        var objectType = b.TypeReference("System", "Object", b.Reference(implementation.Identity));
        var objectConstructor = b.Member(objectType, ".ctor", 0x20, 0, 1);
        const MethodAttributes constructorFlags = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
        EntityHandle parent = objectType, parentConstructor = objectConstructor;
        EntityHandle declaration;
        if (nonpublic)
        {
            parent = b.Type("Test", "Base", baseType: objectType);
            parentConstructor = b.Method(".ctor", [0x20, 0, 1], attributes: constructorFlags,
                emit: il => { il.LoadArgument(0); il.Call(objectConstructor); il.OpCode(ILOpCode.Ret); });
            declaration = b.Method("Read", [0x20, 0, 8], attributes: MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot,
                emit: il => { il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret); });
        }
        else declaration = b.Member(objectType, "ToString", 0x20, 0, 0x0e);
        b.Type("Test", "Sealed", baseType: parent, attributes: TypeAttributes.Public | TypeAttributes.Sealed);
        var constructor = b.Method(".ctor", [0x20, 0, 1], attributes: constructorFlags,
            emit: il => { il.LoadArgument(0); il.Call(parentConstructor); il.OpCode(ILOpCode.Ret); });
        b.Method(nonpublic ? "Read" : "ToString", [0x20, 0, nonpublic ? (byte)8 : (byte)0x0e],
            attributes: MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual,
            emit: il => { if (nonpublic) il.LoadConstantI4(2); else il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Ret); });
        b.Type("Test", "Caller", baseType: objectType);
        var root = b.Method("Run", [0, 1, 8, 8], emit: il => {
            var fallback = il.DefineLabel(); var call = il.DefineLabel();
            if (!nonpublic)
            {
                il.LoadArgument(0); il.Branch(ILOpCode.Brfalse, fallback);
                il.OpCode(ILOpCode.Newobj); il.Token(objectConstructor); il.Branch(ILOpCode.Br, call);
                il.MarkLabel(fallback);
            }
            il.OpCode(ILOpCode.Newobj); il.Token(constructor); il.MarkLabel(call);
            il.OpCode(ILOpCode.Callvirt); il.Token(declaration); il.OpCode(ILOpCode.Pop);
            il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        var input = scope.OpenInput(files.Write("sealed-proof.dll", b.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, root, implementation);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains(nonpublic ? "public nongeneric instance virtual" : "no managed IL body", StringComparison.Ordinal));
    }
}
