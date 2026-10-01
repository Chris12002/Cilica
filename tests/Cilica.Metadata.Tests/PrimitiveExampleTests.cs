using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class PrimitiveExampleTests
{
    [Fact]
    public async Task DocumentedCommandMatchesDotNetAndRecoversAfterOverflow()
    {
        string repository = Repository();
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        Assert.True(configuration is "Debug" or "Release", "Expected test output under bin/Debug or bin/Release.");
        using var files = new ResolverFiles();
        string output = files.DirectoryPath("example output with spaces");
        var run = await NativeProcess.Run("bash",
            [Path.Combine(repository, "examples", "Primitive", "run.sh"),
                "--no-build", "--configuration", configuration, "--output", output], repository);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr);
        Assert.Empty(run.Stderr);
        string actual = File.ReadAllText(Path.Combine(output, "actual.txt"));
        Assert.Equal(File.ReadAllText(Path.Combine(output, "expected.txt")), actual);
        string[] lines = actual.Split('\n');
        Assert.Equal(5, lines.Length);
        Assert.Equal("normal: success 25", lines[0]);
        Assert.Matches("^overflow: Overflow method=0x060000[0-9a-f]{2} il=2 image=[0-9a-f]{64}$", lines[1]);
        Assert.Equal("recovery: success 4", lines[2]);
        Assert.Equal("empty: success 2147483647", lines[3]);
        Assert.Empty(lines[4]);
        Assert.Contains("Native output matches .NET", run.Stdout, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BuildInvocationWorksWithAndWithoutOfflineOption(bool offline)
    {
        if (OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The example requires Bash and Unix tools.");
        using var files = new ResolverFiles();
        string tools = files.DirectoryPath("tools with spaces");
        string dotnet = Path.Combine(tools, "dotnet");
        // Stop before restore/build; no hosted feed is needed for this shell regression.
        File.WriteAllText(dotnet, "#!/bin/bash\nprintf '%s\\n' \"$@\"\nexit 37\n");
        File.SetUnixFileMode(dotnet, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        string repository = Repository();
        var arguments = new List<string>
        {
            "PATH=" + tools + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
            "bash", Path.Combine(repository, "examples", "Primitive", "run.sh"),
        };
        if (offline) arguments.Add("--no-restore");
        var run = await NativeProcess.Run("env", arguments, repository);
        Assert.Equal(37, run.ExitCode);
        Assert.Empty(run.Stderr);
        var expected = new List<string>
        {
            "build", Path.Combine(repository, "examples", "Primitive", "Prepare", "Prepare.csproj"),
            "-c", "Release", "-m:1", "-p:UseSharedCompilation=false", "-nodeReuse:false",
        };
        if (offline) expected.Add("--no-restore");
        expected.Add("");
        Assert.Equal(expected, run.Stdout.Split('\n'));
    }

    private static string Repository()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Cilica.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return directory.FullName;
    }
}
