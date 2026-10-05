using System.Threading.Tasks;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class BoxedInterfaceTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Boxed_interface_ancestry_variance_storage_and_faults_match_dotnet(bool optimized) =>
        IntegerObjectTranslationTests.Verify(optimized,
            [("Relations", BoxedInterfaces.Relations), ("Storage", BoxedInterfaces.Storage), ("Faults", BoxedInterfaces.Faults)], typeof(BoxedInterfaces));
    [Theory]
    [InlineData("UnsupportedDispatch")] [InlineData("MixedDispatch")]
    public void Boxed_interface_dispatch_stays_diagnostic(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(BoxedInterfaces));
        Assert.False(result.Success); Assert.Null(result.Program);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("interface", System.StringComparison.OrdinalIgnoreCase));
    }
}

