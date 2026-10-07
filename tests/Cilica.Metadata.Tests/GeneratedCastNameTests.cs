using System;
using System.Threading.Tasks;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class GeneratedCastNameTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public Task Generated_closed_type_cast_error_text_matches_dotnet(bool optimized) =>
        IntegerObjectTranslationTests.Verify(optimized,[("Text",GeneratedCastNameProbe.Text), ("NestedSource",GeneratedCastNameProbe.NestedSource), ("NestedTarget",GeneratedCastNameProbe.NestedTarget), ("NestedArgument",GeneratedCastNameProbe.NestedArgument), ("NestedGeneric",GeneratedCastNameProbe.NestedGeneric), ("NestedArray",GeneratedCastNameProbe.NestedArray), ("NestedBox",GeneratedCastNameProbe.NestedBox)],typeof(GeneratedCastNameProbe));
    [Fact]
    public void Cast_display_names_preserve_full_runtime_identity_names()
    {
        var result=IntegerObjectTranslationTests.Translate("NestedGeneric",fixture:typeof(GeneratedCastNameProbe));
        Assert.True(result.Success,string.Join("; ",result.Diagnostics));
        Assert.Contains(result.Program!.Flow.Flow.Flow.Types.References.Types, type =>
            type.Source.RuntimeName == "Cilica.Fixtures.Execution.CastNameOuter+Generic`1[Cilica.Fixtures.Execution.CastNameOuter+Nested]"
            && type.Source.CastName == "Generic`1[Cilica.Fixtures.Execution.CastNameOuter+Nested]");
    }

}
