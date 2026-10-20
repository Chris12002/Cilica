"""Build diagnostic copies only. Run from the compiler checkout, with an output directory."""
from pathlib import Path
import hashlib, json, re, shutil, subprocess, sys

root = Path.cwd()
recipe = Path(__file__).resolve().parent
out = Path(sys.argv[1]).resolve()
source_root = Path(sys.argv[2]).resolve()
unity_source = Path(sys.argv[3]).resolve()
assert not out.exists(), "Use a fresh output directory"
out.mkdir(parents=True)
# Standalone benchmark recipes must not inherit compiler-repository build policy.
for name in ["Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props"]:
    (out / name).write_text("<Project />\n")
inputs = source_root.parent
assert hashlib.sha256((inputs / "dll/DiffPlex.Benchmark.dll").read_bytes()).hexdigest() == \
    "652ab88f876736bf50d11bdcbadf4ba411a3c4bc91fb6b09ee038f1709ce60f8"
sources = out / "candidate"
shutil.copytree(source_root, sources)
managed = out / "managed"
managed.mkdir()
for name in ["BenchProbe.cs", "SelectedRoots.csproj"]:
    shutil.copy2(recipe.parent / name, managed / name)
shutil.copy2(recipe / "SectionClock.cs", managed / "SectionClock.cs")
originals = {}

def wrap(path, signature, section):
    text = path.read_text(encoding="utf-8-sig")
    start = text.index(signature)
    opening = text.index("{", start)
    level = 1
    end = opening + 1
    # The selected methods contain no brace-bearing strings/comments.
    while level:
        if text[end] == "{": level += 1
        elif text[end] == "}": level -= 1
        end += 1
    text = text[:opening + 1] + f"\nusing (new Cilica.SectionTiming.SectionClock.Scope({section})) {{\n" + \
        text[opening + 1:end - 1] + "\n}\n" + text[end - 1:]
    path.write_text(text)

targets = [
    (managed / "BenchProbe.cs", "private static DiffResult Diff(int work)", 0, "06000053"),
    (sources / "DiffPlex/Differ.cs", "private static void BuildPieceHashes(", 2, "06000011"),
    (sources / "DiffPlex/Differ.cs", "protected static void BuildModificationData(ModificationData A, ModificationData B)", 3, "0600000f"),
    (sources / "DiffPlex/Chunkers/CharacterChunker.cs", "public IReadOnlyList<string> Chunk(string text)", 1, "06000021"),
    (sources / "DiffPlex/Chunkers/LineChunker.cs", "public IReadOnlyList<string> Chunk(string text)", 1, "06000029"),
]
for path, signature, section, token in targets:
    originals.setdefault(str(path.relative_to(out)), hashlib.sha256(path.read_bytes()).hexdigest())
    wrap(path, signature, section)
project = managed / "SelectedRoots.csproj"
project.write_text(project.read_text().replace('<Compile Include="BenchProbe.cs" />',
    '<Compile Include="BenchProbe.cs" /><Compile Include="SectionClock.cs" />'))
cmd = ["dotnet", "build", str(project), "-c", "Release", "-o", str(out / "dll"),
       "-p:CandidateSource=" + str(sources), "--source", str(inputs / "feed"), "-v:q"]
result = subprocess.run(cmd, text=True, capture_output=True, timeout=120)
(out / "managed-build.log").write_text(result.stdout + result.stderr)
result.check_returncode()
host = out / "host-source"
host.mkdir()
shutil.copy2(recipe / "DotNetSections.cs", host / "Program.cs")
(host / "Host.csproj").write_text(f"""<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
<Optimize>true</Optimize><NuGetAudit>false</NuGetAudit></PropertyGroup>
<ItemGroup><Reference Include="DiffPlex.Benchmark"><HintPath>{out / 'dll/DiffPlex.Benchmark.dll'}</HintPath></Reference></ItemGroup>
</Project>""")
host_cmd = ["dotnet", "build", str(host / "Host.csproj"), "-c", "Release", "-o", str(out / "host"), "-v:q"]
result = subprocess.run(host_cmd, text=True, capture_output=True, timeout=120)
(out / "host-build.log").write_text(result.stdout + result.stderr)
result.check_returncode()
unity = out / "UnitySections"
for name in ["Assets", "Packages", "ProjectSettings"]:
    shutil.copytree(unity_source / name, unity / name)
(unity / "Assets/BenchmarkHarness.cs").unlink()
shutil.copy2(recipe / "UnitySections.cs", unity / "Assets/BenchmarkHarness.cs")
shutil.copy2(out / "dll/DiffPlex.Benchmark.dll", unity / "Assets/Plugins/DiffPlex.Benchmark.dll")
(out / "prepare.json").write_text(json.dumps({
    "revision": subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip(),
    "commands": [cmd, host_cmd], "original_sources": originals,
    "instrumented_sources": {str(path.relative_to(out)): hashlib.sha256(path.read_bytes()).hexdigest()
                             for path, *_ in targets},
    "boundaries": [{"method": signature, "section": section, "original_token": token}
                   for _, signature, section, token in targets],
    "section_names": ["setup_results", "chunking", "hash_preparation", "diff_algorithm"],
    "assembly_sha256": hashlib.sha256((out / "dll/DiffPlex.Benchmark.dll").read_bytes()).hexdigest(),
}, indent=2))
print(out)
