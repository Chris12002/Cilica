using System;
using System.Linq;
using System.Collections.Immutable;
using System.Reflection;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.Resolution;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class InternalInterfaceTests
{
    private static readonly string[] Names = ["Internal", "Public", "Inherited", "ClassSlot", "Generic", "Null", "TypeChecks"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(InternalInterfaceProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Same_image_interfaces_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(InternalInterfaceProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }
    private static ManagedExceptionIrProgram RehomeEntry(ManagedExceptionIrProgram program)
    {
        var flow = program.Flow.Flow.Flow; var owner = flow.Types.References.ObjectBase with { Token = 0x0200ffff };
        var entry = flow.Methods.Single(m => IrMethodId.From(m.Source) == flow.Entry);
        var source = entry.Source with { ContentHash = owner.ContentHash, Token = 0x0600ffff, Specialization = "" };
        var id = IrMethodId.From(source); var binding = program.Flow.Bindings.Single(b => b.Method == flow.Entry);
        var movedBinding = binding with { Method = id, Owner = owner, OwnerExternallyVisible = true, OwnerAssemblyVisible = true, LexicalOwners = [] };
        return program with { Flow = program.Flow with {
            Bindings = program.Flow.Bindings.Replace(binding, movedBinding),
            Flow = program.Flow.Flow with { Flow = flow with { Entry = id, Methods = flow.Methods.Replace(entry, entry with { Source = source }) } } } };
    }
    [Fact]
    public async Task Detached_calls_require_same_image_for_internal_contracts_at_both_stages()
    {
        var programs = await Programs.Value;
        var publicCall = RehomeEntry(programs[1]);
        ManagedExceptionIrValidator.Validate(publicCall);
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(publicCall));
        Assert.Contains("expression, parameter or operand", Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(RehomeEntry(programs[6]))).Message, StringComparison.Ordinal);
        var internalCall = RehomeEntry(programs[0]);
        Assert.Contains("expression, parameter or operand", Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(internalCall)).Message, StringComparison.Ordinal);
        // Move the already-lowered caller independently to exercise the scalar gate.
        var scalar = ValueScalarLowering.LowerExceptions(programs[0]); var boxes = scalar.Flow.Flow; var objects = boxes.Flow.Flow; var flow = objects.Flow;
        var entry = flow.Methods.Single(m => IrMethodId.From(m.Source) == flow.Entry);
        var source = entry.Source with { ContentHash = flow.Types.ObjectBase.ContentHash, Token = 0x0600ffff, Specialization = "" }; var id = IrMethodId.From(source);
        var binding = objects.Bindings.Single(b => b.Method == flow.Entry);
        var moved = objects with { Bindings = objects.Bindings.Replace(binding, binding with { Method = id, Owner = flow.Types.ObjectBase with { Token = 0x0200ffff }, OwnerExternallyVisible = true, OwnerAssemblyVisible = true, LexicalOwners = [] }), Flow = flow with { Entry = id, Methods = flow.Methods.Replace(entry, entry with { Source = source }) } };
        var invalid = scalar with { Flow = scalar.Flow with { Flow = boxes with { Flow = boxes.Flow with { Flow = moved } } } };
        Assert.Contains("expression, parameter or operand", Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalid)).Message, StringComparison.Ordinal);
    }
    [Theory]
    [InlineData("visibility")] [InlineData("nested")] [InlineData("profile")]
    public async Task Detached_internal_catalogs_cannot_forge_visibility_or_use_older_profiles(string mutation)
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        var catalog = flow.Types.References; var contract = catalog.Interfaces.First(i => !i.ExternallyVisible);
        var changed = mutation switch {
            "visibility" => contract with { ExternallyVisible = true },
            "nested" => contract with { Attributes = (contract.Attributes & ~TypeAttributes.VisibilityMask) | TypeAttributes.NestedPrivate }, _ => contract };
        var invalid = catalog with { Integer64 = mutation != "profile", Interfaces = catalog.Interfaces.Replace(contract, changed) };
        Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(invalid));
        var plan = program.Flow.InterfaceDispatch!; var declaration = plan.Declarations.First(d => d.Owner == ObjectTypeId.From(contract.Source));
        Assert.Throws<ArgumentException>(() => ObjectInterfacePlanValidator.Validate(plan with { Declarations = plan.Declarations.Replace(declaration, declaration with { OwnerExternallyVisible = true }) }));
    }

    [Fact]
    public void Closed_metadata_mapping_matches_dotnet_and_retains_exact_work_bounds()
    {
        var implementation = typeof(LocalValueImplementation);
        var contract = implementation.Assembly.GetType("Cilica.Fixtures.Execution.ILocalValue")!;
        var map = implementation.GetInterfaceMap(contract);
        var declarationHandle = (MethodDefinitionHandle)MetadataTokens.EntityHandle(map.InterfaceMethods[0].MetadataToken);
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(implementation.Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var receiver = resolver.ResolveType(input, (TypeDefinitionHandle)MetadataTokens.EntityHandle(implementation.MetadataToken)).Value!;
        var declaration = resolver.ResolveMethod(input, declarationHandle).Value!; var budget = new MemberResolutionBudget();
        var result = resolver.ResolveClosedInterfaceMethodTarget(declaration, new(receiver, SignatureList<TypeSignature>.Empty), library, budget);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Equal(map.TargetMethods[0].MetadataToken, MetadataTokens.GetToken(result.Value!.Implementation.Handle));
        int used = 65536 - budget.Remaining;
        Assert.True(resolver.ResolveClosedInterfaceMethodTarget(declaration, new(receiver, SignatureList<TypeSignature>.Empty), library, new(used)).Success);
        Assert.False(resolver.ResolveClosedInterfaceMethodTarget(declaration, new(receiver, SignatureList<TypeSignature>.Empty), library, new(used - 1)).Success);
        Assert.False(resolver.ResolveInterfaceMethodTarget(input, declarationHandle, receiver).Success);
    }

    [Fact]
    public async Task Foreign_CIL_cannot_name_the_internal_contract()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var fixture = resolver.OpenInput(typeof(InternalInterfaceProbe).Assembly.Location).Value!;
        var builder = new ResolutionAssemblyBuilder(new("ForeignInternalInterface", new Version(1,0,0,0)));
        var library = builder.Reference(implementation.Identity); var imported = builder.Reference(fixture.Identity);
        var obj = builder.TypeReference("System", "Object", library);
        var owner = builder.TypeReference("Cilica.Fixtures.Execution", "LocalValueImplementation", imported);
        var contract = builder.TypeReference("Cilica.Fixtures.Execution", "ILocalValue", imported);
        var constructor = builder.Member(owner, ".ctor", 0x20, 0, 1); var read = builder.Member(contract, "Read", 0x20, 1, 8, 8);
        builder.Type("Test", "Driver", baseType: obj);
        var entry = builder.Method("Execute", [0,2,8,8,8], emit: il => {
            il.OpCode(ILOpCode.Newobj); il.Token(constructor); il.LoadArgument(0); il.OpCode(ILOpCode.Callvirt); il.Token(read); il.OpCode(ILOpCode.Ret); });
        var input = resolver.OpenInput(files.Write("foreign.dll", builder.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, entry, implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("lexical", StringComparison.OrdinalIgnoreCase));
    }

}
