using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class AssemblyFieldTests
{
    private static AssemblyIdentity RuntimeIdentity()
    {
        var name = typeof(object).Assembly.GetName();
        return new(name.Name!, name.Version!, publicKeyToken: Convert.ToHexStringLower(name.GetPublicKeyToken()!));
    }
    private static readonly string[] Names = ["Numbers", "References", "Values", "Family", "Nested", "PrivateAncestor", "FaultOrder"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(AssemblyFieldProbe), Names));
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Same_assembly_instance_storage_and_fault_order_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) => (name, programs[i], typeof(AssemblyFieldProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).ToArray());
    }
    [Theory]
    [InlineData(FieldAttributes.Assembly, true)] [InlineData(FieldAttributes.FamORAssem, true)]
    [InlineData(FieldAttributes.Private, false)] [InlineData(FieldAttributes.Family, false)]
    [InlineData(FieldAttributes.FamANDAssem, false)] [InlineData(FieldAttributes.PrivateScope, false)]
    public void Access_flags_agree_for_read_write_and_borrow(FieldAttributes access, bool expected)
    {
        foreach (string operation in new[] { "read", "write", "borrow" })
        {
            var b = new ResolutionAssemblyBuilder(new("InstanceFieldFlags", new Version(1, 0, 0, 0)));
            var obj = b.TypeReference("System", "Object", b.Reference(RuntimeIdentity()));
            b.Type("Test", "Holder", baseType: obj, attributes: TypeAttributes.NotPublic);
            var field = b.FieldWithAttributes("Value", access, 6, 8);
            b.Type("Test", "Caller");
            var root = b.Method("Run", [0, 0, 8], emit: il => {
                il.OpCode(ILOpCode.Ldnull);
                if (operation == "write") { il.OpCode(ILOpCode.Ldc_i4_1); il.OpCode(ILOpCode.Stfld); il.Token(field); il.OpCode(ILOpCode.Ldc_i4_0); }
                else { il.OpCode(operation == "read" ? ILOpCode.Ldfld : ILOpCode.Ldflda); il.Token(field); if (operation == "borrow") il.OpCode(ILOpCode.Ldind_i4); }
                il.OpCode(ILOpCode.Ret);
            });
            using var files = new ResolverFiles(); using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
            var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!; var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
            var result = ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
            Assert.True(result.Success == expected, string.Join("; ", result.Diagnostics));
            if (expected) ValueScalarLowering.LowerExceptions(result.Program!);
            else Assert.Contains(result.Diagnostics, d => d.Message.Contains("Instance field access", StringComparison.Ordinal));
            var legacy = ValueTranslator.TranslateExceptions(resolver, input, root, library);
            Assert.False(legacy.Success);
        }
    }
    [Theory] [InlineData(FieldAttributes.Assembly)] [InlineData(FieldAttributes.FamORAssem)]
    public void Foreign_assembly_fields_remain_inaccessible(FieldAttributes access)
    {
        using var files = new ResolverFiles();
        var identity = new AssemblyIdentity("ForeignInstanceFields", new Version(1, 0, 0, 0)); var target = new ResolutionAssemblyBuilder(identity);
        var obj = target.TypeReference("System", "Object", target.Reference(RuntimeIdentity()));
        target.Type("Test", "Holder", baseType: obj); target.FieldWithAttributes("Value", access, 6, 8); files.Write("ForeignInstanceFields.dll", target.Build());
        var b = new ResolutionAssemblyBuilder(new("ForeignInstanceCaller", new Version(1, 0, 0, 0)));
        var owner = b.TypeReference("Test", "Holder", b.Reference(identity)); var field = b.Member(owner, "Value", 6, 8);
        b.Type("Test", "Caller"); var root = b.Method("Run", [0, 0, 8], emit: il => { il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Ldfld); il.Token(field); il.OpCode(ILOpCode.Ret); });
        using var resolver = AssemblyResolver.Create([files.DirectoryPath("refs"), Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!; var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("Instance field access", StringComparison.Ordinal));
    }
    [Theory] [InlineData(FieldAttributes.Private)] [InlineData(FieldAttributes.Family)]
    [InlineData(FieldAttributes.FamANDAssem)] [InlineData(FieldAttributes.PrivateScope)]
    [InlineData(FieldAttributes.Assembly | FieldAttributes.InitOnly)]
    public async Task Detached_field_flags_cannot_bypass_access_or_readonly_checks(FieldAttributes attributes)
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        var owner = flow.Types.References.Types.Single(t => t.Source.Name.Contains("AssemblyFieldHolder", StringComparison.Ordinal));
        var field = owner.Fields.Single(f => f.Name == "Number");
        var changed = owner with { Fields = owner.Fields.Replace(field, field with { Attributes = attributes }) };
        var types = flow.Types with { References = flow.Types.References with { Types = flow.Types.References.Types.Replace(owner, changed) } };
        var forged = program with { Flow = program.Flow with { Flow = new(flow with { Types = types }) } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(forged));
        var scalar = ValueScalarLowering.LowerExceptions(program); var boxes = scalar.Flow.Flow; var objects = boxes.Flow.Flow;
        var scalarOwner = objects.Flow.Types.Types.Single(t => t.Source.Name.Contains("AssemblyFieldHolder", StringComparison.Ordinal));
        var scalarField = scalarOwner.Fields.Single(f => f.Name == "Number");
        var changedScalarOwner = scalarOwner with { Fields = scalarOwner.Fields.Replace(scalarField, scalarField with { Attributes = attributes }) };
        var changedObjects = objects with { Flow = objects.Flow with { Types = objects.Flow.Types with { Types = objects.Flow.Types.Types.Replace(scalarOwner, changedScalarOwner) } } };
        var forgedScalar = scalar with { Flow = scalar.Flow with { Flow = boxes with { Flow = boxes.Flow with { Flow = changedObjects } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(forgedScalar));
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task Both_ir_stages_require_complete_lexical_facts(bool caller)
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        var owner = flow.Types.References.Types.Single(t => t.Source.Name.Contains("AssemblyFieldHolder", StringComparison.Ordinal));
        var types = flow.Types with { References = flow.Types.References with { Types = flow.Types.References.Types.Replace(owner, owner with { LexicalOwners = [] }) } };
        var forged = caller ? program with { Flow = program.Flow with { Bindings = [.. program.Flow.Bindings.Select(b => b with { LexicalOwners = [] })] } }
            : program with { Flow = program.Flow with { Flow = new(flow with { Types = types }) } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(forged));
        var scalar = ValueScalarLowering.LowerExceptions(program); var boxes = scalar.Flow.Flow; var objects = boxes.Flow.Flow;
        var scalarOwner = objects.Flow.Types.Types.Single(t => t.Source.Name.Contains("AssemblyFieldHolder", StringComparison.Ordinal));
        var changedObjects = caller ? objects with { Bindings = [.. objects.Bindings.Select(b => b with { LexicalOwners = [] })] }
            : objects with { Flow = objects.Flow with { Types = objects.Flow.Types with { Types = objects.Flow.Types.Types.Replace(scalarOwner, scalarOwner with { LexicalOwners = [] }) } } };
        var forgedScalar = scalar with { Flow = scalar.Flow with { Flow = boxes with { Flow = boxes.Flow with { Flow = changedObjects } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(forgedScalar));
    }
}
