using System;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class DefaultStaticTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Zero_initialized_readonly_statics_match_dotnet(bool optimized) => IntegerObjectTranslationTests.Verify(optimized,
        [(nameof(DefaultStaticProbe.Values), DefaultStaticProbe.Values), (nameof(DefaultStaticProbe.Generic), DefaultStaticProbe.Generic)], typeof(DefaultStaticProbe));

    [Fact]
    public void Initializers_and_legacy_profile_remain_diagnostic()
    {
        var initialized = IntegerObjectTranslationTests.Translate("Initialized", fixture: typeof(DefaultStaticProbe));
        Assert.False(initialized.Success); Assert.Contains(initialized.Diagnostics, d => d.Message.Contains("initialization", StringComparison.Ordinal));
        Assert.False(IntegerObjectTranslationTests.Translate("Values", enabled: false, fixture: typeof(DefaultStaticProbe)).Success);
    }

    [Theory]
    [InlineData("mutable")] [InlineData("private")] [InlineData("address")] [InlineData("write")] [InlineData("literal")]
    public void Unsupported_storage_and_access_cannot_be_treated_as_default(string kind)
    {
        var b = new ResolutionAssemblyBuilder(new("BadDefaultStatic", new Version(1, 0, 0, 0)));
        b.Type("Test", "Holder");
        var attributes = (kind == "private" ? FieldAttributes.Private : FieldAttributes.Public) | FieldAttributes.Static;
        if (kind != "mutable") attributes |= FieldAttributes.InitOnly;
        if (kind == "literal") attributes |= FieldAttributes.Literal;
        var field = b.FieldWithAttributes("Value", attributes, 6, 8);
        b.Type("Test", "Caller");
        var root = b.Method("Run", [0, 0, 8], emit: il => {
            if (kind == "write") { il.LoadConstantI4(7); il.OpCode(ILOpCode.Stsfld); il.Token(field); }
            il.OpCode(kind == "address" ? ILOpCode.Ldsflda : ILOpCode.Ldsfld); il.Token(field);
            if (kind == "address") il.OpCode(ILOpCode.Ldind_i4);
            il.OpCode(ILOpCode.Ret);
        });
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(files.Write("bad.dll", b.Build())).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
        Assert.False(result.Success); Assert.Null(result.Program); Assert.NotEmpty(result.Diagnostics);
    }
}
