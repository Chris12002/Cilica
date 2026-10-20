"""Build the same managed benchmark with no inserted scopes, retaining only the host API."""
from pathlib import Path
import json, shutil, subprocess, sys
recipe = Path(__file__).resolve().parent
out = Path(sys.argv[1]).resolve()
inputs = Path(sys.argv[2]).resolve()
managed = out / "control-managed"
managed.mkdir()
for name in ["SelectedRoots.csproj", "BenchProbe.cs"]:
    shutil.copy2(recipe.parent / name, managed / name)
shutil.copy2(recipe / "SectionClock.cs", managed / "SectionClock.cs")
project = managed / "SelectedRoots.csproj"
project.write_text(project.read_text().replace('<Compile Include="BenchProbe.cs" />',
    '<Compile Include="BenchProbe.cs" /><Compile Include="SectionClock.cs" />'))
cmd = ["dotnet", "build", str(project), "-c", "Release", "-o", str(out / "control-dll"),
       "-p:CandidateSource=" + str(inputs / "candidate"), "--source", str(inputs / "feed"), "-v:q"]
result = subprocess.run(cmd, text=True, capture_output=True, timeout=120)
(out / "control-build.log").write_text(result.stdout + result.stderr)
result.check_returncode()
(out / "control-command.json").write_text(json.dumps(cmd, indent=2))
shutil.copytree(out / "host", out / "control-host")
shutil.copy2(out / "control-dll/DiffPlex.Benchmark.dll", out / "control-host/DiffPlex.Benchmark.dll")
