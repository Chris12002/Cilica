using System;
using System.IO;
using System.Reflection.Metadata;
using Cilica.Compiler;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class DiscoveryDiagnosticTests
{
    [Fact]
    public void Nested_call_resolution_reports_inner_instruction_and_requesting_call_site()
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("NestedDiagnostic", new Version(1, 0, 0, 0)));
        var obj = b.TypeReference("System", "Object", b.Reference(library.Identity));
        var absent = b.Type("Test", "Absent");
        b.Type("Test", "Driver", baseType: obj);
        var missing = b.Member(absent, "Missing", 0, 2, 8, 8, 8);
        var middle = b.Method("Middle", [0, 2, 8, 8, 8], emit: il => {
            il.OpCode(ILOpCode.Nop); il.LoadArgument(0); il.LoadArgument(1); il.Call(missing); il.OpCode(ILOpCode.Ret);
        });
        var root = b.Method("Run", [0, 2, 8, 8, 8], emit: il => {
            il.LoadArgument(0); il.LoadArgument(1); il.Call(middle); il.OpCode(ILOpCode.Ret);
        });
        var input = resolver.OpenInput(files.Write("diagnostic.dll", b.Build())).Value!;
        var method = resolver.ResolveDeclaredMethod(input, middle).Value!;
        var read = resolver.ReadManagedExceptionMethodInput(method, library);
        Assert.False(read.Success);
        Assert.Contains(read.Diagnostics, d => d.Location!.Contains("Test.Driver::Middle token 0x06000001 IL_0003", StringComparison.Ordinal));
        var translated = ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
        Assert.False(translated.Success);
        var failure = Assert.Single(translated.Diagnostics);
        Assert.Contains("Test.Driver::Middle token 0x06000001 IL_0003", failure.Location!, StringComparison.Ordinal);
        Assert.Contains("requested from Test.Driver::Run token 0x06000002 IL_0002", failure.Location!, StringComparison.Ordinal);
        Assert.Contains("Missing", failure.Message, StringComparison.Ordinal);
    }
}
