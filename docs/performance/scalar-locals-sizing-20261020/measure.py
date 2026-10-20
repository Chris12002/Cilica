"""Paired release timing for the scalar-locals sizing experiment (protocol of substring-20261020)."""
import json, statistics, subprocess, hashlib
from pathlib import Path

here = Path(__file__).resolve().parent
variants = ["baseline", "variant_a", "variant_b"]
checksum = {0: 6146, 1: 3122}
summary, raw = {}, {}
for work in (0, 1):
    results = {v: [] for v in variants}
    processes = {v: [] for v in variants}
    for repeat in range(3):
        order = variants[repeat % 3:] + variants[:repeat % 3]
        if repeat % 2: order.reverse()
        for v in order:
            out = subprocess.check_output([str(here / v), "measure", str(work), "1024"], text=True, timeout=120)
            (here / f"timing-work{work}-{v}-{repeat}.jsonl").write_text(out)
            rows = [json.loads(l) for l in out.splitlines()]
            assert len(rows) == 8 and all(r["checksum"] == checksum[work] * r["iterations"] for r in rows[1:])
            samples = [r["ns"] / r["iterations"] for r in rows[1:]]
            results[v] += samples
            processes[v].append(statistics.median(samples))
    base = statistics.median(results["baseline"])
    summary[work] = {v: {"median_ns": statistics.median(s), "min_ns": min(s), "max_ns": max(s), "samples": len(s),
                         "process_medians": processes[v],
                         "reduction_percent": 100 * (1 - statistics.median(s) / base)} for v, s in results.items()}
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
summary["binaries"] = {v: sha(here / v) for v in variants}
summary["sources"] = {v: sha(here / f"{v}.cpp") for v in variants}
(here / "summary.json").write_text(json.dumps(summary, indent=1))
for work in (0, 1):
    print("character" if work == 0 else "line")
    for v in variants:
        s = summary[work][v]
        print(f"  {v:10} median {s['median_ns']/1000:7.3f} us  range {s['min_ns']/1000:.3f}-{s['max_ns']/1000:.3f}"
              f"  processes {[round(x/1000,3) for x in s['process_medians']]}  reduction {s['reduction_percent']:5.2f}%")
