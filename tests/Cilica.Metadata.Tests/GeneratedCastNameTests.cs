using System.Threading.Tasks;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class GeneratedCastNameTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public Task Generated_closed_type_cast_error_text_matches_dotnet(bool optimized) =>
        IntegerObjectTranslationTests.Verify(optimized,[("Text",GeneratedCastNameProbe.Text)],typeof(GeneratedCastNameProbe));
}
