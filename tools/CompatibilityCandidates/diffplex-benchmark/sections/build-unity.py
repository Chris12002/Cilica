"""Build separate instrumented/control players; preserve the instrumented project input."""
from pathlib import Path
import hashlib, json, shutil, subprocess, sys
out = Path(sys.argv[1]).resolve()
variant = sys.argv[2]
assert variant in ("sections", "control")
project = out / "UnitySections"
target = out / ("dll" if variant == "sections" else "control-dll") / "DiffPlex.Benchmark.dll"
plugin = project / "Assets/Plugins/DiffPlex.Benchmark.dll"
shutil.copy2(target, plugin)
cmd = ["unity", "run", str(project), "--editor-version", "6000.3.2f1", "--timeout", "300",
       "--log-file", str(out / (variant + "-unity-build.log")), "--no-tail", "--format", "json",
       "--", "-nographics", "-executeMethod", "BenchmarkBuild.Build", "--cilica-output",
       str(out / "Build" / ("Sections.app" if variant == "sections" else "Control.app"))]
try:
    result = subprocess.run(cmd, text=True, capture_output=True, timeout=330)
    (out / (variant + "-unity-command.log")).write_text(result.stdout + result.stderr)
    result.check_returncode()
    (out / (variant + "-unity-command.json")).write_text(json.dumps({
        "command": cmd, "input_sha256": hashlib.sha256(target.read_bytes()).hexdigest()
    }, indent=2))
finally:
    shutil.copy2(out / "dll/DiffPlex.Benchmark.dll", plugin)
