using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Cilica.Compiler;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class StringMetadataBoundaryTests
{
    private static (ResolutionAssemblyBuilder Builder, TypeDefinitionHandle Text) Core(string name)
    {
        var b = new ResolutionAssemblyBuilder(new(name, new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object");
        var text = b.Type("System", "String", baseType: obj, attributes: TypeAttributes.Public | TypeAttributes.Sealed);
        return (b, text);
    }

    private static ObjectTranslationResult Translate(ResolutionAssemblyBuilder builder, MethodDefinitionHandle entry)
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("strings.dll", builder.Build())).Value!;
        return ObjectTranslator.Translate(resolver, image, entry, image);
    }

    [Fact]
    public void Equal_names_and_tokens_in_other_images_do_not_become_string_intrinsics()
    {
        var (core, _) = Core("SelectedStringCore");
        core.Method("get_Length", [0x20, 0, 8], attributes: MethodAttributes.Public);
        var app = new ResolutionAssemblyBuilder(new("StringLookalike", new Version(1, 0, 0, 0)));
        var reference = app.Reference(new("SelectedStringCore", new Version(1, 0, 0, 0)));
        var obj = app.TypeReference("System", "Object", reference);
        app.Type("Test", "Padding", baseType: obj);
        var other = app.Type("System", "String", baseType: obj);
        Assert.Equal(0x02000003, MetadataTokens.GetToken(other));
        var fakeConcat = app.Method("Concat", [0, 2, 0x0e, 0x0e, 0x0e], emit: il => { il.LoadArgument(0); il.OpCode(ILOpCode.Ret); });
        var selected = app.TypeReference("System", "String", reference);
        var getLength = app.Member(selected, "get_Length", 0x20, 0, 8);
        app.Type("Test", "Driver", baseType: obj);
        var literal = app.String("x");
        var entry = app.Method("Run", [0, 0, 8], emit: il =>
        { il.LoadString(literal); il.LoadString(literal); il.Call(fakeConcat); il.OpCode(ILOpCode.Callvirt); il.Token(getLength); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles();
        var corePath = files.Write("core.dll", core.Build());
        using var resolver = files.Open("refs");
        var runtime = resolver.OpenInput(corePath).Value!;
        var image = resolver.OpenInput(files.Write("app.dll", app.Build())).Value!;
        var result = ObjectTranslator.Translate(resolver, image, entry, runtime);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Equal(2, result.Program!.Flow.Methods.Length);
        Assert.DoesNotContain(result.Program.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values), v => v.Expression is IrExpression.StringConcat);
        Assert.Equal(1, new ObjectTranslationConformanceTests.Evaluator(result.Program).Run());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Selected_string_shell_requires_sealing_and_the_selected_object_parent(bool wrongParent)
    {
        var b = new ResolutionAssemblyBuilder(new("StringShell", new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object"); var parent = b.Type("Test", "Parent", baseType: obj);
        var text = b.Type("System", "String", baseType: wrongParent ? parent : obj,
            attributes: TypeAttributes.Public | (wrongParent ? TypeAttributes.Sealed : 0));
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("shell.dll", b.Build())).Value!;
        var result = ObjectModelBuilder.Build(resolver, resolver.ResolveType(image, text).Value!, image);
        Assert.False(result.Success); Assert.Equal(DiagnosticIds.ExecutionUnsupported, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void Missing_selected_string_does_not_break_primitive_only_translation()
    {
        var b = new ResolutionAssemblyBuilder(new("NoString", new Version(1, 0, 0, 0)));
        b.Type("System", "Object"); b.Type("Test", "Driver", baseType: MetadataTokens.TypeDefinitionHandle(2));
        var plain = b.Method("Plain", [0, 0, 8], emit: il => { il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret); });
        var literal = b.String("missing");
        var text = b.Method("Text", [0, 0, 8], emit: il => { il.LoadString(literal); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("no-string.dll", b.Build())).Value!;
        Assert.True(ObjectTranslator.Translate(resolver, image, plain, image).Success);
        var result = ObjectTranslator.Translate(resolver, image, text, image);
        Assert.False(result.Success); Assert.Contains("IL_", Assert.Single(result.Diagnostics).Location, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Intrinsic_string_cannot_hide_type_or_module_initialization(bool module)
    {
        var b = new ResolutionAssemblyBuilder(new("StringInitialization", new Version(1, 0, 0, 0)));
        void Initializer() => b.Method(".cctor", [0, 0, 1],
            attributes: MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            emit: il => il.OpCode(ILOpCode.Ret));
        if (module) Initializer();
        var obj = b.Type("System", "Object");
        var text = b.Type("System", "String", baseType: obj, attributes: TypeAttributes.Public | TypeAttributes.Sealed);
        if (!module) Initializer();
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("initialization.dll", b.Build())).Value!;
        var result = resolver.ResolveIntrinsicStringType(image, new());
        Assert.False(result.Success); Assert.Contains("initialization", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ProgramLimits.MaximumMethods - 2, true)]
    [InlineData(ProgramLimits.MaximumMethods - 1, false)]
    public void Intrinsic_declarations_share_the_executable_method_limit(int helpers, bool valid)
    {
        var (b, _) = Core("StringMethodBudget");
        var length = b.Method("get_Length", [0x20, 0, 8], attributes: MethodAttributes.Public);
        var methods = Enumerable.Range(0, helpers).Select(i =>
        {
            b.Type("Test", "Helper" + i, baseType: MetadataTokens.TypeDefinitionHandle(2));
            return b.Method("Get", [0, 0, 8], emit: il => { il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret); });
        }).ToArray();
        b.Type("Test", "Driver", baseType: MetadataTokens.TypeDefinitionHandle(2)); var literal = b.String("");
        var root = b.Method("Run", [0, 0, 8], emit: il =>
        {
            foreach (var method in methods) { il.Call(method); il.OpCode(ILOpCode.Pop); }
            il.LoadString(literal); il.OpCode(ILOpCode.Callvirt); il.Token(length); il.OpCode(ILOpCode.Ret);
        });
        var result = Translate(b, root); Assert.Equal(valid, result.Success);
        if (!valid) Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(2, false)]
    public void String_shell_initialization_checks_use_the_supplied_metadata_budget(int budget, bool valid)
    {
        var (b, _) = Core("StringShellBudget");
        b.Method("First", [0, 0, 8]); b.Method("Second", [0, 0, 8]);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("budget.dll", b.Build())).Value!;
        var result = resolver.ResolveIntrinsicStringType(image, new(budget)); Assert.Equal(valid, result.Success);
        if (!valid) Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void Forwarded_selected_String_and_String_only_locals_keep_the_intrinsic_identity()
    {
        var (core, _) = Core("ForwardedStringCore");
        var facade = new ResolutionAssemblyBuilder(new("StringFacade", new Version(1, 0, 0, 0)));
        var reference = facade.Reference(new("ForwardedStringCore", new Version(1, 0, 0, 0)));
        facade.Forward("System", "Object", reference); facade.Forward("System", "String", reference);
        var app = new ResolutionAssemblyBuilder(new("StringLocal", new Version(1, 0, 0, 0)));
        var appReference = app.Reference(new("StringFacade", new Version(1, 0, 0, 0)));
        var obj = app.TypeReference("System", "Object", appReference);
        app.Type("Test", "Driver", baseType: obj);
        var root = app.Method("Run", [0, 0, 8], locals: [7, 1, 0x0e], emit: il => { il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles();
        var corePath = files.Write("core.dll", core.Build()); var facadePath = files.Write("facade.dll", facade.Build());
        var appPath = files.Write("app.dll", app.Build());
        using var resolver = files.Open("refs");
        var image = resolver.OpenInput(appPath).Value!; var runtime = resolver.OpenInput(facadePath).Value!;
        var result = ObjectTranslator.Translate(resolver, image, root, runtime);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Equal(resolver.OpenInput(corePath).Value!.ContentHash, result.Program!.Flow.Types.String!.ContentHash);
        Assert.NotEmpty(ObjectCppEmitter.EmitProgram(result.Program).Text);
    }

    [Theory]
    [InlineData(65536, 1, true)]
    [InlineData(65537, 1, false)]
    [InlineData(65536, 4, true)]
    [InlineData(65536, 5, false)]
    public void Literal_occurrences_share_the_unit_budget_across_methods(int length, int copies, bool valid)
    {
        var (b, _) = Core("LiteralBudget");
        b.Type("Test", "Driver", baseType: MetadataTokens.TypeDefinitionHandle(2));
        var handle = b.String(new string('\ud800', length));
        var helpers = Enumerable.Range(0, copies).Select(i => b.Method("Get" + i, [0, 0, 0x0e], emit: il =>
        { il.LoadString(handle); il.OpCode(ILOpCode.Ret); })).ToArray();
        var root = b.Method("Run", [0, 0, 8], emit: il =>
        { foreach (var helper in helpers) { il.Call(helper); il.OpCode(ILOpCode.Pop); } il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret); });
        var result = Translate(b, root); Assert.Equal(valid, result.Success);
        if (!valid) { var d = Assert.Single(result.Diagnostics); Assert.Equal(DiagnosticIds.TranslationLimit, d.Id); Assert.Contains("IL_", d.Location, StringComparison.Ordinal); }
    }

    [Theory]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void Distinct_literals_are_bounded_without_counting_inference_revisits(int count, bool valid)
    {
        var (b, _) = Core("DistinctLiterals");
        b.Type("Test", "Driver", baseType: MetadataTokens.TypeDefinitionHandle(2));
        var literals = Enumerable.Range(0, count).Select(i => b.String(i.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray();
        var root = b.Method("Run", [0, 1, 8, 8], emit: il =>
        {
            var next = il.DefineLabel(); var join = il.DefineLabel();
            il.LoadArgument(0); il.Branch(ILOpCode.Brtrue, next);
            il.LoadString(literals[0]); il.Branch(ILOpCode.Br, join);
            il.MarkLabel(next); il.OpCode(ILOpCode.Ldnull); il.Branch(ILOpCode.Br, join);
            il.MarkLabel(join); il.OpCode(ILOpCode.Pop);
            foreach (var literal in literals) { il.LoadString(literal); il.OpCode(ILOpCode.Pop); }
            il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret);
        });
        var result = Translate(b, root); Assert.Equal(valid, result.Success);
        if (!valid) Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData("length", false)] [InlineData("length", true)]
    [InlineData("chars", false)] [InlineData("chars", true)]
    [InlineData("equal", false)]
    [InlineData("not_equal", false)]
    [InlineData("equals", false)]
    [InlineData("concat", false)]
    public void Exact_members_lower_without_reading_their_unsupported_bodies(string kind, bool direct)
    {
        var (b, _) = Core("Intrinsic" + kind);
        bool receiver = kind is "length" or "chars";
        string name = kind switch { "length" => "get_Length", "chars" => "get_Chars", "equal" => "op_Equality", "not_equal" => "op_Inequality", "equals" => "Equals", _ => "Concat" };
        byte[] signature = kind switch { "length" => [0x20, 0, 8], "chars" => [0x20, 1, 3, 8], "concat" => [0, 2, 0x0e, 0x0e, 0x0e], _ => [0, 2, 2, 0x0e, 0x0e] };
        var intrinsic = b.Method(name, signature, attributes: MethodAttributes.Public | (receiver ? 0 : MethodAttributes.Static),
            emit: il => { il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Throw); });
        b.Type("Test", "Driver", baseType: MetadataTokens.TypeDefinitionHandle(2));
        var literal = b.String("\udfff");
        var root = b.Method("Run", [0, 0, 8], emit: il =>
        {
            il.LoadString(literal);
            if (kind == "chars") il.LoadConstantI4(0); else if (!receiver) il.LoadString(literal);
            il.OpCode(receiver && !direct ? ILOpCode.Callvirt : ILOpCode.Call); il.Token(intrinsic);
            if (kind == "concat") { il.OpCode(ILOpCode.Pop); il.LoadConstantI4(1); }
            il.OpCode(ILOpCode.Ret);
        });
        var result = Translate(b, root); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Single(result.Program!.Flow.Methods);
        var expressions = result.Program.Flow.Methods[0].Blocks.SelectMany(b => b.Values).Select(v => v.Expression).ToArray();
        Assert.Contains(expressions, e => kind switch { "length" => e is IrExpression.StringLength, "chars" => e is IrExpression.StringCodeUnit,
            "concat" => e is IrExpression.StringConcat, _ => e is IrExpression.StringEquals });
        if (kind == "not_equal") Assert.Contains(expressions, e => e is IrExpression.Compare { Comparison: Int32Comparison.Equal });
        int expected = kind switch { "length" => "\udfff".Length, "chars" => "\udfff"[0], "concat" => 1,
            "not_equal" => string.Equals("\udfff", "\udfff", StringComparison.Ordinal) ? 0 : 1,
            _ => string.Equals("\udfff", "\udfff", StringComparison.Ordinal) ? 1 : 0 };
        Assert.Equal(expected, new ObjectTranslationConformanceTests.Evaluator(result.Program).Run());
    }

    [Theory]
    [InlineData("overload")]
    [InlineData("static")]
    [InlineData("private")]
    [InlineData("generic")]
    [InlineData("parameter")]
    [InlineData("newobj")]
    public void Names_alone_cannot_establish_an_intrinsic(string kind)
    {
        var (b, _) = Core("BadIntrinsic" + kind);
        byte[] signature = kind == "overload" ? [0x20, 0, 3] : kind == "generic" ? [0x30, 1, 0, 8] : [0x20, 0, 8];
        var method = b.Method("get_Length", signature, arity: kind == "generic" ? 1 : 0,
            attributes: kind == "private" ? MethodAttributes.Private : MethodAttributes.Public | (kind == "static" ? MethodAttributes.Static : 0));
        b.Type("Test", "Driver", baseType: MetadataTokens.TypeDefinitionHandle(2));
        var root = b.Method("Run", [0, 0, 8], emit: il =>
        {
            if (kind == "parameter") il.LoadConstantI4(0); else il.OpCode(ILOpCode.Ldnull);
            il.OpCode(kind == "newobj" ? ILOpCode.Newobj : ILOpCode.Callvirt); il.Token(method); il.OpCode(ILOpCode.Ret);
        });
        var result = Translate(b, root); Assert.False(result.Success); Assert.Contains("IL_", Assert.Single(result.Diagnostics).Location, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("even")]
    [InlineData("truncated")]
    [InlineData("flag")]
    public void Malformed_user_strings_fail_without_partial_input(string kind)
    {
        var (b, _) = Core("MalformedString"); var literal = b.String("abc"); var bytes = b.Build();
        using (var pe = new PEReader(new MemoryStream(bytes)))
        {
            var reader = pe.GetMetadataReader();
            int start = pe.PEHeaders.MetadataStartOffset + reader.GetHeapMetadataOffset(HeapIndex.UserString) + MetadataTokens.GetHeapOffset(literal);
            if (kind == "even") bytes[start] = 6;
            else if (kind == "truncated") bytes[start] = 127;
            else bytes[start + 7] = 2;
        }
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("bad.dll", bytes)).Value!;
        var result = resolver.ReadUserString(image, MetadataTokens.GetToken(literal), 10);
        Assert.False(result.Success); Assert.Null(result.Value);
    }
}
