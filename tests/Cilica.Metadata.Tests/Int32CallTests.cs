using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Cli;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class Int32CallTests
{
    private static ResolutionAssemblyBuilder Builder(string name = "Calls")
    {
        var builder = new ResolutionAssemblyBuilder(new(name, new Version(1, 0, 0, 0)));
        builder.Type("", "Entry"); return builder;
    }

    private static ProgramTranslationResult Translate(ResolverFiles files, ResolutionAssemblyBuilder builder, MethodDefinitionHandle root, MethodReadLimits? limits = null)
    {
        string path = files.Write("input.dll", builder.Build());
        using var resolver = files.Open();
        return Int32Translator.TranslateProgram(resolver, resolver.OpenInput(path).Value!, root, limits);
    }

    [Theory]
    [InlineData(nameof(IntegerCalls.Diamond), 3)]
    [InlineData(nameof(IntegerCalls.Ordered), 2)]
    [InlineData(nameof(IntegerCalls.Recursive), 1)]
    [InlineData(nameof(IntegerCalls.Mutual), 2)]
    public void Closure_deduplicates_shared_helpers_and_cycles_and_detaches_from_resolver(string name, int count)
    {
        IrProgram program;
        using (var resolver = AssemblyResolver.Create([]).Value!)
        {
            var owner = typeof(IntegerCalls);
            var assembly = resolver.OpenInput(owner.Assembly.Location).Value!;
            var result = Int32Translator.TranslateProgram(resolver, assembly,
                MetadataTokens.MethodDefinitionHandle(owner.GetMethod(name)!.MetadataToken & 0xffffff));
            Assert.True(result.Success, string.Join("; ", result.Diagnostics)); program = result.Program!;
        }
        Assert.Equal(count, program.Methods.Length);
        string expected = CppEmitter.EmitProgram(program).Text;
        Assert.Equal(expected, CppEmitter.EmitProgram(program with { Methods = [.. program.Methods.Reverse()] }).Text);
        Assert.Contains("extern \"C\"", expected, StringComparison.Ordinal);
        Assert.All(program.Methods, method => Assert.Equal(method.Source.ContentHash, IrMethodId.From(method.Source).ContentHash));
    }

    [Fact]
    public void Cross_assembly_member_binding_uses_scoped_identity_and_explicit_directories()
    {
        using var files = new ResolverFiles();
        var identity = new AssemblyIdentity("Helper", new Version(1, 0, 0, 0));
        var dependency = new ResolutionAssemblyBuilder(identity); dependency.Type("N", "Helper");
        var helper = dependency.Method("Same", [0, 2, 8, 8, 8], emit: il =>
        { il.LoadArgument(0); il.LoadArgument(1); il.OpCode(ILOpCode.Sub); il.OpCode(ILOpCode.Ret); });
        files.Write("unrelated-filename.dll", dependency.Build(), "dependency");
        var builder = Builder(); var owner = builder.TypeReference("N", "Helper", builder.Reference(identity));
        var member = builder.Member(owner, "Same", 0, 2, 8, 8, 8);
        var root = builder.Method("Same", [0, 2, 8, 8, 8], emit: il =>
        { il.LoadArgument(1); il.LoadArgument(0); il.Call(member); il.OpCode(ILOpCode.Ret); });
        string input = files.Write("input.dll", builder.Build());
        using var resolver = files.Open("dependency");
        var result = Int32Translator.TranslateProgram(resolver, resolver.OpenInput(input).Value!, root);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!; Assert.Equal(2, program.Methods.Length);
        IrMethod caller = program.Methods.Single(m => IrMethodId.From(m.Source) == program.Entry);
        var call = Assert.IsType<IrExpression.Call>(caller.Blocks[0].Values[^1].Expression);
        Assert.Equal(new[] { 0, 1 }, call.Arguments);
        Assert.NotEqual(program.Entry.ContentHash, call.Target.ContentHash);
        Assert.Equal(MetadataTokens.GetToken(helper), call.Target.Token);
        Assert.Contains("Helper, Version=1.0.0.0", program.Methods.Single(m => IrMethodId.From(m.Source) == call.Target).Source.AssemblyIdentity, StringComparison.Ordinal);
        using var missing = files.Open();
        var failure = Int32Translator.TranslateProgram(missing, missing.OpenInput(input).Value!, root);
        Assert.Null(failure.Program); Assert.Equal(DiagnosticIds.AssemblyNotFound, failure.Diagnostics[0].Id);
        Assert.Contains("IL_0002", failure.Diagnostics[0].Location!, StringComparison.Ordinal);
        Assert.Contains("Reachable via:", failure.Diagnostics[0].Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("opcode", "CIL2001")]
    [InlineData("instance", "CIL2002")]
    [InlineData("return", "CIL2002")]
    [InlineData("initializer", "CIL2001")]
    [InlineData("native", "CIL2001")]
    [InlineData("generic", "CIL2002")]
    [InlineData("generic_owner", "CIL2002")]
    [InlineData("reference", "CIL2001")]
    [InlineData("underflow", "CIL1013")]
    [InlineData("token", "CIL1014")]
    [InlineData("missing_method", "CIL3009")]
    [InlineData("callvirt", "CIL2001")]
    public void Unsupported_callees_and_bad_calls_return_no_partial_program(string kind, string code)
    {
        using var files = new ResolverFiles(); var builder = Builder();
        if (kind == "reference") builder.MarkReferenceAssembly();
        if (kind == "initializer")
        {
            builder.Type("", "HelperOwner");
            builder.Method(".cctor", [0, 0, 1], emit: il => il.OpCode(ILOpCode.Ret));
        }
        if (kind == "generic_owner") builder.Type("", "Generic`1", arity: 1);
        byte[] signature = kind switch { "instance" => [0x20, 0, 8], "return" => [0, 0, 1], "generic" => [0x10, 1, 0, 8], "underflow" => [0, 1, 8, 8], _ => [0, 0, 8] };
        var helper = builder.Method("Helper", signature, arity: kind == "generic" ? 1 : 0,
            attributes: kind == "instance" ? MethodAttributes.Public : MethodAttributes.Public | MethodAttributes.Static,
            emit: kind == "native" ? null : il => { il.LoadConstantI4(1); if (kind == "opcode") il.OpCode(ILOpCode.Neg); il.OpCode(ILOpCode.Ret); });
        if (kind is "generic_owner" or "initializer") builder.Type("", "RootOwner");
        EntityHandle target = kind switch
        {
            "token" => MetadataTokens.TypeDefinitionHandle(2),
            "missing_method" => MetadataTokens.MethodDefinitionHandle(0xffffff),
            "generic" => builder.Instantiate(helper, 0x0a, 1, 8),
            _ => helper,
        };
        var root = builder.Method("Root", [0, 0, 8], emit: il =>
        { il.OpCode(kind == "callvirt" ? ILOpCode.Callvirt : ILOpCode.Call); il.Token(target); il.OpCode(ILOpCode.Ret); });
        var result = Translate(files, builder, root);
        Assert.Null(result.Program); Assert.False(result.Success);
        Assert.Equal(code, result.Diagnostics[0].Id);
        Assert.Contains("Reachable via:", result.Diagnostics[0].Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ProgramLimits.MaximumMethods, true)]
    [InlineData(ProgramLimits.MaximumMethods + 1, false)]
    public void Method_limit_is_exact_and_graph_walk_is_iterative(int count, bool success)
    {
        using var files = new ResolverFiles(); var builder = Builder();
        for (int index = 1; index <= count; index++)
        {
            int next = index + 1;
            builder.Method("M" + index.ToString(CultureInfo.InvariantCulture), [0, 0, 8], emit: il =>
            { if (next <= count) il.Call(MetadataTokens.MethodDefinitionHandle(next)); else il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret); });
        }
        var result = Translate(files, builder, MetadataTokens.MethodDefinitionHandle(1));
        Assert.Equal(success, result.Success);
        if (success) Assert.Equal(count, result.Program!.Methods.Length);
        else Assert.Equal(DiagnosticIds.TranslationLimit, result.Diagnostics[0].Id);
    }

    [Theory]
    [InlineData(65536, true)]
    [InlineData(65537, false)]
    public void Instruction_limit_counts_all_methods(int count, bool success)
    {
        using var files = new ResolverFiles(); var builder = Builder();
        builder.Method("Root", [0, 0, 8], emit: il =>
        { il.Call(MetadataTokens.MethodDefinitionHandle(2)); for (int n = 2; n < 32768; n++) il.OpCode(ILOpCode.Nop); il.OpCode(ILOpCode.Ret); });
        builder.Method("Helper", [0, 0, 8], emit: il =>
        { il.LoadConstantI4(1); for (int n = 2; n < count - 32768; n++) il.OpCode(ILOpCode.Nop); il.OpCode(ILOpCode.Ret); });
        var result = Translate(files, builder, MetadataTokens.MethodDefinitionHandle(1), new(MaximumInstructions: 1000000));
        Assert.Equal(success, result.Success);
        if (!success)
        {
            Assert.Equal(DiagnosticIds.TranslationLimit, result.Diagnostics[0].Id);
            Assert.DoesNotContain("Aggregate program IL", result.Diagnostics[0].Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Failure_in_transitive_helper_preserves_output_and_reports_the_inclusion_chain()
    {
        using var files = new ResolverFiles(); var builder = Builder();
        var bad = builder.Method("Bad", [0, 0, 8], emit: il => { il.LoadConstantI4(1); il.OpCode(ILOpCode.Neg); il.OpCode(ILOpCode.Ret); });
        var middle = builder.Method("Middle", [0, 0, 8], emit: il => { il.Call(bad); il.OpCode(ILOpCode.Ret); });
        var root = builder.Method("Root", [0, 0, 8], emit: il => { il.Call(middle); il.OpCode(ILOpCode.Ret); });
        string path = files.Write("input.dll", builder.Build()), output = Path.Combine(files.Root, "keep.cpp");
        File.WriteAllText(output, "keep"); using var stdout = new StringWriter(); using var stderr = new StringWriter();
        int exit = CilicaCommand.Run(["emit-cpp", path, "--root", MetadataTokens.GetToken(root).ToString("x8", CultureInfo.InvariantCulture), "--output", output], stdout, stderr);
        Assert.Equal(CilicaCommand.ErrorsReported, exit); Assert.Empty(stdout.ToString()); Assert.Equal("keep", File.ReadAllText(output));
        string diagnostic = stderr.ToString();
        Assert.Contains("Bad", diagnostic, StringComparison.Ordinal); Assert.Contains("Middle", diagnostic, StringComparison.Ordinal); Assert.Contains("Root", diagnostic, StringComparison.Ordinal);
        Assert.Contains("IL_0001", diagnostic, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("entry")]
    [InlineData("duplicate")]
    [InlineData("unreachable")]
    [InlineData("target")]
    [InlineData("arity")]
    [InlineData("default_arguments")]
    [InlineData("forward_argument")]
    [InlineData("null_target")]
    [InlineData("methods")]
    public void Forged_programs_cannot_bypass_closure_checks(string damage)
    {
        IrProgram program;
        using (var resolver = AssemblyResolver.Create([]).Value!)
        {
            var owner = typeof(IntegerCalls); var assembly = resolver.OpenInput(owner.Assembly.Location).Value!;
            program = Int32Translator.TranslateProgram(resolver, assembly,
                MetadataTokens.MethodDefinitionHandle(owner.GetMethod(nameof(IntegerCalls.Scale))!.MetadataToken & 0xffffff)).Program!;
        }
        int index = Array.FindIndex(program.Methods.ToArray(), m => IrMethodId.From(m.Source) == program.Entry);
        IrMethod caller = program.Methods[index]; IrBlock block = caller.Blocks[0];
        int statement = Array.FindIndex(block.Statements.ToArray(), s => s is IrStatement.Define { Value.Expression: IrExpression.Call });
        var define = (IrStatement.Define)block.Statements[statement]; var call = (IrExpression.Call)define.Value.Expression;
        program = damage switch
        {
            "entry" => program with { Entry = program.Entry with { Token = 0x060000ff } },
            "duplicate" => program with { Methods = program.Methods.Add(caller) },
            "unreachable" => program with { Entry = call.Target },
            "methods" => program with { Methods = default },
            _ => program with { Methods = program.Methods.SetItem(index, caller with { Blocks = caller.Blocks.SetItem(0,
                block with { Statements = block.Statements.SetItem(statement, new IrStatement.Define(define.Value with { Expression = damage switch
                {
                    "target" => call with { Target = call.Target with { ContentHash = new string('b', 64) } },
                    "arity" => call with { Arguments = [] },
                    "default_arguments" => call with { Arguments = default },
                    "forward_argument" => call with { Arguments = [define.Value.Id, 0] },
                    _ => call with { Target = null! },
                } })) }) }) },
        };
        Assert.Throws<ArgumentException>(() => CppEmitter.EmitProgram(program));
    }

    private static IrMethod Detached(int token, int statements, IrMethodId? target = null, int arity = 0)
    {
        var source = new Cilica.Metadata.IL.MethodSource("display only", new string('a', 64), token, "display only");
        return new(source, [.. Enumerable.Repeat(IrType.Int32, arity)], IrType.Int32,
            [new(0, 0, [], [.. Enumerable.Range(0, statements).Select(index => (IrStatement)new IrStatement.Define(
                new(index, IrType.Int32, 0, index == 0 && target is not null ? new IrExpression.Call(target, []) : new IrExpression.Constant(1)), index == 0 && target is not null ? IrExceptionTarget.MethodExit : null))],
                new IrReturn(0, statements - 1))], []);
    }

    [Theory]
    [InlineData(65536, true)]
    [InlineData(65537, false)]
    public void Statement_limit_is_aggregate_even_when_each_method_is_within_its_limit(int count, bool success)
    {
        IrMethod helper = Detached(0x06000002, 32768);
        IrMethod caller = Detached(0x06000001, count - 32768, IrMethodId.From(helper.Source));
        var program = new IrProgram(IrMethodId.From(caller.Source), [caller, helper]);
        if (success) Assert.NotEmpty(CppEmitter.EmitProgram(program).Text);
        else Assert.Throws<ArgumentException>(() => CppEmitter.EmitProgram(program));
    }

    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void Call_argument_copies_are_bounded_independently_of_value_count(int calls, bool success)
    {
        IrMethod helper = Detached(0x06000002, 1, arity: 1024);
        IrMethod caller = Detached(0x06000001, 1);
        IrBlock block = caller.Blocks[0];
        caller = caller with { Blocks = [block with
        {
            Statements = block.Statements.AddRange(Enumerable.Range(1, calls).Select(id => (IrStatement)new IrStatement.Define(
                new(id, IrType.Int32, 0, new IrExpression.Call(IrMethodId.From(helper.Source), [.. Enumerable.Repeat(0, 1024)])), IrExceptionTarget.MethodExit))),
            Terminator = new IrReturn(0, calls),
        }] };
        var program = new IrProgram(IrMethodId.From(caller.Source), [caller, helper]);
        if (success) Assert.NotEmpty(CppEmitter.EmitProgram(program).Text);
        else Assert.Throws<ArgumentException>(() => CppEmitter.EmitProgram(program));
        // A method carrying calls cannot be emitted without its verified closure.
        Assert.Throws<ArgumentException>(() => CppEmitter.Emit(caller));
    }

    [Theory]
    [InlineData("same_private", true)]
    [InlineData("other_private", false)]
    [InlineData("other_internal", false)]
    [InlineData("private_scope", false)]
    [InlineData("hidden_owner", false)]
    [InlineData("nested_public", true)]
    [InlineData("nested_private", false)]
    [InlineData("hidden_outer", false)]
    public void Binding_does_not_bypass_the_bounded_call_access_contract(string kind, bool success)
    {
        using var files = new ResolverFiles(); var builder = Builder();
        var outer = builder.Type("", "Helper", attributes: kind is "hidden_owner" or "hidden_outer" ? TypeAttributes.NotPublic : TypeAttributes.Public);
        if (kind.StartsWith("nested", StringComparison.Ordinal) || kind == "hidden_outer")
            builder.Type("", "Nested", parent: outer, attributes: kind == "nested_private" ? TypeAttributes.NestedPrivate : TypeAttributes.NestedPublic);
        var access = kind switch
        {
            "same_private" or "other_private" => MethodAttributes.Private,
            "other_internal" => MethodAttributes.Assembly,
            "private_scope" => MethodAttributes.PrivateScope,
            _ => MethodAttributes.Public,
        };
        var helper = builder.Method("Helper", [0, 0, 8], attributes: access | MethodAttributes.Static,
            emit: il => { il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret); });
        if (kind != "same_private") builder.Type("", "Caller");
        var root = builder.Method("Root", [0, 0, 8], emit: il => { il.Call(helper); il.OpCode(ILOpCode.Ret); });
        var result = Translate(files, builder, root);
        Assert.Equal(success, result.Success);
        if (!success) Assert.Equal(DiagnosticIds.ExecutionUnsupported, result.Diagnostics[0].Id);
    }

    [Fact]
    public void Inherited_member_owner_is_a_diagnostic_without_weakening_general_binding()
    {
        using var files = new ResolverFiles(); var builder = Builder();
        var parent = builder.Type("", "Base");
        var helper = builder.Method("Helper", [0, 0, 8], emit: il => { il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret); });
        var derived = builder.Type("", "Derived", baseType: parent);
        var reference = builder.Member(derived, "Helper", 0, 0, 8);
        builder.Type("", "Caller");
        var root = builder.Method("Root", [0, 0, 8], emit: il => { il.Call(reference); il.OpCode(ILOpCode.Ret); });
        using var resolver = files.Open(); var assembly = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        Assert.Equal(helper, resolver.ResolveMethod(assembly, reference).Value!.Handle);
        var failure = Int32Translator.TranslateProgram(resolver, assembly, root);
        Assert.Null(failure.Program); Assert.Equal(DiagnosticIds.ResolutionUnsupported, failure.Diagnostics[0].Id);
        Assert.Contains("declaring type explicitly", failure.Diagnostics[0].Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(1, false)]
    public void Declaring_owner_snapshot_is_bounded(int depth, bool success)
    {
        using var files = new ResolverFiles(); var builder = Builder();
        var parent = builder.Type("", "Outer"); builder.Type("", "Nested", parent: parent);
        var root = builder.Method("Root", [0, 0, 8], emit: il => { il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret); });
        using var resolver = files.Open(); var assembly = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        var result = resolver.ReadMethodInput(assembly, root, new(MaximumOwnerDepth: depth));
        Assert.Equal(success, result.Success);
        if (success) Assert.True(result.Value!.OwnerExternallyVisible);
        else Assert.Equal(DiagnosticIds.TranslationLimit, result.Diagnostics[0].Id);
    }
}
