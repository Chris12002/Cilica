using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ObjectExampleTests
{
    [Theory]
    [InlineData("O0")]
    [InlineData("O2")]
    public async Task Composed_example_matches_dotnet_and_recovers_after_each_fault(string optimization)
    {
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "Cilica.slnx"))) repository = repository.Parent;
        Assert.NotNull(repository);
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        Assert.True(configuration is "Debug" or "Release");
        using var files = new ResolverFiles();
        string output = files.DirectoryPath("object example with spaces");
        var run = await NativeProcess.Run("bash", [Path.Combine(repository.FullName, "examples", "Objects", "run.sh"),
            "--no-build", "--configuration", configuration, "--optimization", optimization, "--output", output], repository.FullName);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
        string actual = File.ReadAllText(Path.Combine(output, "actual.txt"));
        Assert.Equal(File.ReadAllText(Path.Combine(output, "expected.txt")), actual);
        string[] lines = actual.Split('\n'); Assert.Equal(13, lines.Length);
        Assert.Equal("empty: success 3", lines[0]);
        Assert.Equal("normal: success -50221840", lines[1]);
        Assert.Equal("other-item: success -162139655", lines[2]);
        Fault(3, "null", 4, 1);
        Assert.Equal("after-null: success -94256154", lines[4]);
        Fault(5, "bounds", 10, 2);
        Assert.Equal("after-bounds: success -1458818953", lines[6]);
        Fault(7, "negative-index", 10, 2); Fault(8, "overflow", 2, 2);
        Assert.Equal("after-overflow: success 169891", lines[9]);
        Assert.Equal("maximum-turns: success 279660569", lines[10]);
        Assert.Equal("invalid-turns: success -1", lines[11]); Assert.Empty(lines[12]);
        Assert.Contains("Native output matches .NET", run.Stdout, StringComparison.Ordinal);
        void Fault(int line, string name, int status, int il) => Assert.Matches(
            $"^{name}: fault {status} method=0x060000[0-9a-f]{{2}} il={il} image=[0-9a-f]{{64}}$", lines[line]);
    }
}
