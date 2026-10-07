using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class FrameworkDelegateTests
{
    private static readonly string[] Names = ["FuncOne", "FuncTwo", "FuncReference", "FuncReferenceArguments", "FuncCapture", "ActionValue", "ActionReference", "PredicateInt", "PredicateReference", "ComparisonInt", "ComparisonReference", "NullFunc", "PredicateFault", "FuncBoolean", "Dispatcher"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(FrameworkDelegateProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Framework_callbacks_match_dotnet_with_forced_collection(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(FrameworkDelegateProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 5).Take(5).ToArray());
    }
    [Fact]
    public async Task Pinned_delegate_declarations_preserve_variance_and_exact_constructor_and_invoke_mappings()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var contract = resolver.ResolveAssembly(plan.Bindings[0].Contract.Assembly).Value!;
        using var sourceStream = File.OpenRead(LibraryBuildProducerTests.Request(files, CoreLibraryBuildTests.CorePath("CoreTypes.cs")).References.Single());
        using var targetStream = File.OpenRead(built.AssemblyPath);
        using var sourceImage = new PEReader(sourceStream); using var targetImage = new PEReader(targetStream);
        var sourceReader = sourceImage.GetMetadataReader(); var targetReader = targetImage.GetMetadataReader();
        foreach (string name in new[] { "Action`1", "Action`2", "Func`2", "Func`3", "Predicate`1", "Comparison`1" })
        {
            var source = resolver.ResolveType(contract, "System", name).Value!;
            var target = resolver.ResolveType(implementation, "System", name).Value!;
            var sourceDefinition = sourceReader.GetTypeDefinition(source.Handle); var targetDefinition = targetReader.GetTypeDefinition(target.Handle);
            Assert.Equal(sourceDefinition.GetGenericParameters().Select(h => sourceReader.GetGenericParameter(h).Attributes),
                targetDefinition.GetGenericParameters().Select(h => targetReader.GetGenericParameter(h).Attributes));
            var members = sourceDefinition.GetMethods().Where(h => sourceReader.GetString(sourceReader.GetMethodDefinition(h).Name) is ".ctor" or "Invoke").ToArray();
            Assert.Equal(2, members.Length);
            foreach (var member in members)
            {
                var mapped = resolver.ResolveLibraryMethod(plan, contract, member);
                Assert.True(mapped.Success, name + ": " + string.Join("; ", mapped.Diagnostics));
                Assert.Equal(target, mapped.Value!.Implementation.DeclaringType.Definition);
            }
        }
    }
    [Fact]
    public async Task Unsupported_signature_variance_and_multicast_paths_remain_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(FrameworkDelegateProbe).Assembly.Location).Value!;
        foreach (string suffix in new[] { "Variance", "Unsigned", "NarrowResult", "BooleanArgument", "Multicast" })
        {
            var method = typeof(FrameworkDelegateProbe).GetMethod("Unsupported" + suffix)!;
            var result = ValueTranslator.TranslateIntegerObjects(scope, input, (MethodDefinitionHandle)MetadataTokens.EntityHandle(method.MetadataToken), implementation);
            Assert.False(result.Success, suffix); Assert.Null(result.Program); Assert.NotEmpty(result.Diagnostics);
            if (suffix is "Unsigned" or "NarrowResult" or "BooleanArgument")
                Assert.Contains(result.Diagnostics, d => d.Message.Contains("Delegate requires", StringComparison.Ordinal));
        }
    }
    [Theory]
    [InlineData(2, 8, 8, 8)] // bool delegate / int target result
    [InlineData(8, 2, 8, 8)] // int delegate / bool target result
    [InlineData(8, 8, 8, 2)] // int delegate / bool target parameter
    [InlineData(8, 9, 8, 8)] // int delegate / uint target result
    [InlineData(10, 11, 8, 8)] // long delegate / ulong target result
    [InlineData(8, 8, 10, 11)] // long delegate / ulong target parameter
    public async Task Same_carrier_scalar_mismatches_are_rejected_before_lowering(int declaredResult, int targetResult, int declaredParameter, int targetParameter)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var builder = new ResolutionAssemblyBuilder(new("ScalarDelegateMismatch", new Version(1, 0, 0, 0)));
        var reference = builder.Reference(implementation.Identity);
        var obj = builder.TypeReference("System", "Object", reference);
        var multicast = builder.TypeReference("System", "MulticastDelegate", reference);
        builder.Type("Test", "Callback", baseType: multicast, attributes: TypeAttributes.Public | TypeAttributes.Sealed);
        var constructor = builder.Method(".ctor", [0x20, 2, 1, 0x1c, 0x18], attributes: MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
        var invoke = builder.Method("Invoke", [0x20, 1, (byte)declaredResult, (byte)declaredParameter], attributes: MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual);
        builder.Type("Test", "Driver", baseType: obj);
        var target = builder.Method("Target", [0, 1, (byte)targetResult, (byte)targetParameter], emit: il => { il.LoadArgument(0); il.OpCode(ILOpCode.Ret); });
        var entry = builder.Method("Execute", [0, 2, 8, 8, 8], emit: il => {
            il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Ldftn); il.Token(target);
            il.OpCode(ILOpCode.Newobj); il.Token(constructor); il.LoadArgument(0);
            il.OpCode(ILOpCode.Callvirt); il.Token(invoke); il.OpCode(ILOpCode.Ret); });
        var input = scope.OpenInput(files.Write("mismatch.dll", builder.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, entry, implementation);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Delegate scalar target signature", StringComparison.Ordinal));
    }
}
