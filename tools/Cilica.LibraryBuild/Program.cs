using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cilica.LibraryBuild;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            string? compiler = null, output = null;
            string name = "Cilica.Library";
            var sources = new List<string>(); var references = new List<string>();
            for (int i = 0; i < args.Length; i += 2)
            {
                if (i + 1 == args.Length) throw new ArgumentException("Every option requires a value.");
                switch (args[i])
                {
                    case "--compiler-directory" when compiler is null: compiler = args[i + 1]; break;
                    case "--output-directory" when output is null: output = args[i + 1]; break;
                    case "--assembly-name": name = args[i + 1]; break;
                    case "--source": sources.Add(args[i + 1]); break;
                    case "--reference": references.Add(args[i + 1]); break;
                    default: throw new ArgumentException("Unknown or repeated option: " + args[i]);
                }
            }
            if (compiler is null || output is null)
                throw new ArgumentException("Supply --compiler-directory, --output-directory, --source and --reference. Optional: --assembly-name.");
            var result = await LibraryBuilder.Build(new(compiler, [.. sources], [.. references], output, name));
            Console.WriteLine("Built " + result.AssemblyPath);
            Console.WriteLine("Evidence " + result.ManifestPath);
            Console.WriteLine("Manifest SHA-256 " + result.ManifestSha256 + " (not an approval)");
            return 0;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.IO.IOException
            or UnauthorizedAccessException or TimeoutException)
        { Console.Error.WriteLine(error.Message); return 1; }
    }
}
