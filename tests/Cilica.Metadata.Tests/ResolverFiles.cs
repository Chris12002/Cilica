using System;
using System.IO;
using System.Linq;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

internal sealed class ResolverFiles : IDisposable
{
    internal string Root { get; } = Path.Combine(Path.GetTempPath(), "cilica-resolver-" + Guid.NewGuid().ToString("N"));

    internal string DirectoryPath(string name)
    {
        string path = Path.Combine(Root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    internal string Write(string file, byte[] bytes, string directory = "refs")
    {
        string path = Path.Combine(DirectoryPath(directory), file);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    internal AssemblyResolver Open(params string[] directories)
    {
        var result = AssemblyResolver.Create(directories.Select(DirectoryPath));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
    }
}
