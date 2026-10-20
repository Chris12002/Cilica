"""Sequential paired runs: no inserted scopes, scopes disabled, and scopes enabled."""
from pathlib import Path
import hashlib, json, os, statistics, subprocess, sys
root = Path.cwd()
out = Path(sys.argv[1]).resolve()
env = {k:v for k,v in os.environ.items() if not k.startswith(
    ("DOTNET_Tier", "COMPlus_Tier", "DOTNET_Jit", "COMPlus_Jit", "DOTNET_ReadyToRun", "COMPlus_ReadyToRun"))}
env.update(DOTNET_TieredCompilation="0", DOTNET_ReadyToRun="0")
commands = []

def run(backend, variant, work, mode, tag):
    iterations = 4096 if backend == "unity" else 8192 if backend == "dotnet" else 1024
    enabled = "on" if variant == "on" else "off"
    path = out / (tag + ".json")
    assert not path.exists(), path
    if backend == "unity":
        app = out / "Build" / ("Control.app" if variant == "plain" else "Sections.app")
        cmd = ["open", "-W", "-n", str(app), "--args", "-batchmode", "-nographics",
               "-logFile", str(out / (tag + ".player.log")), "--mode", mode, "--work", str(work),
               "--iterations", str(iterations), "--timers", enabled, "--result", str(path)]
        result = subprocess.run(cmd, text=True, capture_output=True, timeout=60)
        (out / (tag + ".launch.log")).write_text(result.stdout + result.stderr)
        result.check_returncode()
        data = json.loads(path.read_text())
        assert not data.get("error"), data
    elif backend == "dotnet":
        host = out / ("control-host" if variant == "plain" else "host") / "Host.dll"
        cmd = ["dotnet", str(host), mode, str(work), str(iterations), enabled]
        raw = subprocess.check_output(cmd, text=True, env=env, timeout=60)
        path.write_text(raw); data = json.loads(raw)
    else:
        cmd = [str(out / ("plain" if variant == "plain" else "sections")), mode, str(work), str(iterations), enabled]
        raw = subprocess.check_output(cmd, text=True, env=env, timeout=60)
        (out / (tag + ".jsonl")).write_text(raw)
        rows = [json.loads(line) for line in raw.splitlines()]
        data = rows[0] if mode == "verify" else {"samples": rows[1:]}
        if variant == "plain" and mode != "verify":
            for row in data["samples"]:
                row.update(elapsedTicks=row["ns"], frequency=1000000000, sectionTicks=[0]*4, calls=[0]*4)
        path.write_text(json.dumps(data, indent=2))
    commands.append(cmd)
    return data

variants = [(backend, variant) for backend in ["cilica", "dotnet", "unity"] for variant in ["plain", "off", "on"]]
checks = {}
for work in [0, 1]:
    expected = json.loads((root / f"docs/performance/splitcache-20261020/before-verify-work{work}.json").read_text())["values"]
    for backend, variant in variants:
        data = run(backend, variant, work, "verify", f"verify-{backend}-{variant}-{work}")
        assert data["values"] == expected, (backend, variant, work)
        checks[f"{backend}-{variant}-{work}"] = len(expected)
        print("verified", backend, variant, work, flush=True)
(out / "correctness.json").write_text(json.dumps(checks, indent=2))
all_samples = {}
for repeat in range(3):
    for work in ([0, 1] if repeat % 2 == 0 else [1, 0]):
        order = variants[repeat:] + variants[:repeat]
        if repeat % 2: order = list(reversed(order))
        for backend, variant in order:
            tag = f"measure-{backend}-{variant}-{work}-{repeat}"
            data = run(backend, variant, work, "measure", tag)
            rows = data["samples"]
            assert len(rows) == 7
            for row in rows:
                n = row["iterations"]
                assert row["checksum"] == (6146 if work == 0 else 3122) * n
                assert 0 <= sum(row["sectionTicks"]) <= row["elapsedTicks"]
                assert row["calls"] == ([n, 2*n, 2*n, n] if variant == "on" else [0]*4)
            all_samples.setdefault(f"{backend}-{variant}-{work}", []).append(rows)
            print("measured", tag, flush=True)
summary = {}
for key, batches in all_samples.items():
    rows = [r for batch in batches for r in batch]
    scale = lambda row: 1e6 / row["frequency"] / row["iterations"]
    totals = [r["elapsedTicks"]*scale(r) for r in rows]
    summary[key] = {
        "median_us": statistics.median(totals), "min_us": min(totals), "max_us": max(totals),
        "process_medians_us": [statistics.median(r["elapsedTicks"]*scale(r) for r in b) for b in batches],
        "section_medians_us": [statistics.median(r["sectionTicks"][i]*scale(r) for r in rows) for i in range(4)],
        "outside_median_us": statistics.median((r["elapsedTicks"]-sum(r["sectionTicks"]))*scale(r) for r in rows),
        "samples": len(rows)
    }
(out / "summary.json").write_text(json.dumps(summary, indent=2))
(out / "measurement-commands.json").write_text(json.dumps(commands, indent=2))
files = [out/"plain", out/"sections", out/"dll/DiffPlex.Benchmark.dll", out/"control-dll/DiffPlex.Benchmark.dll",
         out/"host/Host.dll", out/"control-host/Host.dll"]
for name in ["Control", "Sections"]:
    app = out / "Build" / (name + ".app")
    files += list((app / "Contents/MacOS").iterdir()) + list(app.rglob("GameAssembly.dylib"))
    files.append(out / "Build" / (name + ".app.settings.json"))
(out / "measurement-provenance.json").write_text(json.dumps({
    "revision": subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip(),
    "files": {str(p.relative_to(out)): hashlib.sha256(p.read_bytes()).hexdigest() for p in files},
    "dotnet": subprocess.check_output(["dotnet", "--info"], text=True),
    "machine": subprocess.check_output(["uname", "-a"], text=True),
    "dotnet_tiering": False, "dotnet_readytorun": False, "sequential": True,
    "section_order": ["setup_results", "chunking", "hash_preparation", "diff_algorithm"],
}, indent=2))
print(json.dumps(summary, indent=2))
