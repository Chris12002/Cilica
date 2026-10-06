using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.Resolution;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ProtectedConstructorTests
{
    private static readonly string[] Names = ["Flow", "Deep", "Generic", "Reference", "Intersection", "Union"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ProtectedConstructorProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    public async Task Protected_base_initialization_matches_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ProtectedConstructorProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }
    [Theory]
    [InlineData("receiver")] [InlineData("duplicate")] [InlineData("grandparent")] [InlineData("fault")]
    public async Task Base_initialization_proofs_are_retained_in_both_stages(string mutation)
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        IrMethod Mutate(IrMethod method, System.Collections.Immutable.ImmutableArray<ObjectMethodBinding> bindings)
        {
            var block = method.Blocks[0]; var call = block.Statements.OfType<IrStatement.Invoke>().First();
            var changed = mutation switch {
                "receiver" => call with { Arguments = call.Arguments.SetItem(0, call.Arguments[1]) },
                "grandparent" => call with { Target = bindings.Single(b => b.Kind == ObjectMethodKind.ObjectBaseConstructor).Method, Arguments = [call.Arguments[0]] },
                "fault" => call with { Exceptional = (IrExceptionTarget)99 }, _ => call };
            var statements = mutation == "duplicate" ? block.Statements.Add(call) : block.Statements.Replace(call, changed);
            return method with { Blocks = method.Blocks.SetItem(0, block with { Statements = statements }) };
        }
        var method = flow.Methods.Single(m => m.Source.Name.Contains("ProtectedLeaf::.ctor", StringComparison.Ordinal));
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Methods = flow.Methods.Replace(method, Mutate(method, program.Flow.Bindings)) } } } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program); var boxes = scalar.Flow.Flow; var objects = boxes.Flow.Flow; var lowered = objects.Flow;
        var body = lowered.Methods.Single(m => m.Source.Name.Contains("ProtectedLeaf::.ctor", StringComparison.Ordinal));
        var invalidScalar = scalar with { Flow = scalar.Flow with { Flow = boxes with { Flow = boxes.Flow with { Flow = objects with { Flow = lowered with { Methods = lowered.Methods.Replace(body, Mutate(body, objects.Bindings)) } } } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar));
    }
    [Theory]
    [InlineData(MethodAttributes.Family, "base", true)]
    [InlineData(MethodAttributes.FamORAssem, "base", true)]
    [InlineData(MethodAttributes.FamANDAssem, "base", false)]
    [InlineData(MethodAttributes.Private, "base", false)]
    [InlineData(MethodAttributes.Family, "allocate", false)]
    [InlineData(MethodAttributes.Family, "unrelated", false)]
    public async Task Foreign_constructor_access_is_narrowly_bound_to_the_immediate_base(MethodAttributes access, string shape, bool expected)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var baseImage = new ResolutionAssemblyBuilder(new("ProtectedBase", new Version(1,0,0,0)));
        var obj = baseImage.TypeReference("System", "Object", baseImage.Reference(implementation.Identity));
        var objCtor = baseImage.Member(obj, ".ctor", 0x20, 0, 1);
        baseImage.Type("Test", "Base", baseType: obj);
        var number = baseImage.Field("Number", 0x06, 8);
        const MethodAttributes flags = MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
        baseImage.Method(".ctor", [0x20,1,1,8], attributes: flags | access, emit: il => {
            il.LoadArgument(0); il.Call(objCtor); il.LoadArgument(0); il.LoadArgument(1); il.OpCode(ILOpCode.Stfld); il.Token(number); il.OpCode(ILOpCode.Ret); });
        var basePath = Path.Combine(Path.GetDirectoryName(built.AssemblyPath)!, "ProtectedBase.dll");
        await File.WriteAllBytesAsync(basePath, baseImage.Build(), TestContext.Current.CancellationToken);
        var source = resolver.OpenInput(basePath).Value!;
        var derivedImage = new ResolutionAssemblyBuilder(new("ProtectedDerived", new Version(1,0,0,0)));
        var parent = derivedImage.TypeReference("Test", "Base", derivedImage.Reference(source.Identity));
        var objectType = derivedImage.TypeReference("System", "Object", derivedImage.Reference(implementation.Identity));
        var constructor = derivedImage.Member(parent, ".ctor", 0x20, 1, 1, 8);
        var field = derivedImage.Member(parent, "Number", 0x06, 8);
        derivedImage.Type("Test", "Derived", baseType: shape == "unrelated" ? objectType : parent);
        var derivedCtor = derivedImage.Method(".ctor", [0x20,1,1,8], attributes: flags | MethodAttributes.Public, emit: il => {
            il.LoadArgument(0); il.LoadArgument(1); il.Call(constructor); il.OpCode(ILOpCode.Ret); });
        derivedImage.Type("Test", "Driver", baseType: objectType);
        var entry = derivedImage.Method("Execute", [0,2,8,8,8], emit: il => {
            il.LoadArgument(0); il.OpCode(ILOpCode.Newobj); il.Token(shape == "allocate" ? constructor : derivedCtor);
            il.OpCode(ILOpCode.Ldfld); il.Token(field); il.LoadArgument(1); il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Ret); });
        string path = files.Write("ProtectedDerived.dll", derivedImage.Build());
        // Resolver directories are snapshots; register both authored images before admission.
        string reference = LibraryBuildProducerTests.Request(files, CoreLibraryBuildTests.CorePath("CoreTypes.cs")).References.Single();
        using var fresh = AssemblyResolver.Create([Path.GetDirectoryName(built.AssemblyPath)!, Path.GetDirectoryName(reference)!]).Value!;
        ResolvedType Resolve(LibraryTypeIdentity identity) => fresh.ResolveType(fresh.ResolveAssembly(identity.Assembly).Value!, (TypeDefinitionHandle)MetadataTokens.EntityHandle(identity.Token)).Value!;
        var freshPlan = fresh.CreateLibraryBindingPlan(plan.Bindings.Select(b => new LibraryTypeMapping(Resolve(b.Contract), Resolve(b.Implementation)))).Value!;
        using var scope = fresh.CreateTrustedLibraryCompilationScope(freshPlan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = fresh.OpenInput(path).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, entry, fresh.ResolveAssembly(implementation.Identity).Value!);
        Assert.True(result.Success == expected, string.Join("; ", result.Diagnostics));
        if (expected) ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program!));
        else Assert.Contains(result.Diagnostics, d => d.Message.Contains("Calls require", StringComparison.Ordinal));
    }

}
