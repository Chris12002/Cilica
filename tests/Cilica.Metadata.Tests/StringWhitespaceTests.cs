using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class StringWhitespaceTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(StringWhitespaceProbe), "Mask", "Strings"));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task All_utf16_units_and_mixed_strings_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("WhitespaceMask", programs[0], StringWhitespaceProbe.Mask)],
            Enumerable.Range(0, 2048).Select(i => (i * 32, 0)).ToArray());
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("WhitespaceStrings", programs[1], StringWhitespaceProbe.Strings)]);
    }
    [Fact]
    public async Task Selected_string_body_cannot_become_an_instance_method_or_initializer()
    {
        var program = (await Programs.Value)[0];
        var objects = program.Flow;
        var binding = objects.Bindings.Single(b => b.Owner == objects.Flow.Flow.Types.References.String);
        foreach (var invalid in new[] {
            binding with { Kind = ObjectMethodKind.Instance, Attributes = binding.Attributes & ~MethodAttributes.Static },
            binding with { TypeInitializerName = "System.String" } })
        {
            var changed = program with { Flow = objects with { Bindings = objects.Bindings.Replace(binding, invalid) } };
            Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(changed));
        }
    }
}
